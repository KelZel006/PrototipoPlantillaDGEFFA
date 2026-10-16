using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Lleva a las plantillas copia lo que se cambia en su plantilla de origen. Un tipo de documento puede ser copia de otro
/// (<see cref="TipoDocumento.CopiaDeTipoDocumentoId"/>): la Certificación es copia de la Resolución.
/// <para>
/// La Certificación se puede editar libremente, y cada cambio que se guarda en la Resolución con el mismo Rubro, Sub Rubro
/// y Documento pasa a la Certificación:
/// </para>
/// <list type="bullet">
/// <item>Solo se copia lo que cambió en la Resolución (texto, estado, condición, posición; logo, pie de página o espacio
/// entre firmas). Si ese mismo dato se había editado en la Certificación, gana el de la Resolución; lo demás que se editó
/// en la Certificación se conserva.</item>
/// <item>Un párrafo nuevo en la Resolución se agrega a la Certificación después del párrafo que lo precede; uno eliminado
/// en la Resolución se elimina de la Certificación.</item>
/// <item>Los párrafos agregados directamente en la Certificación (sin <see cref="Parrafo.OrigenParrafoId"/>) no se
/// tocan.</item>
/// <item>Si la Certificación no existe, se crea como copia completa de la Resolución.</item>
/// </list>
/// Los cambios hechos en la Certificación no vuelven a la Resolución.
/// </summary>
public sealed class SincronizadorCopias(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Parrafo> parrafos,
    IRepositorio<TipoDocumento> tipos,
    IRepositorio<SubRubro> subRubros,
    IRepositorio<ClaseDocumento> clases,
    IRepositorio<ArchivoRecurso> archivos,
    IAlmacenArchivos almacen,
    IServicioHistorial historial,
    TimeProvider reloj) : IObservadorEntidad<Parrafo>, IObservadorEntidad<Plantilla>
{
    public Task DespuesDeGuardarAsync(Parrafo? anterior, Parrafo? actual, CancellationToken ct) =>
        CopiarCambiosAsync([(anterior, actual)], ct);

    public async Task DespuesDeGuardarAsync(Plantilla? anterior, Plantilla? actual, CancellationToken ct)
    {
        if (actual is null)
            return;

        // Una copia recién creada se llena desde su origen (si existe). Después se edita libremente.
        if (await tipos.ObtenerAsync(actual.TipoDocumentoId, ct) is { CopiaDeTipoDocumentoId: { } tipoOrigen })
        {
            if (anterior is null && await BuscarAsync(tipoOrigen, actual, ct) is { } origen)
                await LlenarAsync(origen, actual, ct);
            return;
        }

        foreach (var tipoCopia in await TiposCopiaAsync(actual.TipoDocumentoId, ct))
        {
            if (await BuscarAsync(tipoCopia.Id, actual, ct) is { } copia)
                await CopiarDisenoAsync(anterior, actual, copia, ct);
            else
                await CrearCopiaAsync(actual, tipoCopia, ct);
        }
    }

    /// <summary>
    /// Lleva a las copias los cambios de párrafos de una misma plantilla de origen (<c>Anterior</c> es null al crear y
    /// <c>Actual</c> es null al eliminar). Si la plantilla no es origen de ninguna copia, no hace nada.
    /// </summary>
    public async Task CopiarCambiosAsync(IReadOnlyList<(Parrafo? Anterior, Parrafo? Actual)> cambios, CancellationToken ct = default)
    {
        if (cambios.Count == 0)
            return;
        var origen = await plantillas.ObtenerAsync((cambios[0].Actual ?? cambios[0].Anterior)!.PlantillaId, ct);
        if (origen is null)
            return;

        foreach (var tipoCopia in await TiposCopiaAsync(origen.TipoDocumentoId, ct))
        {
            if (await BuscarAsync(tipoCopia.Id, origen, ct) is { } copia)
                await AplicarCambiosAsync(origen, copia, tipoCopia, cambios, ct);
            else
                await CrearCopiaAsync(origen, tipoCopia, ct);
        }
    }

    private async Task<List<TipoDocumento>> TiposCopiaAsync(string tipoOrigenId, CancellationToken ct) =>
        (await tipos.ListarAsync(ct)).Where(t => t.CopiaDeTipoDocumentoId == tipoOrigenId).ToList();

    /// <summary>La plantilla del tipo indicado con el mismo Rubro, Sub Rubro y Documento.</summary>
    private async Task<Plantilla?> BuscarAsync(string tipoDocumentoId, Plantilla referencia, CancellationToken ct) =>
        (await plantillas.ListarAsync(ct)).FirstOrDefault(p =>
            p.TipoDocumentoId == tipoDocumentoId && p.RubroId == referencia.RubroId &&
            p.SubRubroId == referencia.SubRubroId && p.ClaseDocumentoId == referencia.ClaseDocumentoId);

    private async Task CrearCopiaAsync(Plantilla origen, TipoDocumento tipoCopia, CancellationToken ct)
    {
        var subRubro = await subRubros.ObtenerAsync(origen.SubRubroId, ct);
        var clase = await clases.ObtenerAsync(origen.ClaseDocumentoId, ct);
        var nombre = $"{tipoCopia.Nombre} ({clase?.Nombre ?? origen.ClaseDocumentoId}) – {subRubro?.Nombre ?? origen.SubRubroId}";
        var copia = await plantillas.CrearAsync(new Plantilla
        {
            Nombre = nombre.Length > 200 ? nombre[..200] : nombre,
            RubroId = origen.RubroId,
            SubRubroId = origen.SubRubroId,
            TipoDocumentoId = tipoCopia.Id,
            ClaseDocumentoId = origen.ClaseDocumentoId,
            Estado = EstadoPlantilla.Borrador,
            Descripcion = $"Copia automática de la plantilla {origen.Id}.",
            FechaModificacion = reloj.GetUtcNow(),
        }, ct);
        await historial.RegistrarAsync(plantillas.Coleccion, copia.Id, AccionHistorial.Creacion, copia.Version, null, copia, ct);
        await LlenarAsync(origen, copia, ct);
    }

    /// <summary>Copia en la plantilla copia el diseño y todos los párrafos del origen que todavía no tiene.</summary>
    private async Task LlenarAsync(Plantilla origen, Plantilla copia, CancellationToken ct)
    {
        await CopiarDisenoAsync(null, origen, copia, ct);

        var todos = await parrafos.ListarAsync(ct);
        var copiados = todos.Where(p => p.PlantillaId == copia.Id && p.OrigenParrafoId is not null)
            .Select(p => p.OrigenParrafoId!).ToHashSet(StringComparer.Ordinal);
        var cambios = todos.Where(p => p.PlantillaId == origen.Id && !copiados.Contains(p.Id))
            .Select(p => ((Parrafo?)null, (Parrafo?)p)).ToList();
        if (cambios.Count > 0 && await tipos.ObtenerAsync(copia.TipoDocumentoId, ct) is { } tipoCopia)
            await AplicarCambiosAsync(origen, copia, tipoCopia, cambios, ct);
    }

    /// <summary>
    /// Copia el logo, la alineación, el pie de página y el espacio entre firmas que cambiaron en el origen
    /// (con <paramref name="anterior"/> null, todos).
    /// </summary>
    private async Task CopiarDisenoAsync(Plantilla? anterior, Plantilla actual, Plantilla copia, CancellationToken ct)
    {
        var nueva = Clonar(copia);
        if (anterior is null || anterior.LogoId != actual.LogoId)
            nueva.LogoId = actual.LogoId;
        if (anterior is null || anterior.AlineacionLogo != actual.AlineacionLogo)
            nueva.AlineacionLogo = actual.AlineacionLogo;
        if (anterior is null || anterior.PiePagina != actual.PiePagina)
            nueva.PiePagina = actual.PiePagina;
        if (anterior is null || !anterior.EspacioFirmasCm.Equals(actual.EspacioFirmasCm))
            nueva.EspacioFirmasCm = actual.EspacioFirmasCm;

        if (nueva.LogoId == copia.LogoId && nueva.AlineacionLogo == copia.AlineacionLogo &&
            nueva.PiePagina == copia.PiePagina && nueva.EspacioFirmasCm.Equals(copia.EspacioFirmasCm))
        {
            return;
        }

        nueva.FechaModificacion = reloj.GetUtcNow();
        var guardada = await plantillas.ActualizarAsync(nueva, ct);
        await historial.RegistrarAsync(plantillas.Coleccion, guardada.Id, AccionHistorial.Actualizacion, guardada.Version, copia, guardada, ct);

        // Si la copia tenía un logo propio que ya nadie usa, se borra el archivo.
        if (!string.IsNullOrEmpty(copia.LogoId) && copia.LogoId != guardada.LogoId &&
            !(await plantillas.ListarAsync(ct)).Any(p => p.LogoId == copia.LogoId))
        {
            if (await archivos.ObtenerAsync(copia.LogoId, ct) is not null)
                await archivos.EliminarAsync(copia.LogoId, null, ct);
            await almacen.EliminarAsync(copia.LogoId, ct);
        }
    }

    private async Task AplicarCambiosAsync(
        Plantilla origen,
        Plantilla copia,
        TipoDocumento tipoCopia,
        IReadOnlyList<(Parrafo? Anterior, Parrafo? Actual)> cambios,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        // En el texto, el nombre del tipo de origen se cambia por el de la copia («resolución» → «certificación»).
        var tipoOrigen = await tipos.ObtenerAsync(origen.TipoDocumentoId, ct);
        var palabra = tipoOrigen is null ? null : new CambioPalabra(tipoOrigen.Nombre, tipoCopia.Nombre);

        var todos = await parrafos.ListarAsync(ct);
        var deOrigen = todos.Where(p => p.PlantillaId == origen.Id).ToList();
        var guardados = todos.Where(p => p.PlantillaId == copia.Id).ToList();
        // Se trabaja sobre clones; al final se comparan con los guardados para escribir solo lo que cambió.
        var trabajo = guardados.Select(Clonar).ToList();
        var reubicar = new List<Parrafo>();
        var secciones = new HashSet<Seccion>();

        foreach (var (anterior, actual) in cambios)
        {
            var origenId = (actual ?? anterior)!.Id;
            var copias = trabajo.Where(p => p.OrigenParrafoId == origenId).ToList();

            if (actual is null)
            {
                foreach (var eliminada in copias)
                {
                    trabajo.Remove(eliminada);
                    secciones.Add(eliminada.Seccion);
                }
                continue;
            }

            // Nuevo en la Resolución (o eliminado antes en la Certificación): se agrega completo.
            if (copias.Count == 0)
            {
                var nuevo = Clonar(actual);
                nuevo.Id = "";
                nuevo.Version = 0;
                nuevo.PlantillaId = copia.Id;
                nuevo.OrigenParrafoId = actual.Id;
                nuevo.Contenido = palabra?.Aplicar(nuevo.Contenido) ?? nuevo.Contenido;
                trabajo.Add(nuevo);
                reubicar.Add(nuevo);
                secciones.Add(nuevo.Seccion);
                continue;
            }

            // Solo lo que cambió en la Resolución reemplaza lo de la Certificación.
            foreach (var destino in copias)
            {
                if (anterior is null || !MismoJson(anterior.Contenido, actual.Contenido))
                {
                    destino.Contenido = palabra?.Aplicar(Clonar(actual.Contenido)) ?? Clonar(actual.Contenido);
                    destino.CamposUsados = [.. actual.CamposUsados];
                }
                if (anterior is null || anterior.Activo != actual.Activo)
                    destino.Activo = actual.Activo;
                if (anterior is null || anterior.MostrarSoloSiCumple != actual.MostrarSoloSiCumple)
                    destino.MostrarSoloSiCumple = actual.MostrarSoloSiCumple;
                if (anterior is null || !MismoJson(anterior.Condicion, actual.Condicion))
                    destino.Condicion = actual.Condicion is null ? null : Clonar(actual.Condicion);
                if (anterior is null || anterior.Seccion != actual.Seccion || anterior.Orden != actual.Orden)
                {
                    secciones.Add(destino.Seccion);
                    destino.Seccion = actual.Seccion;
                    secciones.Add(destino.Seccion);
                    reubicar.Add(destino);
                }
            }
        }

        foreach (var seccion in secciones)
            Ordenar(trabajo, reubicar, deOrigen, seccion);

        // Guardar solo las diferencias.
        var porId = guardados.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var crear = trabajo.Where(p => p.Id == "").ToList();
        var actualizar = trabajo.Where(p => p.Id != "" && Huella(p) != Huella(porId[p.Id])).ToList();
        foreach (var parrafo in crear.Concat(actualizar))
            parrafo.FechaModificacion = ahora;
        var conservados = trabajo.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var eliminar = guardados.Where(p => !conservados.Contains(p.Id)).ToList();

        var registro = new List<CambioHistorial>();
        if (crear.Count > 0 || actualizar.Count > 0)
        {
            var lote = await parrafos.AplicarLoteAsync(crear, actualizar, ct);
            registro.AddRange(lote.Creados.Select(p =>
                new CambioHistorial(parrafos.Coleccion, p.Id, AccionHistorial.Creacion, p.Version, null, p)));
            registro.AddRange(lote.Actualizados.Select(p =>
                new CambioHistorial(parrafos.Coleccion, p.Id, AccionHistorial.Actualizacion, p.Version, porId[p.Id], p)));
        }
        foreach (var sobrante in eliminar)
        {
            await parrafos.EliminarAsync(sobrante.Id, null, ct);
            registro.Add(new CambioHistorial(parrafos.Coleccion, sobrante.Id, AccionHistorial.Eliminacion, sobrante.Version, sobrante, null));
        }
        if (registro.Count > 0)
            await historial.RegistrarAsync(registro, ct);
    }

    /// <summary>
    /// Ordena una sección de la copia. Los párrafos que no se mueven conservan su orden (incluidos los agregados en la
    /// Certificación); cada párrafo a reubicar se coloca después de la copia del párrafo que lo precede en la Resolución.
    /// Al final la sección queda numerada 1, 2, 3…
    /// </summary>
    private static void Ordenar(List<Parrafo> trabajo, List<Parrafo> reubicar, List<Parrafo> deOrigen, Seccion seccion)
    {
        var ordenOrigen = deOrigen.ToDictionary(p => p.Id, p => p.Orden, StringComparer.Ordinal);
        int OrdenEnOrigen(Parrafo p) => ordenOrigen.GetValueOrDefault(p.OrigenParrafoId ?? "", int.MaxValue);

        var lista = trabajo.Where(p => p.Seccion == seccion && !reubicar.Contains(p)).OrderBy(p => p.Orden).ToList();
        foreach (var movil in reubicar.Where(p => p.Seccion == seccion).OrderBy(OrdenEnOrigen))
        {
            var posicion = 0;
            var orden = OrdenEnOrigen(movil);
            foreach (var previo in deOrigen.Where(p => p.Seccion == seccion && p.Orden < orden).OrderByDescending(p => p.Orden))
            {
                var indice = lista.FindLastIndex(p => p.OrigenParrafoId == previo.Id);
                if (indice >= 0)
                {
                    posicion = indice + 1;
                    break;
                }
            }
            lista.Insert(posicion, movil);
        }

        for (var i = 0; i < lista.Count; i++)
            lista[i].Orden = i + 1;
    }

    /// <summary>Lo que se guarda de un párrafo; si no cambió, no se reescribe.</summary>
    private static string Huella(Parrafo p) => JsonSerializer.Serialize(
        new { p.Seccion, p.Orden, p.Contenido, p.Activo, p.MostrarSoloSiCumple, p.Condicion, p.CamposUsados, p.OrigenParrafoId },
        OpcionesJson.Predeterminadas);

    private static bool MismoJson<T>(T a, T b) =>
        JsonSerializer.Serialize(a, OpcionesJson.Predeterminadas) == JsonSerializer.Serialize(b, OpcionesJson.Predeterminadas);

    private static T Clonar<T>(T entidad) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(entidad, OpcionesJson.Predeterminadas), OpcionesJson.Predeterminadas)!;
}
