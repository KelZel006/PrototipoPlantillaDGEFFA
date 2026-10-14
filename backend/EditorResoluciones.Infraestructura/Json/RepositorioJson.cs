using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Infraestructura.Json;

/// <summary>Repositorio genérico sobre un archivo JSON. Cada operación de escritura es atómica para su colección.</summary>
public sealed class RepositorioJson<T>(AlmacenJson almacen) : IRepositorio<T> where T : class, IEntidad
{
    private static readonly DefinicionColeccion Definicion = ColeccionesJson.De<T>();

    public string Coleccion => Definicion.Nombre;

    public async Task<IReadOnlyList<T>> ListarAsync(CancellationToken ct = default) =>
        await almacen.LeerAsync<T>(Definicion.Nombre, ct);

    public async Task<T?> ObtenerAsync(string id, CancellationToken ct = default) =>
        (await almacen.LeerAsync<T>(Definicion.Nombre, ct)).FirstOrDefault(e => e.Id == id);

    public Task<T> CrearAsync(T entidad, CancellationToken ct = default) =>
        almacen.ModificarAsync<T, T>(Definicion.Nombre, registros =>
        {
            if (string.IsNullOrWhiteSpace(entidad.Id))
                entidad.Id = GenerarId(registros);
            else if (registros.Any(e => e.Id == entidad.Id))
                throw new ConflictoException($"Ya existe un registro con el identificador '{entidad.Id}' en {Definicion.Nombre}.");

            entidad.Version = 1;
            registros.Add(entidad);
            return entidad;
        }, ct);

    public Task<T> ActualizarAsync(T entidad, CancellationToken ct = default) =>
        almacen.ModificarAsync<T, T>(Definicion.Nombre, registros =>
        {
            var indice = registros.FindIndex(e => e.Id == entidad.Id);
            if (indice < 0)
                throw new EntidadNoEncontradaException(Definicion.Nombre, entidad.Id);
            var guardada = registros[indice].Version;
            if (guardada != entidad.Version)
                throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, entidad.Version));

            entidad.Version = guardada + 1;
            registros[indice] = entidad;
            return entidad;
        }, ct);

    public Task EliminarAsync(string id, int? version, CancellationToken ct = default) =>
        almacen.ModificarAsync<T, bool>(Definicion.Nombre, registros =>
        {
            var indice = registros.FindIndex(e => e.Id == id);
            if (indice < 0)
                throw new EntidadNoEncontradaException(Definicion.Nombre, id);
            var guardada = registros[indice].Version;
            if (version is not null && guardada != version)
                throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, version.Value));

            registros.RemoveAt(indice);
            return true;
        }, ct);

    public Task<LoteAplicado<T>> AplicarLoteAsync(IReadOnlyList<T> crear, IReadOnlyList<T> actualizar, CancellationToken ct = default) =>
        almacen.ModificarAsync<T, LoteAplicado<T>>(Definicion.Nombre, registros =>
        {
            // Primero se comprueba todo; solo si no hay conflictos se modifica la lista.
            var indices = new List<int>(actualizar.Count);
            foreach (var entidad in actualizar)
            {
                var indice = registros.FindIndex(e => e.Id == entidad.Id);
                if (indice < 0)
                    throw new EntidadNoEncontradaException(Definicion.Nombre, entidad.Id);
                var guardada = registros[indice].Version;
                if (guardada != entidad.Version)
                    throw new ConflictoException(ServicioEntidades<T>.MensajeVersion(guardada, entidad.Version));
                indices.Add(indice);
            }
            if (actualizar.Select(e => e.Id).Distinct().Count() != actualizar.Count)
                throw new ConflictoException("El lote repite registros.");
            foreach (var entidad in crear.Where(e => !string.IsNullOrWhiteSpace(e.Id)))
            {
                if (registros.Any(e => e.Id == entidad.Id))
                    throw new ConflictoException($"Ya existe un registro con el identificador '{entidad.Id}' en {Definicion.Nombre}.");
            }

            for (var i = 0; i < actualizar.Count; i++)
            {
                actualizar[i].Version++;
                registros[indices[i]] = actualizar[i];
            }
            foreach (var entidad in crear)
            {
                if (string.IsNullOrWhiteSpace(entidad.Id))
                    entidad.Id = GenerarId(registros);
                entidad.Version = 1;
                registros.Add(entidad);
            }
            return new LoteAplicado<T>(crear, actualizar);
        }, ct);

    private static string GenerarId(List<T> registros)
    {
        string id;
        do
        {
            id = $"{Definicion.Prefijo}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        } while (registros.Any(e => e.Id == id));
        return id;
    }
}

/// <summary>Verifica que cada colección registrada se pueda leer.</summary>
public sealed class DiagnosticoAlmacenamientoJson(AlmacenJson almacen) : IDiagnosticoAlmacenamiento
{
    public async Task<IReadOnlyList<EstadoColeccion>> VerificarAsync(CancellationToken ct = default)
    {
        var estados = new List<EstadoColeccion>();
        foreach (var definicion in ColeccionesJson.Todas)
        {
            try
            {
                var registros = await almacen.LeerAsync<System.Text.Json.JsonElement>(definicion.Nombre, ct);
                estados.Add(new EstadoColeccion(definicion.Nombre, true, registros.Count, null));
            }
            catch (AlmacenamientoException ex)
            {
                estados.Add(new EstadoColeccion(definicion.Nombre, false, 0, ex.Message));
            }
        }
        return estados;
    }
}
