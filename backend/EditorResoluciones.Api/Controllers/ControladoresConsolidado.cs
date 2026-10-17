using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.AspNetCore.Mvc;

namespace EditorResoluciones.Api.Controllers;

/// <summary>
/// Documentos Consolidados. El número PLAN lo genera el servidor; las reglas de visibilidad y los permisos se validan
/// en el dominio (<see cref="ServicioConsolidados"/>).
/// </summary>
[ApiController]
[Route("api/consolidados")]
public sealed class ConsolidadosController(ServicioConsolidados servicio) : ControllerBase
{
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
}

/// <summary>Versiones publicadas de las plantillas: solo lectura. Filtros: <c>?plantillaId=&amp;vigencia=</c>.</summary>
[ApiController]
[Route("api/versiones-plantilla")]
public sealed class VersionesPlantillaController(IRepositorio<VersionPlantilla> versiones) : ControllerBase
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
