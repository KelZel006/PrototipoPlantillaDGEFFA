using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Operaciones de consulta y modificación comunes a todas las colecciones:
/// valida en el servidor, controla versiones, asigna marcas de tiempo y registra el historial.
/// </summary>
public partial class ServicioEntidades<T>(
    IRepositorio<T> repositorio,
    IEnumerable<IValidadorEntidad<T>> validadores,
    IEnumerable<INormalizadorEntidad<T>> normalizadores,
    IEnumerable<IObservadorEntidad<T>> observadores,
    IServicioHistorial historial,
    TimeProvider reloj) where T : class, IEntidad
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdentificadorValido();

    public string Coleccion => repositorio.Coleccion;

    public Task<IReadOnlyList<T>> ListarAsync(CancellationToken ct = default) => repositorio.ListarAsync(ct);

    public async Task<T> ObtenerAsync(string id, CancellationToken ct = default) =>
        await repositorio.ObtenerAsync(id, ct) ?? throw new EntidadNoEncontradaException(Coleccion, id);

    public async Task<T> CrearAsync(T entidad, CancellationToken ct = default)
    {
        var resultado = new ResultadoValidacion();
        if (!string.IsNullOrEmpty(entidad.Id) && !IdentificadorValido().IsMatch(entidad.Id))
            resultado.Agregar("id", "El identificador solo admite letras, números, guion y guion bajo (máximo 64).");
        await NormalizarAsync(entidad, ct);
        await ValidarGuardadoAsync(entidad, resultado, ct);
        resultado.LanzarSiHayErrores();

        MarcarTiempo(entidad);
        var creada = await repositorio.CrearAsync(entidad, ct);
        await historial.RegistrarAsync(Coleccion, creada.Id, AccionHistorial.Creacion, creada.Version, null, creada, ct);
        await NotificarAsync(null, creada, ct);
        return creada;
    }

    public async Task<T> ActualizarAsync(string id, T entidad, CancellationToken ct = default)
    {
        entidad.Id = id;
        var anterior = await ObtenerAsync(id, ct);
        if (anterior.Version != entidad.Version)
            throw new ConflictoException(MensajeVersion(anterior.Version, entidad.Version));

        await NormalizarAsync(entidad, ct);
        var resultado = new ResultadoValidacion();
        await ValidarGuardadoAsync(entidad, resultado, ct);
        resultado.LanzarSiHayErrores();

        MarcarTiempo(entidad);
        var actualizada = await repositorio.ActualizarAsync(entidad, ct);
        await historial.RegistrarAsync(Coleccion, id, AccionHistorial.Actualizacion, actualizada.Version, anterior, actualizada, ct);
        await NotificarAsync(anterior, actualizada, ct);
        return actualizada;
    }

    public async Task EliminarAsync(string id, int? version, CancellationToken ct = default)
    {
        var actual = await ObtenerAsync(id, ct);
        if (version is not null && version != actual.Version)
            throw new ConflictoException(MensajeVersion(actual.Version, version.Value));

        var resultado = new ResultadoValidacion();
        foreach (var validador in validadores)
            await validador.ValidarEliminacionAsync(actual, resultado, ct);
        resultado.LanzarSiHayErrores();

        await repositorio.EliminarAsync(id, version, ct);
        await historial.RegistrarAsync(Coleccion, id, AccionHistorial.Eliminacion, actual.Version, actual, null, ct);
        await NotificarAsync(actual, null, ct);
    }

    private async Task NotificarAsync(T? anterior, T? actual, CancellationToken ct)
    {
        foreach (var observador in observadores)
            await observador.DespuesDeGuardarAsync(anterior, actual, ct);
    }

    public static string MensajeVersion(int guardada, int recibida) =>
        $"El registro fue modificado por otra operación (versión guardada {guardada}, versión recibida {recibida}). Recargue los datos antes de guardar.";

    private async Task NormalizarAsync(T entidad, CancellationToken ct)
    {
        foreach (var normalizador in normalizadores)
            await normalizador.NormalizarAsync(entidad, ct);
    }

    private async Task ValidarGuardadoAsync(T entidad, ResultadoValidacion resultado, CancellationToken ct)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(entidad, new ValidationContext(entidad), errores, validateAllProperties: true);
        foreach (var error in errores)
        {
            var miembro = error.MemberNames.FirstOrDefault() ?? "";
            resultado.Agregar(JsonNamingPolicy.CamelCase.ConvertName(miembro), error.ErrorMessage ?? "Valor no válido.");
        }

        // Las reglas de cada colección suponen datos básicos completos; si faltan, basta con informar esos errores.
        if (!resultado.EsValido)
            return;

        foreach (var validador in validadores)
            await validador.ValidarGuardadoAsync(entidad, resultado, ct);
    }

    private void MarcarTiempo(T entidad)
    {
        if (entidad is IConMarcaTiempo conMarca)
            conMarca.FechaModificacion = reloj.GetUtcNow();
    }
}
