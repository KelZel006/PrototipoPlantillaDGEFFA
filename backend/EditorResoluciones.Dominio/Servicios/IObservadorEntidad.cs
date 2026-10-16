using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Reacciona después de guardar una entidad (por ejemplo, para copiar la Resolución a la Certificación).
/// <paramref name="anterior"/> es null al crear y <paramref name="actual"/> es null al eliminar.
/// </summary>
public interface IObservadorEntidad<in T> where T : class, IEntidad
{
    Task DespuesDeGuardarAsync(T? anterior, T? actual, CancellationToken ct);
}
