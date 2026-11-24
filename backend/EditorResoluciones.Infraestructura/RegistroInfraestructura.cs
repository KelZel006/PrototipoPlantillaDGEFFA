using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Infraestructura.Json;
using EditorResoluciones.Infraestructura.Pdf;
using EditorResoluciones.Infraestructura.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Infraestructura;

public static class RegistroInfraestructura
{
    /// <summary>
    /// Registra el almacenamiento en SQL Server: las tablas SEFH_ de la base indicada en <see cref="OpcionesAlmacenSql"/>.
    /// Los logos se guardan en la misma base.
    /// </summary>
    public static IServiceCollection AgregarAlmacenamientoSqlServer(this IServiceCollection servicios)
    {
        servicios.AddSingleton<AlmacenSql>();
        servicios.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioSql<>));
        servicios.AddSingleton<IUnidadDeTrabajo, UnidadDeTrabajoSql>();
        servicios.AddSingleton<IDiagnosticoAlmacenamiento, DiagnosticoAlmacenamientoSql>();
        servicios.AddSingleton<IGeneradorPdf, GeneradorPdfMigraDoc>();
        servicios.AddSingleton<IAlmacenArchivos, AlmacenArchivosSql>();
        return servicios;
    }

    /// <summary>
    /// Registra el almacenamiento simulado en archivos JSON. Lo usan las pruebas automáticas, que no dependen de una base
    /// de datos instalada.
    /// </summary>
    public static IServiceCollection AgregarAlmacenamientoJson(this IServiceCollection servicios)
    {
        servicios.AddSingleton<AlmacenJson>();
        servicios.AddSingleton(typeof(IRepositorio<>), typeof(RepositorioJson<>));
        servicios.AddSingleton<IUnidadDeTrabajo, UnidadDeTrabajoJson>();
        servicios.AddSingleton<IDiagnosticoAlmacenamiento, DiagnosticoAlmacenamientoJson>();
        servicios.AddSingleton<IGeneradorPdf, GeneradorPdfMigraDoc>();
        servicios.AddSingleton<IAlmacenArchivos, AlmacenArchivosDisco>();
        return servicios;
    }
}
