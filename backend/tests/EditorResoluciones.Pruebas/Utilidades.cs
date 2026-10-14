using EditorResoluciones.Infraestructura.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EditorResoluciones.Pruebas;

/// <summary>Carpeta temporal de datos que se elimina al terminar la prueba.</summary>
public sealed class DirectorioTemporal : IDisposable
{
    public string Ruta { get; } = Path.Combine(Path.GetTempPath(), "editor-resoluciones-pruebas", Guid.NewGuid().ToString("N"));

    public DirectorioTemporal() => Directory.CreateDirectory(Ruta);

    public string Archivo(string nombre) => Path.Combine(Ruta, nombre);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Ruta, recursive: true);
        }
        catch (IOException)
        {
            // Una carpeta temporal que no se pudo borrar no afecta el resultado.
        }
    }
}

public static class Rutas
{
    /// <summary>Carpeta real de datos de prueba iniciales del proyecto de la API.</summary>
    public static string DatosSemilla { get; } = BuscarDatosSemilla();

    private static string BuscarDatosSemilla()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null)
        {
            var candidata = Path.Combine(carpeta.FullName, "EditorResoluciones.Api", "DatosSemilla");
            if (Directory.Exists(candidata))
                return candidata;
            carpeta = carpeta.Parent;
        }
        throw new DirectoryNotFoundException("No se encontró la carpeta DatosSemilla.");
    }

    public static AlmacenJson CrearAlmacen(string directorio, string? semilla = null) =>
        new(Options.Create(new OpcionesAlmacenJson { Directorio = directorio, DirectorioSemilla = semilla }),
            NullLogger<AlmacenJson>.Instance);
}

/// <summary>API en memoria que lee y escribe en una carpeta de datos indicada (copiada de la semilla).</summary>
public sealed class FabricaApi(string directorioDatos) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("AlmacenJson:Directorio", directorioDatos);
        builder.UseSetting("AlmacenJson:DirectorioSemilla", Rutas.DatosSemilla);
    }
}
