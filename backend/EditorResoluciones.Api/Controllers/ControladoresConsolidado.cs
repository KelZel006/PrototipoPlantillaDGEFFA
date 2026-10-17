using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace EditorResoluciones.Api.Controllers;

/// <summary>
/// Documentos Consolidados. El número PLAN lo genera el servidor; las reglas de visibilidad y los permisos se validan
/// en el dominio (<see cref="ServicioConsolidados"/>).
/// </summary>
[ApiController]
[Route("api/consolidados")]
public sealed class ConsolidadosController(ServicioConsolidados servicio, ServicioPropuestas propuestas, ServicioRevision revision)
    : ControllerBase
{
    /// <summary>Envía a verificación (EN REGISTRO → VERIFICADO): solo el analista dueño.</summary>
    [HttpPost("{id}/enviar")]
    public async Task<ActionResult<DocumentoConsolidado>> Enviar(string id, CancellationToken ct) =>
        Ok(await revision.EnviarAsync(id, ct));

    /// <summary>Devuelve a registro (VERIFICADO → EN REGISTRO) con justificación: solo el Oficial Jurídico.</summary>
    [HttpPost("{id}/devolver")]
    public async Task<ActionResult<Devolucion>> Devolver(string id, [FromBody] SolicitudDevolucion solicitud, CancellationToken ct) =>
        Ok(await revision.DevolverAsync(id, solicitud, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ResumenConsolidado>>> Listar(CancellationToken ct) =>
        Ok(await servicio.ListarAsync(ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<DetalleConsolidado>> Obtener(string id, CancellationToken ct) =>
        Ok(await servicio.ObtenerAsync(id, ct));

    /// <summary>Crea un consolidado EN REGISTRO. No recibe datos: número, año, analista y fechas los asigna el servidor.</summary>
    [HttpPost]
    public async Task<ActionResult<DocumentoConsolidado>> Crear(CancellationToken ct)
    {
        var creado = await servicio.CrearAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    /// <summary>Operación CREAR. Si es una Resolución, también se agrega su Certificación (D3). Devuelve las propuestas agregadas.</summary>
    [HttpPost("{id}/propuestas/crear")]
    public async Task<ActionResult<IReadOnlyList<Propuesta>>> AgregarCrear(string id, [FromBody] SolicitudCrearPlantilla solicitud, CancellationToken ct) =>
        Ok(await propuestas.AgregarCrearAsync(id, solicitud, ct));

    /// <summary>Operación MODIFICAR sobre una plantilla vigente. Si es una Resolución, también se agrega su Certificación (D3).</summary>
    [HttpPost("{id}/propuestas/modificar")]
    public async Task<ActionResult<IReadOnlyList<Propuesta>>> AgregarModificar(string id, [FromBody] SolicitudModificarPlantilla solicitud, CancellationToken ct) =>
        Ok(await propuestas.AgregarModificarAsync(id, solicitud, ct));
}

/// <summary>
/// Propuestas de un Documento Consolidado: consulta, edición del contenido con el editor, vista previa en PDF y retiro.
/// Solo el analista dueño edita, mientras el consolidado está EN REGISTRO.
/// </summary>
[ApiController]
[Route("api/propuestas/{id}")]
public sealed class PropuestasController(
    ServicioPropuestas servicio,
    CatalogoBloques catalogoBloques,
    ServicioLogos logos,
    ServicioDocumento documentos,
    IGeneradorPdf generadorPdf) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Propuesta>> Obtener(string id, CancellationToken ct) => Ok(await servicio.ObtenerAsync(id, ct));

    /// <summary>Identidad, diseño y si la propuesta se puede editar, con la forma que usa el editor.</summary>
    [HttpGet("edicion")]
    public async Task<ActionResult<PlantillaEdicion>> Edicion(string id, CancellationToken ct) => Ok(await servicio.EdicionAsync(id, ct));

    [HttpGet("ajustes")]
    public async Task<ActionResult<IReadOnlyList<Ajuste>>> Ajustes(string id, CancellationToken ct) => Ok(await servicio.AjustesAsync(id, ct));

    [HttpPost("retirar")]
    public async Task<ActionResult<IReadOnlyList<Propuesta>>> Retirar(string id, [FromBody] SolicitudRetiro solicitud, CancellationToken ct) =>
        Ok(await servicio.RetirarAsync(id, solicitud, ct));

    /// <summary>Filtro opcional: <c>?bloque=CONSIDERANDO</c>.</summary>
    [HttpGet("parrafos")]
    public async Task<ActionResult<IReadOnlyList<ParrafoPropuesta>>> Parrafos(string id, [FromQuery] string? bloque, CancellationToken ct) =>
        Ok(await servicio.ParrafosAsync(id, bloque, ct));

    /// <summary>Comparación con la versión original: bloques, párrafos con diferencias palabra por palabra y diseño.</summary>
    [HttpGet("comparacion")]
    public async Task<ActionResult<ComparacionPropuesta>> Comparacion(string id, CancellationToken ct) =>
        Ok(await servicio.ComparacionAsync(id, ct));

    [HttpPost("parrafos")]
    public async Task<ActionResult<ParrafoPropuesta>> CrearParrafo(string id, [FromBody] DatosParrafo datos, CancellationToken ct) =>
        Ok(await servicio.CrearParrafoAsync(id, datos, ct));

    [HttpPut("parrafos/{parrafoId}")]
    public async Task<ActionResult<ParrafoPropuesta>> ActualizarParrafo(string id, string parrafoId, [FromBody] DatosParrafo datos, CancellationToken ct) =>
        Ok(await servicio.ActualizarParrafoAsync(id, parrafoId, datos, ct));

    /// <summary>Se recomienda enviar <c>?version=n</c> para no borrar un párrafo que cambió en otra pestaña.</summary>
    [HttpDelete("parrafos/{parrafoId}")]
    public async Task<IActionResult> EliminarParrafo(string id, string parrafoId, [FromQuery] int? version, CancellationToken ct)
    {
        await servicio.EliminarParrafoAsync(id, parrafoId, version, ct);
        return NoContent();
    }

    [HttpPost("parrafos/{parrafoId}/duplicar")]
    public async Task<ActionResult<ParrafoPropuesta>> DuplicarParrafo(string id, string parrafoId, CancellationToken ct) =>
        Ok(await servicio.DuplicarParrafoAsync(id, parrafoId, ct));

    [HttpPost("parrafos/reordenar")]
    public async Task<ActionResult<IReadOnlyList<ParrafoPropuesta>>> Reordenar(string id, [FromBody] SolicitudReordenPropuesta solicitud, CancellationToken ct) =>
        Ok(await servicio.ReordenarAsync(id, solicitud, ct));

    [HttpPut("diseno")]
    public async Task<ActionResult<PlantillaEdicion>> Diseno(string id, [FromBody] DatosDiseno datos, CancellationToken ct) =>
        Ok(await servicio.ActualizarDisenoAsync(id, datos, ct));

    /// <summary>Carga o reemplaza el logo del encabezado (PNG o JPEG, hasta 1 MB).</summary>
    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<PlantillaEdicion>> CargarLogo(string id, IFormFile archivo, CancellationToken ct)
    {
        if (archivo.Length > ServicioLogos.TamanoMaximo)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["archivo"] = ["El logo no puede superar 1 MB."],
            }) { Title = "Los datos enviados no son válidos." });

        // Antes de guardar el archivo se comprueba que la propuesta exista y que este usuario la pueda editar.
        if (!(await servicio.EdicionAsync(id, ct)).Editable)
            throw new ConflictoException("La propuesta no se puede editar.");
        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        var nuevo = await logos.GuardarAsync(archivo.FileName, memoria.ToArray(), ct);
        string? anterior;
        try
        {
            anterior = await servicio.CambiarLogoAsync(id, nuevo.Id, ct);
        }
        catch
        {
            await logos.EliminarSiNoSeUsaAsync(nuevo.Id, CancellationToken.None);
            throw;
        }
        await logos.EliminarSiNoSeUsaAsync(anterior, ct);
        return Ok(await servicio.EdicionAsync(id, ct));
    }

    [HttpDelete("logo")]
    public async Task<ActionResult<PlantillaEdicion>> QuitarLogo(string id, CancellationToken ct)
    {
        var anterior = await servicio.CambiarLogoAsync(id, null, ct);
        await logos.EliminarSiNoSeUsaAsync(anterior, ct);
        return Ok(await servicio.EdicionAsync(id, ct));
    }

    /// <summary>Vista previa en PDF de la propuesta, con los mismos parámetros que el PDF de una versión.</summary>
    [HttpGet("pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(string id, [FromQuery] string? solicitudId, [FromQuery] string? interlineado, CancellationToken ct)
    {
        var propuesta = await servicio.ObtenerAsync(id, ct);
        var configurados = await catalogoBloques.DeContenidoAsync(propuesta.TipoDocumentoId, propuesta.ContenidoPropuesto, ct);
        var (plantilla, parrafos) = ConstructorContenido.ADocumento(propuesta.ContenidoPropuesto, propuesta.Id, propuesta.Nombre,
            propuesta.RubroId, propuesta.SubRubroId, propuesta.TipoDocumentoId, propuesta.ClaseDocumentoId, configurados);
        var documento = await documentos.GenerarAsync(plantilla, parrafos, solicitudId, Interlineados.Interpretar(interlineado), ct);
        var sufijo = string.IsNullOrEmpty(solicitudId) ? "plantilla" : solicitudId;
        return File(generadorPdf.Generar(documento), "application/pdf", $"propuesta_{id}_{sufijo}.pdf");
    }
}

/// <summary>Catálogo de plantillas vigentes para la operación MODIFICAR (indica si ya están en un consolidado abierto).</summary>
[ApiController]
[Route("api/plantillas-vigentes")]
public sealed class PlantillasVigentesController(ServicioPropuestas servicio) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlantillaVigente>>> Listar(CancellationToken ct) => Ok(await servicio.VigentesAsync(ct));
}

/// <summary>Versiones publicadas de las plantillas: solo lectura. Filtros: <c>?plantillaId=&amp;vigencia=</c>.</summary>
[ApiController]
[Route("api/versiones-plantilla")]
public sealed class VersionesPlantillaController(
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<Plantilla> plantillas,
    CatalogoBloques catalogoBloques,
    ServicioAutorizacion autorizacion,
    ServicioDocumento documentos,
    IGeneradorPdf generadorPdf) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VersionPlantilla>>> Listar(
        [FromQuery] string? plantillaId,
        [FromQuery] Vigencia? vigencia,
        CancellationToken ct)
    {
        IEnumerable<VersionPlantilla> lista = await versiones.ListarAsync(ct);
        if (!string.IsNullOrEmpty(plantillaId))
            lista = lista.Where(v => v.PlantillaId == plantillaId);
        if (vigencia is not null)
            lista = lista.Where(v => v.Vigencia == vigencia);
        return Ok(lista.OrderBy(v => v.PlantillaId).ThenBy(v => v.NumeroVersion).ToList());
    }

    /// <summary>PDF de una versión publicada (vigente o no): consulta de solo lectura (D11).</summary>
    [HttpGet("{id}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(string id, [FromQuery] string? solicitudId, [FromQuery] string? interlineado, CancellationToken ct)
    {
        await autorizacion.UsuarioAsync(ct);
        var version = await versiones.ObtenerAsync(id, ct) ?? throw new EntidadNoEncontradaException(versiones.Coleccion, id);
        var identidad = await plantillas.ObtenerAsync(version.PlantillaId, ct)
            ?? throw new EntidadNoEncontradaException(plantillas.Coleccion, version.PlantillaId);
        var configurados = await catalogoBloques.DeContenidoAsync(identidad.TipoDocumentoId, version.Contenido, ct);
        var (plantilla, parrafos) = ConstructorContenido.ADocumento(version.Contenido, identidad.Id, identidad.Nombre,
            identidad.RubroId, identidad.SubRubroId, identidad.TipoDocumentoId, identidad.ClaseDocumentoId, configurados);
        var documento = await documentos.GenerarAsync(plantilla, parrafos, solicitudId, Interlineados.Interpretar(interlineado), ct);
        return File(generadorPdf.Generar(documento), "application/pdf", $"{identidad.Id}_v{version.NumeroVersion}.pdf");
    }
}

/// <summary>Usuarios de prueba y sus roles, para el selector «Usuario de prueba» del prototipo. Solo lectura.</summary>
[ApiController]
[Route("api/usuarios")]
public sealed class UsuariosController(IRepositorio<Usuario> usuarios) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Usuario>>> Listar(CancellationToken ct) =>
        Ok((await usuarios.ListarAsync(ct)).Where(u => u.Activo).OrderBy(u => u.Id).ToList());
}
