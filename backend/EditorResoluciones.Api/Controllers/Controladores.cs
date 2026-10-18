using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EditorResoluciones.Api.Controllers;

[Route("api/rubros")]
[CatalogoDeAdministrador]
public sealed class RubrosController(ServicioEntidades<Rubro> servicio) : ControladorCrud<Rubro>(servicio);

/// <summary>Filtro: <c>?rubroId=</c>.</summary>
[Route("api/subrubros")]
[CatalogoDeAdministrador]
public sealed class SubRubrosController(ServicioEntidades<SubRubro> servicio) : ControladorCrud<SubRubro>(servicio)
{
    protected override IEnumerable<SubRubro> Filtrar(IEnumerable<SubRubro> registros, IQueryCollection consulta) =>
        PorParametro(registros, consulta, "rubroId", (s, v) => s.RubroId == v);
}

/// <summary>Lista "Documento": Original, Modificación o Aclaratoria.</summary>
[Route("api/clases-documento")]
[CatalogoDeAdministrador]
public sealed class ClasesDocumentoController(ServicioEntidades<ClaseDocumento> servicio) : ControladorCrud<ClaseDocumento>(servicio);

/// <summary>Tipos de documento con sus bloques (<c>bloques</c>: código, orden y si es obligatorio), decisión D2.</summary>
[Route("api/tipos-documento")]
[CatalogoDeAdministrador]
public sealed class TiposDocumentoController(ServicioEntidades<TipoDocumento> servicio, CatalogoBloques catalogo)
    : ControladorCrud<TipoDocumento>(servicio)
{
    /// <summary>Bloques del tipo, con los datos del catálogo y en su orden (los cinco originales si no tiene configuración).</summary>
    [HttpGet("{id}/bloques")]
    public async Task<ActionResult<IReadOnlyList<BloqueConfigurado>>> Bloques(string id, CancellationToken ct)
    {
        await Servicio.ObtenerAsync(id, ct);
        return Ok(await catalogo.DeTipoAsync(id, ct));
    }
}

/// <summary>Catálogo de bloques documentales (CABECERA, VISTOS, CONSIDERANDO, FUNDAMENTACION_LEGAL…).</summary>
[Route("api/bloques")]
[CatalogoDeAdministrador]
public sealed class BloquesController(ServicioEntidades<BloqueDocumental> servicio) : ControladorCrud<BloqueDocumental>(servicio);

/// <summary>
/// Identidad de las plantillas (Rubro, Sub Rubro, Tipo Documento y Documento): solo lectura. Las plantillas se crean y
/// se modifican dentro de un Documento Consolidado (decisión D11). Filtros:
/// <c>?rubroId=&amp;subRubroId=&amp;tipoDocumentoId=&amp;claseDocumentoId=</c>.
/// </summary>
[Route("api/plantillas")]
[EdicionDirectaCerrada]
public sealed class PlantillasController(ServicioEntidades<Plantilla> servicio) : ControladorCrud<Plantilla>(servicio)
{
    protected override IEnumerable<Plantilla> Filtrar(IEnumerable<Plantilla> registros, IQueryCollection consulta)
    {
        registros = PorParametro(registros, consulta, "rubroId", (p, v) => p.RubroId == v);
        registros = PorParametro(registros, consulta, "subRubroId", (p, v) => p.SubRubroId == v);
        registros = PorParametro(registros, consulta, "tipoDocumentoId", (p, v) => p.TipoDocumentoId == v);
        return PorParametro(registros, consulta, "claseDocumentoId", (p, v) => p.ClaseDocumentoId == v);
    }
}

/// <summary>
/// Párrafos de la colección anterior a la Fase 3: solo lectura (decisión D11). El contenido vigente está en las
/// versiones publicadas; se edita dentro de una propuesta. Filtros: <c>?plantillaId=&amp;seccion=</c>.
/// </summary>
[Route("api/parrafos")]
[EdicionDirectaCerrada]
public sealed class ParrafosController(ServicioEntidades<Parrafo> servicio) : ControladorCrud<Parrafo>(servicio)
{
    protected override IEnumerable<Parrafo> Filtrar(IEnumerable<Parrafo> registros, IQueryCollection consulta)
    {
        registros = PorParametro(registros, consulta, "plantillaId", (p, v) => p.PlantillaId == v);
        registros = PorParametro(registros, consulta, "seccion",
            (p, v) => string.Equals(p.Seccion.ToString(), v, StringComparison.OrdinalIgnoreCase));
        return registros.OrderBy(p => p.Seccion).ThenBy(p => p.Orden);
    }
}

[Route("api/campos")]
[CatalogoDeAdministrador]
public sealed class CamposController(ServicioEntidades<Campo> servicio, ServicioValoresCampo valores)
    : ControladorCrud<Campo>(servicio)
{
    /// <summary>Operadores de comparación permitidos para cada tipo de dato.</summary>
    [HttpGet("operadores")]
    public ActionResult<IReadOnlyDictionary<TipoDato, OperadorComparacion[]>> Operadores() =>
        Ok(ReglasCondiciones.OperadoresPorTipo);

    /// <summary>Valores distintos del campo en los datos de prueba (sugerencias para las condiciones).</summary>
    [HttpGet("{id}/valores")]
    public async Task<ActionResult<IEnumerable<string>>> Valores(string id, CancellationToken ct) =>
        Ok(await valores.ValoresAsync(id, ct));
}

[Route("api/solicitudes")]
public sealed class SolicitudesController(ServicioEntidades<Solicitud> servicio) : ControladorCrud<Solicitud>(servicio);

/// <summary>Filtro: <c>?solicitudId=</c>.</summary>
[Route("api/detalles-bienes")]
public sealed class DetallesBienesController(ServicioEntidades<DetalleBien> servicio) : ControladorCrud<DetalleBien>(servicio)
{
    protected override IEnumerable<DetalleBien> Filtrar(IEnumerable<DetalleBien> registros, IQueryCollection consulta) =>
        PorParametro(registros, consulta, "solicitudId", (b, v) => b.SolicitudId == v);
}

/// <summary>Contenido de los archivos cargados (por ejemplo, el logo), para mostrarlos en la interfaz.</summary>
[ApiController]
[Route("api/archivos")]
public sealed class ArchivosController(ServicioLogos logos) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(string id, CancellationToken ct)
    {
        var (datos, contenido) = await logos.LeerAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(contenido, datos.TipoContenido);
    }
}

/// <summary>
/// Historial técnico de cambios (antes y después de cada registro): solo lectura y solo para el administrador, porque
/// incluye el contenido de todos los documentos. Filtros: <c>?coleccion=&amp;entidadId=</c>.
/// </summary>
[ApiController]
[Route("api/historial")]
public sealed class HistorialController(IRepositorio<RegistroHistorial> repositorio, ServicioAutorizacion autorizacion) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegistroHistorial>>> Listar(
        [FromQuery] string? coleccion,
        [FromQuery] string? entidadId,
        CancellationToken ct)
    {
        await autorizacion.ExigirAsync(Rol.Administrador, ct);
        IEnumerable<RegistroHistorial> registros = await repositorio.ListarAsync(ct);
        if (!string.IsNullOrEmpty(coleccion))
            registros = registros.Where(r => r.Coleccion == coleccion);
        if (!string.IsNullOrEmpty(entidadId))
            registros = registros.Where(r => r.EntidadId == entidadId);
        return Ok(registros.OrderByDescending(r => r.Fecha).ToList());
    }
}

/// <summary>Diagnóstico del almacenamiento de prueba.</summary>
[ApiController]
[Route("api/salud")]
public sealed class SaludController(IDiagnosticoAlmacenamiento diagnostico) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Obtener(CancellationToken ct)
    {
        var colecciones = await diagnostico.VerificarAsync(ct);
        return Ok(new
        {
            estado = colecciones.All(c => c.Disponible) ? "correcto" : "con errores",
            almacenamiento = "Archivos JSON (datos de prueba, sin base de datos)",
            colecciones,
        });
    }
}

/// <summary>
/// Rechaza las escrituras: las plantillas vigentes no se editan directamente (decisión D11). Toda creación o
/// modificación pasa por un Documento Consolidado y su aprobación. Es un filtro de recurso: se aplica antes de leer y
/// validar el cuerpo de la petición.
/// </summary>
public sealed class EdicionDirectaCerradaAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuted(ResourceExecutedContext contexto)
    {
    }

    public void OnResourceExecuting(ResourceExecutingContext contexto)
    {
        if (HttpMethods.IsGet(contexto.HttpContext.Request.Method))
            return;
        contexto.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Las plantillas vigentes no se editan directamente.",
            Detail = "Cree o modifique la plantilla dentro de un Documento Consolidado; los cambios se publican al aprobarse.",
        }) { StatusCode = StatusCodes.Status409Conflict };
    }
}

/// <summary>
/// Los catálogos (rubros, tipos de documento, bloques, campos…) se consultan libremente, pero solo el administrador los
/// modifica (sección 11 de la especificación). Se valida en el servidor, antes de leer el cuerpo de la petición.
/// </summary>
public sealed class CatalogoDeAdministradorAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext contexto, ResourceExecutionDelegate siguiente)
    {
        if (!HttpMethods.IsGet(contexto.HttpContext.Request.Method))
            await contexto.HttpContext.RequestServices.GetRequiredService<ServicioAutorizacion>().ExigirAsync(Rol.Administrador);
        await siguiente();
    }
}
