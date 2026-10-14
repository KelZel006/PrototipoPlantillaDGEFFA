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

public abstract class ValidadorEntidad<T> : IValidadorEntidad<T> where T : class, IEntidad
{
    public virtual Task ValidarGuardadoAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct) => Task.CompletedTask;

    public virtual Task ValidarEliminacionAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct) => Task.CompletedTask;
}
