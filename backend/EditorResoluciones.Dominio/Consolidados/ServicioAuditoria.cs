using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Presentación sin el contenido congelado (para los informes).</summary>
public sealed record PresentacionResumen(int Numero, string Usuario, DateTimeOffset Fecha, int CantidadPlantillas, string HuellaIntegridad);

/// <summary>
/// Trazabilidad de una versión publicada (sección 10 de la especificación): qué texto estaba vigente antes, qué texto se
/// propuso y publicó, qué documento consolidado lo originó, quién lo preparó, lo envió, lo revisó y lo aprobó, y todas
/// sus presentaciones y devoluciones.
/// </summary>
public sealed record Trazabilidad(
    VersionPlantilla Version,
    VersionPlantilla? Anterior,
    string? ConsolidadoId,
    string? ConsolidadoNumero,
    string? Analista,
    string? EnviadoPor,
    string? Aprobador,
    DateTimeOffset? FechaAprobacion,
    IReadOnlyList<PresentacionResumen> Presentaciones,
    IReadOnlyList<Devolucion> Devoluciones,
    IReadOnlyList<Ajuste> Ajustes);

/// <summary>Consultas de auditoría. Son de solo lectura y las consultan el administrador y el Oficial Jurídico.</summary>
public sealed class ServicioAuditoria(
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<DocumentoConsolidado> consolidados,
    IRepositorio<Presentacion> presentaciones,
    IRepositorio<Devolucion> devoluciones,
    ServicioAutorizacion autorizacion)
{
    public async Task<Trazabilidad> TrazabilidadAsync(string versionId, CancellationToken ct = default)
    {
        var usuario = await autorizacion.UsuarioAsync(ct);
        if (!usuario.Tiene(Rol.Administrador) && !usuario.Tiene(Rol.OficialJuridico))
            throw new AccesoDenegadoException("La trazabilidad la consultan el administrador y el Oficial Jurídico.");

        var version = await versiones.ObtenerAsync(versionId, ct) ?? throw new EntidadNoEncontradaException(versiones.Coleccion, versionId);
        var anterior = version.VersionAnteriorId is { } idAnterior ? await versiones.ObtenerAsync(idAnterior, ct) : null;
        var consolidado = version.ConsolidadoId is { } idConsolidado ? await consolidados.ObtenerAsync(idConsolidado, ct) : null;
        var deConsolidado = consolidado is null
            ? []
            : (await presentaciones.ListarAsync(ct)).Where(p => p.ConsolidadoId == consolidado.Id).OrderBy(p => p.Numero)
                .Select(p => new PresentacionResumen(p.Numero, p.Usuario, p.Fecha, p.CantidadPlantillas, p.HuellaIntegridad)).ToList();
        var devueltas = consolidado is null
            ? []
            : (await devoluciones.ListarAsync(ct)).Where(d => d.ConsolidadoId == consolidado.Id).OrderBy(d => d.Fecha).ToList();

        return new Trazabilidad(
            version,
            anterior,
            consolidado?.Id,
            consolidado?.Numero,
            consolidado?.AnalistaId,
            consolidado?.EnviadoPor,
            consolidado?.AprobadorId,
            consolidado?.FechaAprobacion,
            deConsolidado,
            devueltas,
            ComparadorContenido.Ajustes(anterior?.Contenido ?? new ContenidoPlantilla(), version.Contenido));
    }
}
