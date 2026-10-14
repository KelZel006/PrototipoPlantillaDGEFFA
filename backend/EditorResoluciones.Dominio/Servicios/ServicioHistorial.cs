using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Servicios;

public interface IServicioHistorial
{
    Task RegistrarAsync(
        string coleccion,
        string entidadId,
        AccionHistorial accion,
        int versionEntidad,
        object? anterior,
        object? nuevo,
        CancellationToken ct);
}

/// <summary>Guarda cada creación, actualización y eliminación con el estado anterior y el nuevo.</summary>
public sealed class ServicioHistorial(
    IRepositorio<RegistroHistorial> repositorio,
    IUsuarioActual usuario,
    TimeProvider reloj) : IServicioHistorial
{
    public async Task RegistrarAsync(
        string coleccion,
        string entidadId,
        AccionHistorial accion,
        int versionEntidad,
        object? anterior,
        object? nuevo,
        CancellationToken ct)
    {
        var registro = new RegistroHistorial
        {
            Fecha = reloj.GetUtcNow(),
            Usuario = usuario.Nombre,
            Coleccion = coleccion,
            EntidadId = entidadId,
            Accion = accion,
            VersionEntidad = versionEntidad,
            Anterior = Serializar(anterior),
            Nuevo = Serializar(nuevo),
        };
        await repositorio.CrearAsync(registro, ct);
    }

    private static JsonElement? Serializar(object? valor) =>
        valor is null ? null : JsonSerializer.SerializeToElement(valor, valor.GetType(), OpcionesJson.Predeterminadas);
}
