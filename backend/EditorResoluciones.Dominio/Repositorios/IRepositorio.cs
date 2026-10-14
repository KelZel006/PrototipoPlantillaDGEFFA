using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Repositorios;

/// <summary>
/// Acceso a una colección de registros. En esta etapa la implementa un almacenamiento JSON;
/// más adelante puede implementarlo una base de datos sin cambiar el dominio ni la API.
/// </summary>
public interface IRepositorio<T> where T : class, IEntidad
{
    /// <summary>Nombre lógico de la colección (por ejemplo, "parrafos").</summary>
    string Coleccion { get; }

    Task<IReadOnlyList<T>> ListarAsync(CancellationToken ct = default);

    Task<T?> ObtenerAsync(string id, CancellationToken ct = default);

    /// <summary>Agrega el registro. Si no trae identificador, se genera uno. Deja la versión en 1.</summary>
    Task<T> CrearAsync(T entidad, CancellationToken ct = default);

    /// <summary>Reemplaza el registro si su versión coincide con la guardada, e incrementa la versión.</summary>
    Task<T> ActualizarAsync(T entidad, CancellationToken ct = default);

    /// <summary>Elimina el registro. Si se indica versión, debe coincidir con la guardada.</summary>
    Task EliminarAsync(string id, int? version, CancellationToken ct = default);

    /// <summary>
    /// Crea y actualiza varios registros en una sola operación: o se aplican todos o ninguno.
    /// Cada actualización exige que su versión coincida con la guardada.
    /// </summary>
    Task<LoteAplicado<T>> AplicarLoteAsync(IReadOnlyList<T> crear, IReadOnlyList<T> actualizar, CancellationToken ct = default);
}

/// <summary>Resultado de <see cref="IRepositorio{T}.AplicarLoteAsync"/>: los registros como quedaron guardados.</summary>
public sealed record LoteAplicado<T>(IReadOnlyList<T> Creados, IReadOnlyList<T> Actualizados);

/// <summary>Estado de lectura de una colección, para el diagnóstico del almacenamiento.</summary>
public sealed record EstadoColeccion(string Nombre, bool Disponible, int Registros, string? Error);

/// <summary>Diagnóstico del almacenamiento (qué colecciones se pueden leer).</summary>
public interface IDiagnosticoAlmacenamiento
{
    Task<IReadOnlyList<EstadoColeccion>> VerificarAsync(CancellationToken ct = default);
}
