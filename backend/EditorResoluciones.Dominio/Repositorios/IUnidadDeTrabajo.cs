using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Repositorios;

/// <summary>
/// Ejecuta operaciones que escriben en varias colecciones como una sola unidad: se guardan todas o ninguna.
/// Con archivos JSON se implementa con un diario de transacción; con una base de datos será una transacción del motor.
/// </summary>
public interface IUnidadDeTrabajo
{
    /// <summary>
    /// Ejecuta la operación. Si lanza una excepción, no se guarda nada. Las transacciones se ejecutan de a una.
    /// </summary>
    Task<TResultado> EjecutarAsync<TResultado>(Func<ITransaccion, Task<TResultado>> operacion, CancellationToken ct = default);
}

/// <summary>Acceso a las colecciones dentro de una transacción. Los cambios se guardan al terminar la operación.</summary>
public interface ITransaccion
{
    /// <summary>Registros de la colección dentro de la transacción (incluye lo creado o actualizado en ella).</summary>
    Task<IReadOnlyList<T>> ListarAsync<T>(CancellationToken ct = default) where T : class, IEntidad;

    /// <summary>Agrega el registro. Si no trae identificador, se genera uno. Deja la versión en 1.</summary>
    Task<T> CrearAsync<T>(T entidad, CancellationToken ct = default) where T : class, IEntidad;

    /// <summary>Reemplaza el registro si su versión coincide con la guardada, e incrementa la versión.</summary>
    Task<T> ActualizarAsync<T>(T entidad, CancellationToken ct = default) where T : class, IEntidad;
}
