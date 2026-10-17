using System.Text.Json;
using System.Text.Json.Nodes;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Infraestructura.Json;

/// <summary>
/// Transacciones de varias colecciones sobre archivos JSON.
/// <list type="number">
/// <item>Se ejecuta una transacción a la vez. Cada colección se bloquea la primera vez que se usa y queda bloqueada
/// hasta el final, así nadie la modifica en medio.</item>
/// <item>Los cambios se hacen en memoria. Si la operación falla, no se escribe nada.</item>
/// <item>Antes de escribir, se guarda un diario con el contenido final de cada colección modificada.</item>
/// <item>Se reemplaza cada archivo de forma atómica y se borra el diario. Si una escritura falla, las colecciones ya
/// escritas se restauran a su contenido anterior y se borra el diario: no queda nada aplicado a medias.</item>
/// <item>Si el proceso se interrumpe de golpe en ese paso (sin poder restaurar), al iniciar se completa la transacción
/// desde el diario (<see cref="AlmacenJson"/>): todo queda aplicado.</item>
/// </list>
/// Protege contra escrituras simultáneas dentro de un solo proceso de la API y contra interrupciones. No es una
/// solución de producción: varios procesos escribiendo los mismos archivos no están protegidos.
/// </summary>
public sealed class UnidadDeTrabajoJson(AlmacenJson almacen) : IUnidadDeTrabajo
{
    public async Task<TResultado> EjecutarAsync<TResultado>(Func<ITransaccion, Task<TResultado>> operacion, CancellationToken ct = default)
    {
        await almacen.BloqueoTransacciones.WaitAsync(ct);
        var transaccion = new TransaccionJson(almacen);
        try
        {
            var resultado = await operacion(transaccion);
            await transaccion.ConfirmarAsync(ct);
            return resultado;
        }
        finally
        {
            transaccion.LiberarBloqueos();
            almacen.BloqueoTransacciones.Release();
        }
    }

    private sealed class TransaccionJson(AlmacenJson almacen) : ITransaccion
    {
        /// <summary>Colecciones abiertas en la transacción, en el orden en que se usaron.</summary>
        private readonly List<ColeccionAbierta> _abiertas = [];

        public async Task<IReadOnlyList<T>> ListarAsync<T>(CancellationToken ct = default) where T : class, IEntidad =>
            (await AbrirAsync<T>(ct)).Registros;

        public async Task<T> CrearAsync<T>(T entidad, CancellationToken ct = default) where T : class, IEntidad
        {
            var coleccion = await AbrirAsync<T>(ct);
            if (string.IsNullOrWhiteSpace(entidad.Id))
                entidad.Id = GeneradorIds.Nuevo(coleccion.Definicion.Prefijo, coleccion.Registros);
            else if (coleccion.Registros.Any(e => e.Id == entidad.Id))
                throw new ConflictoException($"Ya existe un registro con el identificador '{entidad.Id}' en {coleccion.Definicion.Nombre}.");

            entidad.Version = 1;
            coleccion.Registros.Add(entidad);
            coleccion.Modificada = true;
            return entidad;
        }

        public async Task<T> ActualizarAsync<T>(T entidad, CancellationToken ct = default) where T : class, IEntidad
        {
            var coleccion = await AbrirAsync<T>(ct);
            var indice = coleccion.Registros.FindIndex(e => e.Id == entidad.Id);
            if (indice < 0)
                throw new EntidadNoEncontradaException(coleccion.Definicion.Nombre, entidad.Id);
            var guardada = coleccion.Registros[indice].Version;
            if (guardada != entidad.Version)
                throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, entidad.Version));

            entidad.Version = guardada + 1;
            coleccion.Registros[indice] = entidad;
            coleccion.Modificada = true;
            return entidad;
        }

        private async Task<ColeccionAbierta<T>> AbrirAsync<T>(CancellationToken ct) where T : class, IEntidad
        {
            var definicion = ColeccionesJson.De<T>();
            if (_abiertas.FirstOrDefault(a => a.Definicion == definicion) is { } abierta)
                return (ColeccionAbierta<T>)abierta;

            await almacen.BloquearAsync(definicion.Nombre, ct);
            try
            {
                var registros = await almacen.LeerSinBloqueoAsync<T>(definicion.Nombre, ct);
                var nueva = new ColeccionAbierta<T>(definicion, registros);
                _abiertas.Add(nueva);
                return nueva;
            }
            catch
            {
                almacen.Liberar(definicion.Nombre);
                throw;
            }
        }

        public async Task ConfirmarAsync(CancellationToken ct)
        {
            var modificadas = _abiertas.Where(a => a.Modificada).ToList();
            if (modificadas.Count == 0)
                return;

            // 1. Diario con el contenido final. Se escribe en un temporal y se renombra: existe completo o no existe.
            Directory.CreateDirectory(almacen.DirectorioTransacciones);
            var diario = Path.Combine(almacen.DirectorioTransacciones, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
            var contenido = new JsonObject
            {
                ["colecciones"] = new JsonObject(modificadas.Select(m =>
                    KeyValuePair.Create(m.Definicion.Nombre, (JsonNode?)m.ComoJson()))),
            };
            await File.WriteAllTextAsync(diario + ".tmp", contenido.ToJsonString(OpcionesJson.Predeterminadas), ct);
            File.Move(diario + ".tmp", diario);

            // 2. Cada colección, de forma atómica.
            var escritas = new List<ColeccionAbierta>();
            try
            {
                foreach (var coleccion in modificadas)
                {
                    if (almacen.AntesDeEscribirEnTransaccion is { } antes)
                        await antes(coleccion.Definicion.Nombre);
                    await coleccion.EscribirAsync(almacen, CancellationToken.None);
                    escritas.Add(coleccion);
                }
            }
            catch (Exception error)
            {
                // Se revierte lo ya escrito. Si tampoco se puede, el diario queda y se completa al reiniciar.
                try
                {
                    foreach (var escrita in escritas)
                        await escrita.RestaurarAsync(almacen);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or AlmacenamientoException)
                {
                    throw new AlmacenamientoException(
                        "No se pudo terminar ni revertir la operación. Se completará automáticamente al reiniciar la API.", error);
                }
                File.Delete(diario);
                throw new AlmacenamientoException("No se pudo guardar la operación. No se aplicó ningún cambio.", error);
            }

            // 3. Transacción terminada.
            File.Delete(diario);
        }

        public void LiberarBloqueos()
        {
            foreach (var abierta in _abiertas)
                almacen.Liberar(abierta.Definicion.Nombre);
            _abiertas.Clear();
        }
    }

    private abstract class ColeccionAbierta(DefinicionColeccion definicion)
    {
        public DefinicionColeccion Definicion { get; } = definicion;
        public bool Modificada { get; set; }
        public abstract JsonNode ComoJson();
        public abstract Task EscribirAsync(AlmacenJson almacen, CancellationToken ct);

        /// <summary>Vuelve a escribir el contenido que tenía la colección al abrirla.</summary>
        public abstract Task RestaurarAsync(AlmacenJson almacen);
    }

    private sealed class ColeccionAbierta<T>(DefinicionColeccion definicion, List<T> registros) : ColeccionAbierta(definicion)
    {
        /// <summary>Copia del contenido al abrir, para revertir.</summary>
        private readonly JsonNode _original = JsonSerializer.SerializeToNode(registros, OpcionesJson.Predeterminadas)!;

        public List<T> Registros { get; } = registros;

        public override Task RestaurarAsync(AlmacenJson almacen) =>
            almacen.EscribirSinBloqueoAsync(Definicion.Nombre, _original.Deserialize<List<JsonElement>>(OpcionesJson.Predeterminadas)!, CancellationToken.None);

        public override JsonNode ComoJson() => JsonSerializer.SerializeToNode(Registros, OpcionesJson.Predeterminadas)!;

        public override Task EscribirAsync(AlmacenJson almacen, CancellationToken ct) =>
            almacen.EscribirSinBloqueoAsync(Definicion.Nombre, Registros, ct);
    }
}
