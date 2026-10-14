using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Infraestructura.Json;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Infraestructura;

public static class RegistroInfraestructura
{
    /// <summary>
    /// Registra el almacenamiento simulado en archivos JSON. Para usar una base de datos real en el futuro,
    /// basta con registrar otra implementación de <see cref="IRepositorio{T}"/> en lugar de esta.
    /// </summary>
    public static IServiceCollection AgregarAlmacenamientoJson(this IServiceCollection servicios)
    {
        servicios.AddSingleton<AlmacenJson>();
        servicios.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioJson<>));
        servicios.AddSingleton<IDiagnosticoAlmacenamiento, DiagnosticoAlmacenamientoJson>();
        return servicios;
    }
}
