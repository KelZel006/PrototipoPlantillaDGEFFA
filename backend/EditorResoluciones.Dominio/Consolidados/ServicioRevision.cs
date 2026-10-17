using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Observación puntual que envía el Oficial Jurídico al devolver.</summary>
public sealed record DatosObservacion(string PropuestaId, string? BloqueCodigo, string? Problema, string? Recomendacion);

/// <summary>Devolución a registro: justificación general obligatoria y observaciones opcionales por plantilla y bloque.</summary>
public sealed record SolicitudDevolucion(string? Justificacion, List<DatosObservacion>? Observaciones);

/// <summary>
/// Flujo de revisión del Documento Consolidado:
/// <list type="bullet">
/// <item><b>Enviar a verificación</b> (analista dueño): EN REGISTRO → VERIFICADO. Congela lo enviado en una presentación
/// con su huella de integridad. El documento sale de la bandeja del analista (D6).</item>
/// <item><b>Devolver a registro</b> (Oficial Jurídico): VERIFICADO → EN REGISTRO, con justificación obligatoria y
/// observaciones. Las devoluciones se agregan; nunca se sobrescriben.</item>
/// </list>
/// La aprobación (VERIFICADO → APROBADO) es de la Fase 6.
/// </summary>
public sealed class ServicioRevision(
    IUnidadDeTrabajo unidad,
    IRepositorio<VersionPlantilla> versiones,
    CatalogoBloques catalogoBloques,
    IRepositorio<TipoDocumento> tipos,
    ServicioAutorizacion autorizacion,
    TimeProvider reloj)
{
    public const int MinimoJustificacion = 10;
    public const int MaximoTexto = 2000;

    public async Task<DocumentoConsolidado> EnviarAsync(string consolidadoId, CancellationToken ct = default)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
        var ahora = reloj.GetUtcNow();
        var nombresTipo = (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre, StringComparer.Ordinal);
        var vigentes = (await versiones.ListarAsync(ct)).Where(v => v.Vigencia == Vigencia.Vigente)
            .ToDictionary(v => v.PlantillaId, v => v.Id, StringComparer.Ordinal);

        return await unidad.EjecutarAsync(async tx =>
        {
            var consolidado = (await tx.ListarAsync<DocumentoConsolidado>(ct)).FirstOrDefault(c => c.Id == consolidadoId);
            if (consolidado is null || consolidado.AnalistaId != analista.Id || !ServicioConsolidados.PuedeVer(analista, consolidado))
                throw new EntidadNoEncontradaException("consolidados", consolidadoId);
            if (consolidado.Estado != EstadoConsolidado.EnRegistro)
                throw new ConflictoException($"El documento {consolidado.Numero} no está EN REGISTRO.");

            var activas = (await tx.ListarAsync<Propuesta>(ct)).Where(p => p.ConsolidadoId == consolidado.Id && !p.Retirada).ToList();
            var resultado = new ResultadoValidacion();
            if (activas.Count == 0)
                resultado.Agregar("propuestas", "El documento no tiene plantillas: agregue al menos una antes de enviarlo.");
            foreach (var propuesta in activas)
            {
                var configurados = await catalogoBloques.DeContenidoAsync(propuesta.TipoDocumentoId, propuesta.ContenidoPropuesto, ct);
                var vacios = CatalogoBloques.ObligatoriosVacios(configurados, propuesta.ContenidoPropuesto);
                if (vacios.Count > 0)
                    resultado.Agregar("bloques", $"«{propuesta.Nombre}»: los bloques obligatorios {string.Join(", ", vacios)} no tienen párrafos activos.");
                // La versión original debe seguir siendo la vigente (otra aprobación pudo publicar una nueva).
                if (propuesta is { Operacion: OperacionPropuesta.Modificar, PlantillaId: { } plantillaId } &&
                    vigentes.GetValueOrDefault(plantillaId) != propuesta.VersionOrigenId)
                {
                    resultado.Agregar("propuestas", $"«{propuesta.Nombre}»: la versión vigente cambió desde que se agregó. Retírela y vuelva a agregarla.");
                }
            }
            resultado.LanzarSiHayErrores();

            var anterior = FusionDerivada.Clonar(consolidado);
            consolidado.NumeroPresentacion++;
            consolidado.Estado = EstadoConsolidado.Verificado;
            consolidado.FechaEnvio = ahora;
            consolidado.EnviadoPor = analista.Id;
            consolidado.FechaActualizacion = ahora;
            await tx.ActualizarAsync(consolidado, ct);

            var enviadas = activas.Select(FusionDerivada.Clonar).ToList();
            var presentacion = await tx.CrearAsync(new Presentacion
            {
                ConsolidadoId = consolidado.Id,
                Numero = consolidado.NumeroPresentacion,
                Usuario = analista.Id,
                Fecha = ahora,
                CantidadPlantillas = enviadas.Count,
                HuellaIntegridad = HuellaContenido.De(enviadas),
                Propuestas = enviadas,
            }, ct);

            var tiposEnviados = enviadas.Select(p => nombresTipo.GetValueOrDefault(p.TipoDocumentoId, p.TipoDocumentoId))
                .Distinct().Order(StringComparer.CurrentCulture);
            await tx.CrearAsync(new Movimiento
            {
                ConsolidadoId = consolidado.Id,
                Tipo = TipoMovimiento.EnvioVerificacion,
                Usuario = analista.Id,
                Fecha = ahora,
                EstadoOrigen = EstadoConsolidado.EnRegistro,
                EstadoDestino = EstadoConsolidado.Verificado,
                NumeroPresentacion = presentacion.Numero,
                Detalle = $"{Ordinal(presentacion.Numero)} presentación: {enviadas.Count} " +
                          $"{(enviadas.Count == 1 ? "plantilla" : "plantillas")} ({string.Join(", ", tiposEnviados)}).",
            }, ct);
            await tx.CrearAsync(ServicioHistorial.Construir(
                new CambioHistorial("consolidados", consolidado.Id, AccionHistorial.Actualizacion, consolidado.Version, anterior, consolidado),
                analista.Id, ahora), ct);
            return consolidado;
        }, ct);
    }

    public async Task<Devolucion> DevolverAsync(string consolidadoId, SolicitudDevolucion solicitud, CancellationToken ct = default)
    {
        var oficial = await autorizacion.ExigirAsync(Rol.OficialJuridico, ct);
        var ahora = reloj.GetUtcNow();
        var justificacion = solicitud.Justificacion?.Trim() ?? "";
        var observaciones = solicitud.Observaciones ?? [];

        return await unidad.EjecutarAsync(async tx =>
        {
            var consolidado = (await tx.ListarAsync<DocumentoConsolidado>(ct)).FirstOrDefault(c => c.Id == consolidadoId);
            if (consolidado is null || !ServicioConsolidados.PuedeVer(oficial, consolidado))
                throw new EntidadNoEncontradaException("consolidados", consolidadoId);
            if (consolidado.Estado != EstadoConsolidado.Verificado)
                throw new ConflictoException($"Solo se devuelve un documento VERIFICADO; {consolidado.Numero} no lo está.");
            ExigirSeparacion(oficial, consolidado);

            var delConsolidado = (await tx.ListarAsync<Propuesta>(ct))
                .Where(p => p.ConsolidadoId == consolidado.Id && !p.Retirada)
                .ToDictionary(p => p.Id, StringComparer.Ordinal);
            var resultado = new ResultadoValidacion();
            if (justificacion.Length < MinimoJustificacion || justificacion.Length > MaximoTexto)
                resultado.Agregar("justificacion", $"La justificación es obligatoria (de {MinimoJustificacion} a {MaximoTexto} caracteres).");
            for (var i = 0; i < observaciones.Count; i++)
            {
                var o = observaciones[i];
                if (!delConsolidado.TryGetValue(o.PropuestaId ?? "", out var propuesta))
                {
                    resultado.Agregar($"observaciones[{i}].propuestaId", "La plantilla observada no está en este documento.");
                    continue;
                }
                if (!string.IsNullOrEmpty(o.BloqueCodigo) && propuesta.ContenidoPropuesto.Bloques.All(b => b.Codigo != o.BloqueCodigo))
                    resultado.Agregar($"observaciones[{i}].bloqueCodigo", $"El bloque '{o.BloqueCodigo}' no está en «{propuesta.Nombre}».");
                if (string.IsNullOrWhiteSpace(o.Problema) || o.Problema.Length > MaximoTexto)
                    resultado.Agregar($"observaciones[{i}].problema", "Describa el problema (hasta 2000 caracteres).");
                if (o.Recomendacion is { Length: > MaximoTexto })
                    resultado.Agregar($"observaciones[{i}].recomendacion", "La recomendación admite hasta 2000 caracteres.");
            }
            resultado.LanzarSiHayErrores();

            var anterior = FusionDerivada.Clonar(consolidado);
            consolidado.Estado = EstadoConsolidado.EnRegistro;
            consolidado.RevisorId = oficial.Id;
            consolidado.FechaRevision = ahora;
            consolidado.FechaActualizacion = ahora;
            await tx.ActualizarAsync(consolidado, ct);

            var numeroDevolucion = (await tx.ListarAsync<Devolucion>(ct)).Count(d => d.ConsolidadoId == consolidado.Id) + 1;
            var devolucion = await tx.CrearAsync(new Devolucion
            {
                ConsolidadoId = consolidado.Id,
                NumeroPresentacion = consolidado.NumeroPresentacion,
                Usuario = oficial.Id,
                Fecha = ahora,
                Justificacion = justificacion,
                Observaciones = observaciones.Select(o => new Observacion
                {
                    PropuestaId = o.PropuestaId,
                    BloqueCodigo = string.IsNullOrWhiteSpace(o.BloqueCodigo) ? null : o.BloqueCodigo,
                    Problema = o.Problema!.Trim(),
                    Recomendacion = string.IsNullOrWhiteSpace(o.Recomendacion) ? null : o.Recomendacion.Trim(),
                }).ToList(),
            }, ct);
            await tx.CrearAsync(new Movimiento
            {
                ConsolidadoId = consolidado.Id,
                Tipo = TipoMovimiento.Devolucion,
                Usuario = oficial.Id,
                Fecha = ahora,
                EstadoOrigen = EstadoConsolidado.Verificado,
                EstadoDestino = EstadoConsolidado.EnRegistro,
                NumeroPresentacion = consolidado.NumeroPresentacion,
                Detalle = $"{Ordinal(numeroDevolucion)} devolución ({devolucion.Id}): {Resumir(justificacion)}",
            }, ct);
            await tx.CrearAsync(ServicioHistorial.Construir(
                new CambioHistorial("consolidados", consolidado.Id, AccionHistorial.Actualizacion, consolidado.Version, anterior, consolidado),
                oficial.Id, ahora), ct);
            return devolucion;
        }, ct);
    }

    /// <summary>
    /// Separación de funciones: quien preparó o envió el documento no lo revisa, aunque tenga también el rol de Oficial
    /// Jurídico.
    /// </summary>
    public static void ExigirSeparacion(Usuario revisor, DocumentoConsolidado consolidado)
    {
        if (revisor.Id == consolidado.AnalistaId || revisor.Id == consolidado.EnviadoPor)
            throw new AccesoDenegadoException("Quien preparó o envió el documento no puede revisarlo.");
    }

    /// <summary>Primera, Segunda, Tercera… (para la bitácora).</summary>
    public static string Ordinal(int numero) => numero switch
    {
        1 => "Primera",
        2 => "Segunda",
        3 => "Tercera",
        4 => "Cuarta",
        5 => "Quinta",
        6 => "Sexta",
        7 => "Séptima",
        8 => "Octava",
        9 => "Novena",
        10 => "Décima",
        _ => $"{numero}.ª",
    };

    private static string Resumir(string texto) => texto.Length > 160 ? texto[..160] + "…" : texto;
}
