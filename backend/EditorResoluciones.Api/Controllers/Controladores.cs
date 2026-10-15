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

[Route("api/tipos-documento")]
public sealed class TiposDocumentoController(ServicioEntidades<TipoDocumento> servicio) : ControladorCrud<TipoDocumento>(servicio);

/// <summary>Filtros: <c>?rubroId=&amp;subRubroId=&amp;tipoDocumentoId=</c>.</summary>
[Route("api/plantillas")]
public sealed class PlantillasController(
    ServicioEntidades<Plantilla> servicio,
    ServicioDocumento documentos,
    IGeneradorPdf generadorPdf) : ControladorCrud<Plantilla>(servicio)
{
    /// <summary>
    /// PDF de prueba con Cabecera, Considerando y Resuelve, en ese orden. Con <c>?solicitudId=</c> se sustituyen
    /// los campos y se aplican las condiciones; sin ella, los campos se muestran como {Etiqueta}.
    /// </summary>
    [HttpGet("{id}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(string id, [FromQuery] string? solicitudId, CancellationToken ct)
    {
        var documento = await documentos.GenerarAsync(id, solicitudId, ct);
        var bytes = generadorPdf.Generar(documento);
        var sufijo = string.IsNullOrEmpty(solicitudId) ? "plantilla" : solicitudId;
        return File(bytes, "application/pdf", $"resolucion-prueba_{id}_{sufijo}.pdf");
    }

    protected override IEnumerable<Plantilla> Filtrar(IEnumerable<Plantilla> registros, IQueryCollection consulta)
    {
        registros = PorParametro(registros, consulta, "rubroId", (p, v) => p.RubroId == v);
        registros = PorParametro(registros, consulta, "subRubroId", (p, v) => p.SubRubroId == v);
        return PorParametro(registros, consulta, "tipoDocumentoId", (p, v) => p.TipoDocumentoId == v);
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
