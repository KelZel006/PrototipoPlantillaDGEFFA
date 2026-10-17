using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Resultado de la aprobación: el consolidado aprobado y las versiones publicadas.</summary>
public sealed record ResultadoAprobacion(DocumentoConsolidado Consolidado, IReadOnlyList<VersionPlantilla> Publicadas);

/// <summary>Versión de una plantilla en el consolidado de plantillas (pantalla F).</summary>
public sealed record VersionResumen(
    string Id,
    int NumeroVersion,
    Vigencia Vigencia,
    bool CargaInicial,
    string PublicadaPor,
    DateTimeOffset FechaPublicacion,
    DateTimeOffset InicioVigencia,
    DateTimeOffset? FinVigencia,
    string? ConsolidadoId,
    string? ConsolidadoNumero,
    string? VersionAnteriorId);

/// <summary>Plantilla del consolidado de plantillas con todas sus versiones, de la más reciente a la más antigua.</summary>
public sealed record PlantillaConVersiones(
    string PlantillaId,
    string Nombre,
    string RubroId,
    string SubRubroId,
    string TipoDocumentoId,
    string TipoDocumento,
    string ClaseDocumentoId,
    IReadOnlyList<VersionResumen> Versiones);

/// <summary>
/// Aprobación y publicación del Documento Consolidado (sección 9 de la especificación). Todo se aplica en una sola
/// transacción: si algo falla, no queda nada publicado a medias y el documento sigue VERIFICADO.
/// <list type="number">
/// <item>Valida que el documento esté VERIFICADO, que lo apruebe un Oficial Jurídico que no lo preparó ni lo envió, y
/// que nada cambió desde el envío (huella de integridad de la última presentación).</item>
/// <item>MODIFICAR: la versión original debe seguir vigente y sin cambios; pasa a NO VIGENTE y la propuesta se publica
/// como versión nueva VIGENTE, ligada a la anterior y al consolidado.</item>
/// <item>CREAR: no debe existir otra plantilla con la misma identidad; se crea la plantilla y su versión 1 VIGENTE.</item>
/// <item>Como máximo una versión vigente por plantilla y una plantilla por identidad (regla 14).</item>
/// <item>El documento pasa a APROBADO con el aprobador y la fecha. Un error queda en la bitácora.</item>
/// </list>
/// </summary>
public sealed class ServicioPublicacion(
    IUnidadDeTrabajo unidad,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<DocumentoConsolidado> consolidados,
    IRepositorio<TipoDocumento> tipos,
    ServicioAutorizacion autorizacion,
    TimeProvider reloj)
{
    public async Task<ResultadoAprobacion> AprobarAsync(string consolidadoId, CancellationToken ct = default)
    {
        var oficial = await autorizacion.ExigirAsync(Rol.OficialJuridico, ct);
        var ahora = reloj.GetUtcNow();
        try
        {
            return await unidad.EjecutarAsync(tx => PublicarAsync(tx, consolidadoId, oficial, ahora, ct), ct);
        }
        catch (Exception error) when (error is ConflictoException or AlmacenamientoException)
        {
            // La publicación no se aplicó: el error queda documentado y el documento sigue VERIFICADO.
            await RegistrarErrorAsync(consolidadoId, oficial, ahora, error.Message, ct);
            throw;
        }
    }

    private async Task<ResultadoAprobacion> PublicarAsync(ITransaccion tx, string consolidadoId, Usuario oficial, DateTimeOffset ahora, CancellationToken ct)
    {
        var consolidado = (await tx.ListarAsync<DocumentoConsolidado>(ct)).FirstOrDefault(c => c.Id == consolidadoId);
        if (consolidado is null || !ServicioConsolidados.PuedeVer(oficial, consolidado))
            throw new EntidadNoEncontradaException("consolidados", consolidadoId);
        if (consolidado.Estado != EstadoConsolidado.Verificado)
            throw new ConflictoException($"Solo se aprueba un documento VERIFICADO; {consolidado.Numero} no lo está.");
        ServicioRevision.ExigirSeparacion(oficial, consolidado);

        var activas = (await tx.ListarAsync<Propuesta>(ct)).Where(p => p.ConsolidadoId == consolidado.Id && !p.Retirada).ToList();
        var presentacion = (await tx.ListarAsync<Presentacion>(ct))
            .Where(p => p.ConsolidadoId == consolidado.Id).MaxBy(p => p.Numero)
            ?? throw new ConflictoException("El documento no tiene una presentación registrada.");
        if (activas.Count == 0)
            throw new ConflictoException("El documento no tiene plantillas para publicar.");
        if (HuellaContenido.De(activas) != presentacion.HuellaIntegridad)
            throw new ConflictoException("El contenido cambió desde que se envió a verificación. No se publicó nada.");

        var todasPlantillas = (await tx.ListarAsync<Plantilla>(ct)).ToList();
        var todasVersiones = (await tx.ListarAsync<VersionPlantilla>(ct)).ToList();
        var publicadas = new List<VersionPlantilla>();
        var registro = new List<CambioHistorial>();
        var movimientos = new List<string>();

        foreach (var propuesta in activas.OrderBy(p => p.DerivadaDePropuestaId is null ? 0 : 1))
        {
            var contenido = FusionDerivada.Clonar(propuesta.ContenidoPropuesto);
            foreach (var parrafo in contenido.Bloques.SelectMany(b => b.Parrafos))
                parrafo.Version = 1;

            VersionPlantilla? anterior = null;
            Plantilla plantilla;
            if (propuesta.Operacion == OperacionPropuesta.Modificar)
            {
                plantilla = todasPlantillas.FirstOrDefault(p => p.Id == propuesta.PlantillaId)
                    ?? throw new ConflictoException($"La plantilla de «{propuesta.Nombre}» ya no existe.");
                var vigentes = todasVersiones.Where(v => v.PlantillaId == plantilla.Id && v.Vigencia == Vigencia.Vigente).ToList();
                if (vigentes.Count > 1)
                    throw new ConflictoException($"«{plantilla.Nombre}» tiene {vigentes.Count} versiones vigentes. Resuelva la inconsistencia antes de publicar.");
                anterior = vigentes.SingleOrDefault();
                if (anterior is null || anterior.Id != propuesta.VersionOrigenId || anterior.Huella != propuesta.HuellaOrigen)
                    throw new ConflictoException($"La versión vigente de «{plantilla.Nombre}» cambió desde que se preparó la propuesta. No se publicó nada.");

                var antes = FusionDerivada.Clonar(anterior);
                anterior.Vigencia = Vigencia.NoVigente;
                anterior.FinVigencia = ahora;
                await tx.ActualizarAsync(anterior, ct);
                registro.Add(new CambioHistorial("versionesPlantilla", anterior.Id, AccionHistorial.Actualizacion, anterior.Version, antes, anterior));
            }
            else
            {
                var repetida = todasPlantillas.FirstOrDefault(p => p.RubroId == propuesta.RubroId && p.SubRubroId == propuesta.SubRubroId &&
                    p.TipoDocumentoId == propuesta.TipoDocumentoId && p.ClaseDocumentoId == propuesta.ClaseDocumentoId);
                if (repetida is not null)
                    throw new ConflictoException($"Ya existe una plantilla vigente con la identidad de «{propuesta.Nombre}» ({repetida.Id}). No se publicó nada.");
                plantilla = await tx.CrearAsync(new Plantilla
                {
                    Nombre = propuesta.Nombre,
                    RubroId = propuesta.RubroId,
                    SubRubroId = propuesta.SubRubroId,
                    TipoDocumentoId = propuesta.TipoDocumentoId,
                    ClaseDocumentoId = propuesta.ClaseDocumentoId,
                    Estado = EstadoPlantilla.Publicada,
                    Descripcion = $"Creada por {consolidado.Numero}.",
                    LogoId = contenido.Diseno.LogoId,
                    AlineacionLogo = contenido.Diseno.AlineacionLogo,
                    PiePagina = contenido.Diseno.PiePagina,
                    EspacioFirmasCm = contenido.Diseno.EspacioFirmasCm,
                    FechaModificacion = ahora,
                }, ct);
                todasPlantillas.Add(plantilla);
                registro.Add(new CambioHistorial("plantillas", plantilla.Id, AccionHistorial.Creacion, plantilla.Version, null, plantilla));
            }

            var nueva = await tx.CrearAsync(new VersionPlantilla
            {
                PlantillaId = plantilla.Id,
                NumeroVersion = (anterior?.NumeroVersion ?? todasVersiones.Where(v => v.PlantillaId == plantilla.Id).Select(v => v.NumeroVersion).DefaultIfEmpty(0).Max()) + 1,
                Vigencia = Vigencia.Vigente,
                Contenido = contenido,
                Huella = HuellaContenido.De(contenido),
                VersionAnteriorId = anterior?.Id,
                ConsolidadoId = consolidado.Id,
                PublicadaPor = oficial.Id,
                FechaPublicacion = ahora,
                InicioVigencia = ahora,
            }, ct);
            todasVersiones.Add(nueva);
            publicadas.Add(nueva);
            registro.Add(new CambioHistorial("versionesPlantilla", nueva.Id, AccionHistorial.Creacion, nueva.Version, null, nueva));
            movimientos.Add(anterior is null
                ? $"Publicada «{plantilla.Nombre}» ({plantilla.Id}), versión {nueva.NumeroVersion}: plantilla nueva, VIGENTE."
                : $"Publicada «{plantilla.Nombre}» ({plantilla.Id}), versión {nueva.NumeroVersion} VIGENTE; la versión {anterior.NumeroVersion} pasa a NO VIGENTE.");
        }

        // Regla 14, de nuevo dentro de la transacción.
        var inconsistencias = MigracionCargaInicial.Inconsistencias(todasPlantillas, todasVersiones);
        if (inconsistencias.Count > 0)
            throw new ConflictoException($"La publicación dejaría datos inconsistentes: {string.Join(" ", inconsistencias)}");

        var antesConsolidado = FusionDerivada.Clonar(consolidado);
        consolidado.Estado = EstadoConsolidado.Aprobado;
        consolidado.RevisorId = oficial.Id;
        consolidado.FechaRevision = ahora;
        consolidado.AprobadorId = oficial.Id;
        consolidado.FechaAprobacion = ahora;
        consolidado.FechaActualizacion = ahora;
        await tx.ActualizarAsync(consolidado, ct);
        registro.Add(new CambioHistorial("consolidados", consolidado.Id, AccionHistorial.Actualizacion, consolidado.Version, antesConsolidado, consolidado));

        await tx.CrearAsync(new Movimiento
        {
            ConsolidadoId = consolidado.Id,
            Tipo = TipoMovimiento.Aprobacion,
            Usuario = oficial.Id,
            Fecha = ahora,
            EstadoOrigen = EstadoConsolidado.Verificado,
            EstadoDestino = EstadoConsolidado.Aprobado,
            NumeroPresentacion = presentacion.Numero,
            Detalle = $"Aprobación final de la {ServicioRevision.Ordinal(presentacion.Numero).ToLowerInvariant()} presentación.",
        }, ct);
        foreach (var detalle in movimientos)
        {
            await tx.CrearAsync(new Movimiento
            {
                ConsolidadoId = consolidado.Id,
                Tipo = detalle.Contains("NO VIGENTE") ? TipoMovimiento.CambioVigencia : TipoMovimiento.Publicacion,
                Usuario = oficial.Id,
                Fecha = ahora,
                NumeroPresentacion = presentacion.Numero,
                Detalle = detalle,
            }, ct);
        }
        foreach (var cambio in registro)
            await tx.CrearAsync(ServicioHistorial.Construir(cambio, oficial.Id, ahora), ct);
        return new ResultadoAprobacion(consolidado, publicadas);
    }

    /// <summary>Deja en la bitácora un intento de aprobación que no se pudo publicar.</summary>
    private async Task RegistrarErrorAsync(string consolidadoId, Usuario oficial, DateTimeOffset ahora, string mensaje, CancellationToken ct)
    {
        try
        {
            await unidad.EjecutarAsync(async tx =>
            {
                if ((await tx.ListarAsync<DocumentoConsolidado>(ct)).All(c => c.Id != consolidadoId))
                    return false;
                await tx.CrearAsync(new Movimiento
                {
                    ConsolidadoId = consolidadoId,
                    Tipo = TipoMovimiento.ErrorPublicacion,
                    Usuario = oficial.Id,
                    Fecha = ahora,
                    Detalle = $"No se publicó: {mensaje}",
                }, ct);
                return true;
            }, ct);
        }
        catch (AlmacenamientoException)
        {
            // Si tampoco se puede escribir la bitácora, prevalece el error original.
        }
    }

    /// <summary>Consolidado de plantillas (pantalla F): cada plantilla con su historia de versiones.</summary>
    public async Task<IReadOnlyList<PlantillaConVersiones>> ConsolidadoDePlantillasAsync(CancellationToken ct = default)
    {
        await autorizacion.UsuarioAsync(ct);
        var nombresTipo = (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre, StringComparer.Ordinal);
        var numeros = (await consolidados.ListarAsync(ct)).ToDictionary(c => c.Id, c => c.Numero, StringComparer.Ordinal);
        var porPlantilla = (await versiones.ListarAsync(ct)).ToLookup(v => v.PlantillaId, StringComparer.Ordinal);
        return (await plantillas.ListarAsync(ct))
            .Where(p => porPlantilla[p.Id].Any())
            .Select(p => new PlantillaConVersiones(p.Id, p.Nombre, p.RubroId, p.SubRubroId, p.TipoDocumentoId,
                nombresTipo.GetValueOrDefault(p.TipoDocumentoId, p.TipoDocumentoId), p.ClaseDocumentoId,
                porPlantilla[p.Id].OrderByDescending(v => v.NumeroVersion).Select(v => new VersionResumen(
                    v.Id, v.NumeroVersion, v.Vigencia, v.CargaInicial, v.PublicadaPor, v.FechaPublicacion, v.InicioVigencia, v.FinVigencia,
                    v.ConsolidadoId, v.ConsolidadoId is null ? null : numeros.GetValueOrDefault(v.ConsolidadoId), v.VersionAnteriorId)).ToList()))
            .OrderBy(p => p.TipoDocumento, StringComparer.CurrentCulture).ThenBy(p => p.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }
}
