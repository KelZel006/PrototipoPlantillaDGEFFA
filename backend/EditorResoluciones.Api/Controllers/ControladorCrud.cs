using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace EditorResoluciones.Api.Controllers;

/// <summary>
/// Endpoints comunes de consulta y modificación. Las colecciones derivadas solo definen su ruta
/// y, si lo necesitan, los filtros por parámetros de consulta.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ControladorCrud<T>(ServicioEntidades<T> servicio) : ControllerBase where T : class, IEntidad
{
    protected ServicioEntidades<T> Servicio => servicio;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<T>>> Listar(CancellationToken ct)
    {
        var registros = await servicio.ListarAsync(ct);
        return Ok(Filtrar(registros, Request.Query).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<T>> Obtener(string id, CancellationToken ct) =>
        Ok(await servicio.ObtenerAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<T>> Crear([FromBody] T entidad, CancellationToken ct)
    {
        var creada = await servicio.CrearAsync(entidad, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<T>> Actualizar(string id, [FromBody] T entidad, CancellationToken ct) =>
        Ok(await servicio.ActualizarAsync(id, entidad, ct));

    /// <summary>Elimina el registro. Se recomienda enviar <c>?version=n</c> para no borrar una versión que otro modificó.</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(string id, [FromQuery] int? version, CancellationToken ct)
    {
        await servicio.EliminarAsync(id, version, ct);
        return NoContent();
    }

    /// <summary>Filtros opcionales por parámetros de consulta.</summary>
    protected virtual IEnumerable<T> Filtrar(IEnumerable<T> registros, IQueryCollection consulta) => registros;

    /// <summary>Aplica el filtro solo si el parámetro viene en la consulta.</summary>
    protected static IEnumerable<T> PorParametro(
        IEnumerable<T> registros,
        IQueryCollection consulta,
        string parametro,
        Func<T, string, bool> coincide)
    {
        var valor = consulta[parametro].ToString();
        return string.IsNullOrEmpty(valor) ? registros : registros.Where(r => coincide(r, valor));
    }
}
