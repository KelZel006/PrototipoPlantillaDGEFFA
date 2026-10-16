using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Documento;
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
        servicios.AddScoped<ServicioParrafos>();
        // Copia de la Resolución a la Certificación después de cada guardado.
        servicios.AddScoped<SincronizadorCopias>();
        servicios.AddScoped<IObservadorEntidad<Parrafo>>(p => p.GetRequiredService<SincronizadorCopias>());
        servicios.AddScoped<IObservadorEntidad<Plantilla>>(p => p.GetRequiredService<SincronizadorCopias>());
        servicios.AddScoped<ServicioValoresCampo>();
        servicios.AddScoped<ServicioDocumento>();
        servicios.AddScoped<ServicioLogos>();

        // Registra todos los validadores y normalizadores del dominio (ValidadorRubro, NormalizadorParrafo, …).
        Type[] interfaces = [typeof(IValidadorEntidad<>), typeof(INormalizadorEntidad<>), typeof(IReglaModificacion<>)];
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

/// <summary>Usuario de prueba configurable (<c>Simulacion:Usuario</c>). No hay autenticación en esta etapa.</summary>
public sealed class UsuarioPrueba(IConfiguration configuracion) : IUsuarioActual
{
    public string Nombre { get; } = configuracion["Simulacion:Usuario"] ?? "usuario.prueba";
}
