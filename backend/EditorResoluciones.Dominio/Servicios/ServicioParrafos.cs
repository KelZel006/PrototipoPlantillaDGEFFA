using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>Posición enviada por el cliente: el párrafo y la versión que el cliente tiene cargada.</summary>
public sealed record ElementoOrden(string Id, int Version);

public sealed record SolicitudReorden(string PlantillaId, Seccion Seccion, List<ElementoOrden> Elementos);

/// <summary>Operaciones sobre varios párrafos a la vez. Cada una se guarda de forma atómica.</summary>
public sealed class ServicioParrafos(
    IRepositorio<Parrafo> repositorio,
    IServicioHistorial historial,
    TimeProvider reloj)
{
    /// <summary>
    /// Aplica el nuevo orden de una sección. La lista debe contener exactamente los párrafos actuales
    /// de la sección; si otra persona agregó, quitó o modificó alguno, se rechaza para no perder cambios.
    /// </summary>
    public async Task<IReadOnlyList<Parrafo>> ReordenarAsync(SolicitudReorden solicitud, CancellationToken ct = default)
    {
        var actuales = await DeSeccionAsync(solicitud.PlantillaId, solicitud.Seccion, ct);
        var idsEnviados = solicitud.Elementos.Select(e => e.Id).ToList();
        if (idsEnviados.Distinct().Count() != idsEnviados.Count ||
            !idsEnviados.ToHashSet().SetEquals(actuales.Select(p => p.Id)))
        {
            throw new ConflictoException("La lista de párrafos cambió mientras la ordenaba. Recargue la sección e intente de nuevo.");
        }

        var porId = actuales.ToDictionary(p => p.Id);
        var cambios = new List<(Parrafo Anterior, Parrafo Nuevo)>();
        for (var posicion = 0; posicion < solicitud.Elementos.Count; posicion++)
        {
            var elemento = solicitud.Elementos[posicion];
            var anterior = porId[elemento.Id];
            if (anterior.Version != elemento.Version)
                throw new ConflictoException(ServicioEntidades<Parrafo>.MensajeVersion(anterior.Version, elemento.Version));
            if (anterior.Orden == posicion + 1)
                continue;

            var nuevo = Clonar(anterior);
            nuevo.Orden = posicion + 1;
            nuevo.FechaModificacion = reloj.GetUtcNow();
            cambios.Add((anterior, nuevo));
        }

        if (cambios.Count > 0)
        {
            await repositorio.AplicarLoteAsync([], cambios.Select(c => c.Nuevo).ToList(), ct);
            await historial.RegistrarAsync(
                cambios.Select(c => Actualizacion(c.Anterior, c.Nuevo)).ToList(), ct);
        }

        return await DeSeccionAsync(solicitud.PlantillaId, solicitud.Seccion, ct);
    }

    /// <summary>
    /// Crea una copia del párrafo, con su condición y su estado, justo debajo del original.
    /// Los párrafos siguientes bajan una posición.
    /// </summary>
    public async Task<Parrafo> DuplicarAsync(string id, CancellationToken ct = default)
    {
        var original = await repositorio.ObtenerAsync(id, ct) ?? throw new EntidadNoEncontradaException(repositorio.Coleccion, id);
        var ahora = reloj.GetUtcNow();

        var copia = Clonar(original);
        copia.Id = "";
        copia.Orden = original.Orden + 1;
        copia.FechaModificacion = ahora;

        var desplazados = (await DeSeccionAsync(original.PlantillaId, original.Seccion, ct))
            .Where(p => p.Id != original.Id && p.Orden > original.Orden)
            .Select(p => (Anterior: p, Nuevo: Clonar(p)))
            .ToList();
        foreach (var (_, nuevo) in desplazados)
        {
            nuevo.Orden++;
            nuevo.FechaModificacion = ahora;
        }

        var lote = await repositorio.AplicarLoteAsync([copia], desplazados.Select(d => d.Nuevo).ToList(), ct);
        var creada = lote.Creados[0];

        await historial.RegistrarAsync(
        [
            new CambioHistorial(repositorio.Coleccion, creada.Id, AccionHistorial.Creacion, creada.Version, null, creada),
            .. desplazados.Select(d => Actualizacion(d.Anterior, d.Nuevo)),
        ], ct);

        return creada;
    }

    private async Task<List<Parrafo>> DeSeccionAsync(string plantillaId, Seccion seccion, CancellationToken ct) =>
        (await repositorio.ListarAsync(ct))
            .Where(p => p.PlantillaId == plantillaId && p.Seccion == seccion)
            .OrderBy(p => p.Orden)
            .ToList();

    private CambioHistorial Actualizacion(Parrafo anterior, Parrafo nuevo) =>
        new(repositorio.Coleccion, nuevo.Id, AccionHistorial.Actualizacion, nuevo.Version, anterior, nuevo);

    private static Parrafo Clonar(Parrafo parrafo) =>
        JsonSerializer.Deserialize<Parrafo>(JsonSerializer.Serialize(parrafo, OpcionesJson.Predeterminadas), OpcionesJson.Predeterminadas)!;
}
