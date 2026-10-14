using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Api.Configuracion;

public static class RegistroDominio
{
    public static IServiceCollection AgregarDominio(this IServiceCollection servicios)
    {
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddSingleton<IUsuarioActual, UsuarioPrueba>();
        servicios.AddScoped<IServicioHistorial, ServicioHistorial>();
        servicios.AddScoped(typeof(ServicioEntidades<>));

        // Registra todos los validadores del dominio (ValidadorRubro, ValidadorParrafo, …).
        var interfaz = typeof(IValidadorEntidad<>);
        var validadores = typeof(ValidadorEntidad<>).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaz)
                .Select(i => (Servicio: i, Implementacion: t)));
        foreach (var (servicio, implementacion) in validadores)
            servicios.AddScoped(servicio, implementacion);

        return servicios;
    }
}

/// <summary>Usuario de prueba configurable (<c>Simulacion:Usuario</c>). No hay autenticación en esta etapa.</summary>
public sealed class UsuarioPrueba(IConfiguration configuracion) : IUsuarioActual
{
    public string Nombre { get; } = configuracion["Simulacion:Usuario"] ?? "usuario.prueba";
}
