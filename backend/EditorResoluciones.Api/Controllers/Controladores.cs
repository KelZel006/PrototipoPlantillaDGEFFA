using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace EditorResoluciones.Api.Controllers;

[Route("api/rubros")]
public sealed class RubrosController(ServicioEntidades<Rubro> servicio) : ControladorCrud<Rubro>(servicio);

/// <summary>Filtro: <c>?rubroId=</c>.</summary>
[Route("api/subrubros")]
public sealed class SubRubrosController(ServicioEntidades<SubRubro> servicio) : ControladorCrud<SubRubro>(servicio)
{
    protected override IEnumerable<SubRubro> Filtrar(IEnumerable<SubRubro> registros, IQueryCollection consulta) =>
        PorParametro(registros, consulta, "rubroId", (s, v) => s.RubroId == v);
}

/// <summary>Lista "Documento": Original, Modificación o Aclaratoria.</summary>
[Route("api/clases-documento")]
public sealed class ClasesDocumentoController(ServicioEntidades<ClaseDocumento> servicio) : ControladorCrud<ClaseDocumento>(servicio);

[Route("api/tipos-documento")]
public sealed class TiposDocumentoController(ServicioEntidades<TipoDocumento> servicio) : ControladorCrud<TipoDocumento>(servicio);

/// <summary>Filtros: <c>?rubroId=&amp;subRubroId=&amp;tipoDocumentoId=&amp;claseDocumentoId=</c>.</summary>
[Route("api/plantillas")]
public sealed class PlantillasController(
    ServicioEntidades<Plantilla> servicio,
    ServicioDocumento documentos,
    IGeneradorPdf generadorPdf,
    ServicioLogos logos) : ControladorCrud<Plantilla>(servicio)
{
    /// <summary>
    /// PDF de prueba con Cabecera, Considerando y Resuelve, en ese orden. Con <c>?solicitudId=</c> se sustituyen
    /// los campos y se aplican las condiciones; sin ella, los campos se muestran como {Etiqueta}.
    /// </summary>
    [HttpGet("{id}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(
        string id,
        [FromQuery] string? solicitudId,
        [FromQuery] string? interlineado,
        CancellationToken ct)
    {
        var documento = await documentos.GenerarAsync(id, solicitudId, Interlineados.Interpretar(interlineado), ct);
        var bytes = generadorPdf.Generar(documento);
        var sufijo = string.IsNullOrEmpty(solicitudId) ? "plantilla" : solicitudId;
        return File(bytes, "application/pdf", $"resolucion-prueba_{id}_{sufijo}.pdf");
    }

    /// <summary>Carga o reemplaza el logo del encabezado (PNG o JPEG, hasta 1 MB).</summary>
    [HttpPost("{id}/logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<Plantilla>> CargarLogo(string id, IFormFile archivo, CancellationToken ct)
    {
        if (archivo.Length > ServicioLogos.TamanoMaximo)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["archivo"] = ["El logo no puede superar 1 MB."],
            }) { Title = "Los datos enviados no son válidos." });

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        return Ok(await logos.AsignarAsync(id, archivo.FileName, memoria.ToArray(), ct));
    }

    [HttpDelete("{id}/logo")]
    public async Task<ActionResult<Plantilla>> QuitarLogo(string id, CancellationToken ct) =>
        Ok(await logos.QuitarAsync(id, ct));

    protected override IEnumerable<Plantilla> Filtrar(IEnumerable<Plantilla> registros, IQueryCollection consulta)
    {
        registros = PorParametro(registros, consulta, "rubroId", (p, v) => p.RubroId == v);
        registros = PorParametro(registros, consulta, "subRubroId", (p, v) => p.SubRubroId == v);
        registros = PorParametro(registros, consulta, "tipoDocumentoId", (p, v) => p.TipoDocumentoId == v);
        return PorParametro(registros, consulta, "claseDocumentoId", (p, v) => p.ClaseDocumentoId == v);
    }
}

/// <summary>Filtros: <c>?plantillaId=&amp;seccion=</c>. Se devuelven ordenados por sección y orden.</summary>
[Route("api/parrafos")]
public sealed class ParrafosController(ServicioEntidades<Parrafo> servicio, ServicioParrafos parrafos)
    : ControladorCrud<Parrafo>(servicio)
{
    protected override IEnumerable<Parrafo> Filtrar(IEnumerable<Parrafo> registros, IQueryCollection consulta)
    {
        registros = PorParametro(registros, consulta, "plantillaId", (p, v) => p.PlantillaId == v);
        registros = PorParametro(registros, consulta, "seccion",
            (p, v) => string.Equals(p.Seccion.ToString(), v, StringComparison.OrdinalIgnoreCase));
        return registros.OrderBy(p => p.Seccion).ThenBy(p => p.Orden);
    }

    /// <summary>Aplica un nuevo orden a todos los párrafos de una sección, en una sola operación.</summary>
    [HttpPost("reordenar")]
    public async Task<ActionResult<IEnumerable<Parrafo>>> Reordenar([FromBody] SolicitudReorden solicitud, CancellationToken ct) =>
        Ok(await parrafos.ReordenarAsync(solicitud, ct));

    /// <summary>Copia el párrafo (con su condición) justo debajo del original.</summary>
    [HttpPost("{id}/duplicar")]
    public async Task<ActionResult<Parrafo>> Duplicar(string id, CancellationToken ct)
    {
        var copia = await parrafos.DuplicarAsync(id, ct);
        return CreatedAtAction(nameof(Obtener), new { id = copia.Id }, copia);
    }
}

[Route("api/campos")]
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

/// <summary>Historial de cambios: solo lectura. Filtros: <c>?coleccion=&amp;entidadId=</c>.</summary>
[ApiController]
[Route("api/historial")]
public sealed class HistorialController(IRepositorio<RegistroHistorial> repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RegistroHistorial>>> Listar(
        [FromQuery] string? coleccion,
        [FromQuery] string? entidadId,
        CancellationToken ct)
    {
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
