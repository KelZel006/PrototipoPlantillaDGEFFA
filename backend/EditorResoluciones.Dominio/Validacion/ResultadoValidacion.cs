using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Validacion;

/// <summary>Acumula errores de validación por campo (nombres en camelCase, como los recibe el cliente).</summary>
public sealed class ResultadoValidacion
{
    private readonly Dictionary<string, List<string>> _errores = new(StringComparer.Ordinal);

    public bool EsValido => _errores.Count == 0;

    public IReadOnlyDictionary<string, string[]> Errores =>
        _errores.ToDictionary(par => par.Key, par => par.Value.ToArray(), StringComparer.Ordinal);

    public void Agregar(string campo, string mensaje)
    {
        if (!_errores.TryGetValue(campo, out var lista))
        {
            lista = [];
            _errores[campo] = lista;
        }
        if (!lista.Contains(mensaje))
        {
            lista.Add(mensaje);
        }
    }

    public void LanzarSiHayErrores()
    {
        if (!EsValido)
        {
            throw new ValidacionException(Errores);
        }
    }
}

/// <summary>Reglas propias de una colección que se revisan en el servidor antes de guardar o eliminar.</summary>
public interface IValidadorEntidad<in T> where T : class, IEntidad
{
    Task ValidarGuardadoAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct);

    Task ValidarEliminacionAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct);
}

/// <summary>
/// Completa o corrige datos que calcula el servidor antes de validar y guardar
/// (por ejemplo, los campos usados en un párrafo o su posición al final de la sección).
/// </summary>
/// <summary>
/// Regla que decide si un registro se puede crear, modificar o eliminar en este momento (no si sus datos son válidos).
/// Por ejemplo, la Certificación no se edita directamente porque se copia de la Resolución.
/// </summary>
public interface IReglaModificacion<in T> where T : class, IEntidad
{
    /// <summary><paramref name="entidad"/> es el registro a guardar o, al eliminar, el registro actual.</summary>
    Task ValidarModificacionAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct);
}

public interface INormalizadorEntidad<in T> where T : class, IEntidad
{
    Task NormalizarAsync(T entidad, CancellationToken ct);
}

public abstract class ValidadorEntidad<T> : IValidadorEntidad<T> where T : class, IEntidad
{
    public virtual Task ValidarGuardadoAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct) => Task.CompletedTask;

    public virtual Task ValidarEliminacionAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct) => Task.CompletedTask;
}
