using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Api.Configuracion;

public static class RegistroDominio
{
    public static IServiceCollection AgregarDominio(this IServiceCollection servicios)
    {
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddHttpContextAccessor();
        servicios.AddSingleton<IUsuarioActual, UsuarioPrueba>();
        servicios.AddScoped<ServicioAutorizacion>();
        servicios.AddSingleton(proveedor => new ZonaHorariaInstitucional(TimeZoneInfo.FindSystemTimeZoneById(
            proveedor.GetRequiredService<IConfiguration>()["Consolidado:ZonaHoraria"] ?? "America/Tegucigalpa")));
        servicios.AddScoped<ServicioConsolidados>();
        servicios.AddScoped<ServicioPropuestas>();
        servicios.AddScoped<CatalogoBloques>();
        servicios.AddScoped<ServicioRevision>();
        servicios.AddScoped<ServicioPublicacion>();
        servicios.AddScoped<MigracionCargaInicial>();
        servicios.AddScoped<IServicioHistorial, ServicioHistorial>();
        servicios.AddScoped(typeof(ServicioEntidades<>));
        servicios.AddScoped<ServicioValoresCampo>();
        servicios.AddScoped<ServicioDocumento>();
        servicios.AddScoped<ServicioLogos>();

        // Registra todos los validadores y normalizadores del dominio (ValidadorRubro, NormalizadorParrafo, …).
        Type[] interfaces = [typeof(IValidadorEntidad<>), typeof(INormalizadorEntidad<>)];
        var implementaciones = typeof(ValidadorEntidad<>).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && interfaces.Contains(i.GetGenericTypeDefinition()))
                .Select(i => (Servicio: i, Implementacion: t)));
        foreach (var (servicio, implementacion) in implementaciones)
            servicios.AddScoped(servicio, implementacion);

        return servicios;
    }
}

/// <summary>
/// Usuario de prueba: el del encabezado <c>X-Usuario-Prueba</c> o, si no viene, el configurado en
/// <c>Simulacion:Usuario</c>. Solo simula el flujo del prototipo: no es autenticación. En producción se reemplaza por
/// el usuario autenticado del proveedor de identidad institucional.
/// </summary>
public sealed partial class UsuarioPrueba(IConfiguration configuracion, IHttpContextAccessor http) : IUsuarioActual
{
    public const string Encabezado = "X-Usuario-Prueba";

    private readonly string _predeterminado = configuracion["Simulacion:Usuario"] ?? "usuario.prueba";

    public string Nombre
    {
        get
        {
            var valor = http.HttpContext?.Request.Headers[Encabezado].ToString();
            if (string.IsNullOrWhiteSpace(valor))
                return _predeterminado;
            // Un valor con caracteres extraños no se usa como identificador; se rechazará como usuario no registrado.
            return IdentificadorValido().IsMatch(valor) ? valor : "(no válido)";
        }
    }

    [GeneratedRegex("^[a-z0-9._-]{1,60}$")]
    private static partial Regex IdentificadorValido();
}
