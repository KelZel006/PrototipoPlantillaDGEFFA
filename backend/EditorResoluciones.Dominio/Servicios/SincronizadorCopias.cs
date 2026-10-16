using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Mantiene las plantillas copia iguales a su plantilla de origen. Un tipo de documento puede ser copia de otro
/// (<see cref="TipoDocumento.CopiaDeTipoDocumentoId"/>): la Certificación es copia de la Resolución.
/// <para>
/// Cada vez que se guarda la Resolución (sus párrafos o su diseño), la Certificación con el mismo Rubro, Sub Rubro y
/// Documento queda con la misma información: se crea si no existe, se copian logo, pie de página y espacio entre firmas,
/// y sus párrafos se igualan a los de la Resolución (crear, actualizar, eliminar y orden). Cada copia guarda el párrafo
/// de origen en <see cref="Parrafo.OrigenParrafoId"/>. La copia no se edita directamente (lo impiden los validadores).
/// </para>
/// </summary>
public sealed class SincronizadorCopias(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Parrafo> parrafos,
    IRepositorio<TipoDocumento> tipos,
    IRepositorio<SubRubro> subRubros,
    IRepositorio<ClaseDocumento> clases,
    IServicioHistorial historial,
    TimeProvider reloj) : IObservadorEntidad<Parrafo>, IObservadorEntidad<Plantilla>
{
    public Task DespuesDeGuardarAsync(Parrafo? anterior, Parrafo? actual, CancellationToken ct) =>
        SincronizarAsync((actual ?? anterior)!.PlantillaId, ct);

    public async Task DespuesDeGuardarAsync(Plantilla? anterior, Plantilla? actual, CancellationToken ct)
    {
        if (actual is null)
            return;

        // Si se crea o modifica una copia, se vuelve a llenar desde su origen (si existe).
        if (await tipos.ObtenerAsync(actual.TipoDocumentoId, ct) is { CopiaDeTipoDocumentoId: { } tipoOrigen })
        {
            var origen = (await plantillas.ListarAsync(ct))
                .FirstOrDefault(p => p.TipoDocumentoId == tipoOrigen && MismaCombinacion(p, actual));
            if (origen is not null)
                await SincronizarAsync(origen.Id, ct);
            return;
        }

        await SincronizarAsync(actual.Id, ct);
    }

    /// <summary>Actualiza todas las copias de la plantilla indicada. Si no es origen de ninguna, no hace nada.</summary>
    public async Task SincronizarAsync(string plantillaId, CancellationToken ct = default)
    {
        var origen = await plantillas.ObtenerAsync(plantillaId, ct);
        if (origen is null)
            return;
        var tiposCopia = (await tipos.ListarAsync(ct)).Where(t => t.CopiaDeTipoDocumentoId == origen.TipoDocumentoId).ToList();
        foreach (var tipoCopia in tiposCopia)
            await CopiarAsync(origen, tipoCopia, ct);
    }

    /// <summary>Verdadero si la plantilla es copia de otra y, por lo tanto, no se edita directamente.</summary>
    public static async Task<bool> EsCopiaAsync(
        string plantillaId,
        IRepositorio<Plantilla> plantillas,
        IRepositorio<TipoDocumento> tipos,
        CancellationToken ct) =>
        await plantillas.ObtenerAsync(plantillaId, ct) is { } plantilla &&
        await tipos.ObtenerAsync(plantilla.TipoDocumentoId, ct) is { CopiaDeTipoDocumentoId: not null };

    public const string MensajeCopia =
        "Esta plantilla se copia automáticamente de la Resolución con el mismo Rubro, Sub Rubro y Documento. Haga los cambios en la Resolución.";

    private async Task CopiarAsync(Plantilla origen, TipoDocumento tipoCopia, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var cambios = new List<CambioHistorial>();

        var copia = (await plantillas.ListarAsync(ct))
            .FirstOrDefault(p => p.TipoDocumentoId == tipoCopia.Id && MismaCombinacion(p, origen));
        if (copia is null)
        {
            var subRubro = await subRubros.ObtenerAsync(origen.SubRubroId, ct);
            var clase = await clases.ObtenerAsync(origen.ClaseDocumentoId, ct);
            var nombre = $"{tipoCopia.Nombre} ({clase?.Nombre ?? origen.ClaseDocumentoId}) – {subRubro?.Nombre ?? origen.SubRubroId}";
            var nueva = new Plantilla
            {
                Nombre = nombre.Length > 200 ? nombre[..200] : nombre,
                RubroId = origen.RubroId,
                SubRubroId = origen.SubRubroId,
                TipoDocumentoId = tipoCopia.Id,
                ClaseDocumentoId = origen.ClaseDocumentoId,
                Estado = EstadoPlantilla.Borrador,
                Descripcion = $"Copia automática de la plantilla {origen.Id}.",
                FechaModificacion = ahora,
            };
            CopiarDiseno(origen, nueva);
            copia = await plantillas.CrearAsync(nueva, ct);
            cambios.Add(new CambioHistorial(plantillas.Coleccion, copia.Id, AccionHistorial.Creacion, copia.Version, null, copia));
        }
        else if (!MismoDiseno(origen, copia))
        {
            var anterior = Clonar(copia);
            CopiarDiseno(origen, copia);
            copia.FechaModificacion = ahora;
            copia = await plantillas.ActualizarAsync(copia, ct);
            cambios.Add(new CambioHistorial(plantillas.Coleccion, copia.Id, AccionHistorial.Actualizacion, copia.Version, anterior, copia));
        }

        var todos = await parrafos.ListarAsync(ct);
        var deOrigen = todos.Where(p => p.PlantillaId == origen.Id).ToList();
        var deCopia = todos.Where(p => p.PlantillaId == copia.Id).ToList();
        var porOrigen = deCopia
            .Where(p => p.OrigenParrafoId is not null)
            .GroupBy(p => p.OrigenParrafoId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var crear = new List<Parrafo>();
        var actualizar = new List<(Parrafo Anterior, Parrafo Nuevo)>();
        var conservados = new HashSet<string>(StringComparer.Ordinal);
        // En el texto, el nombre del tipo de origen se cambia por el de la copia («resolución» → «certificación»).
        var tipoOrigen = await tipos.ObtenerAsync(origen.TipoDocumentoId, ct);
        var cambio = tipoOrigen is null ? null : new CambioPalabra(tipoOrigen.Nombre, tipoCopia.Nombre);
        foreach (var parrafo in deOrigen)
        {
            var nuevo = Clonar(parrafo);
            if (cambio is not null)
                nuevo.Contenido = cambio.Aplicar(nuevo.Contenido);
            nuevo.PlantillaId = copia.Id;
            nuevo.OrigenParrafoId = parrafo.Id;
            nuevo.FechaModificacion = ahora;
            if (porOrigen.TryGetValue(parrafo.Id, out var existente))
            {
                conservados.Add(existente.Id);
                nuevo.Id = existente.Id;
                nuevo.Version = existente.Version;
                if (Huella(nuevo) != Huella(existente))
                    actualizar.Add((existente, nuevo));
            }
            else
            {
                nuevo.Id = "";
                crear.Add(nuevo);
            }
        }

        if (crear.Count > 0 || actualizar.Count > 0)
        {
            var lote = await parrafos.AplicarLoteAsync(crear, actualizar.Select(a => a.Nuevo).ToList(), ct);
            cambios.AddRange(lote.Creados.Select(p =>
                new CambioHistorial(parrafos.Coleccion, p.Id, AccionHistorial.Creacion, p.Version, null, p)));
            var anteriores = actualizar.ToDictionary(a => a.Anterior.Id, a => a.Anterior);
            cambios.AddRange(lote.Actualizados.Select(p =>
                new CambioHistorial(parrafos.Coleccion, p.Id, AccionHistorial.Actualizacion, p.Version, anteriores[p.Id], p)));
        }

        // Lo que ya no está en la Resolución (o no vino de ella) se elimina de la copia.
        foreach (var sobrante in deCopia.Where(p => !conservados.Contains(p.Id)))
        {
            await parrafos.EliminarAsync(sobrante.Id, null, ct);
            cambios.Add(new CambioHistorial(parrafos.Coleccion, sobrante.Id, AccionHistorial.Eliminacion, sobrante.Version, sobrante, null));
        }

        if (cambios.Count > 0)
            await historial.RegistrarAsync(cambios, ct);
    }

    private static bool MismaCombinacion(Plantilla a, Plantilla b) =>
        a.RubroId == b.RubroId && a.SubRubroId == b.SubRubroId && a.ClaseDocumentoId == b.ClaseDocumentoId;

    private static void CopiarDiseno(Plantilla origen, Plantilla destino)
    {
        destino.LogoId = origen.LogoId;
        destino.AlineacionLogo = origen.AlineacionLogo;
        destino.PiePagina = origen.PiePagina;
        destino.EspacioFirmasCm = origen.EspacioFirmasCm;
    }

    private static bool MismoDiseno(Plantilla a, Plantilla b) =>
        a.LogoId == b.LogoId && a.AlineacionLogo == b.AlineacionLogo && a.PiePagina == b.PiePagina &&
        a.EspacioFirmasCm.Equals(b.EspacioFirmasCm);

    /// <summary>Lo que se copia de un párrafo; si no cambió, la copia no se reescribe.</summary>
    private static string Huella(Parrafo p) => JsonSerializer.Serialize(
        new { p.Seccion, p.Orden, p.Contenido, p.Activo, p.MostrarSoloSiCumple, p.Condicion, p.CamposUsados, p.OrigenParrafoId },
        OpcionesJson.Predeterminadas);

    private static T Clonar<T>(T entidad) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(entidad, OpcionesJson.Predeterminadas), OpcionesJson.Predeterminadas)!;
}

/// <summary>
/// La copia (Certificación) no se edita directamente: sus párrafos no se crean, modifican ni eliminan, y su diseño
/// (logo, pie de página, espacio entre firmas) no cambia. Todo eso llega desde la Resolución.
/// </summary>
public sealed class ReglaCopiasNoEditables(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<TipoDocumento> tipos) : Validacion.IReglaModificacion<Parrafo>, Validacion.IReglaModificacion<Plantilla>
{
    public async Task ValidarModificacionAsync(Parrafo parrafo, Validacion.ResultadoValidacion resultado, CancellationToken ct)
    {
        if (await SincronizadorCopias.EsCopiaAsync(parrafo.PlantillaId, plantillas, tipos, ct))
            resultado.Agregar("plantillaId", SincronizadorCopias.MensajeCopia);
    }

    public async Task ValidarModificacionAsync(Plantilla plantilla, Validacion.ResultadoValidacion resultado, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(plantilla.Id) ||
            await tipos.ObtenerAsync(plantilla.TipoDocumentoId, ct) is not { CopiaDeTipoDocumentoId: not null } ||
            await plantillas.ObtenerAsync(plantilla.Id, ct) is not { } guardada)
        {
            return;
        }
        if (guardada.LogoId != plantilla.LogoId || guardada.AlineacionLogo != plantilla.AlineacionLogo ||
            guardada.PiePagina != plantilla.PiePagina || !guardada.EspacioFirmasCm.Equals(plantilla.EspacioFirmasCm))
        {
            resultado.Agregar("logoId", SincronizadorCopias.MensajeCopia);
        }
    }
}
