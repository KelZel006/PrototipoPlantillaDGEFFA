using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>Un cambio a registrar: el estado anterior y el nuevo (null en creación o eliminación).</summary>
public sealed record CambioHistorial(
    string Coleccion,
    string EntidadId,
    AccionHistorial Accion,
    int VersionEntidad,
    object? Anterior,
    object? Nuevo);

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

    /// <summary>Registra todos los cambios de una misma operación en una sola escritura.</summary>
    Task RegistrarAsync(IReadOnlyList<CambioHistorial> cambios, CancellationToken ct);
}

/// <summary>Guarda cada creación, actualización y eliminación con el estado anterior y el nuevo.</summary>
public sealed class ServicioHistorial(
    IRepositorio<RegistroHistorial> repositorio,
    IUsuarioActual usuario,
    TimeProvider reloj) : IServicioHistorial
{
    public Task RegistrarAsync(
        string coleccion,
        string entidadId,
        AccionHistorial accion,
        int versionEntidad,
        object? anterior,
        object? nuevo,
        CancellationToken ct) =>
        RegistrarAsync([new CambioHistorial(coleccion, entidadId, accion, versionEntidad, anterior, nuevo)], ct);

    public async Task RegistrarAsync(IReadOnlyList<CambioHistorial> cambios, CancellationToken ct)
    {
        if (cambios.Count == 0)
            return;

        var fecha = reloj.GetUtcNow();
        var registros = cambios.Select(cambio => Construir(cambio, usuario.Nombre, fecha)).ToList();
        await repositorio.AplicarLoteAsync(registros, [], ct);
    }

    /// <summary>Registro de historial de un cambio (también se usa dentro de las transacciones de varias colecciones).</summary>
    public static RegistroHistorial Construir(CambioHistorial cambio, string usuario, DateTimeOffset fecha) => new()
    {
        Fecha = fecha,
        Usuario = usuario,
        Coleccion = cambio.Coleccion,
        EntidadId = cambio.EntidadId,
        Accion = cambio.Accion,
        VersionEntidad = cambio.VersionEntidad,
        Anterior = Serializar(cambio.Anterior),
        Nuevo = Serializar(cambio.Nuevo),
    };

    private static JsonElement? Serializar(object? valor) =>
        valor is null ? null : JsonSerializer.SerializeToElement(valor, valor.GetType(), OpcionesJson.Predeterminadas);
}
