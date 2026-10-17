using EditorResoluciones.Infraestructura.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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
    /// <summary>
    /// Semilla de la aplicación (backend/EditorResoluciones.Api/DatosSemilla). Puede cambiar con el uso,
    /// por eso solo se le aplican pruebas de coherencia que no dependen de su contenido exacto.
    /// </summary>
    public static string DatosSemilla { get; } = BuscarDatosSemilla();

    /// <summary>Copia fija de los datos de prueba que usan las pruebas de comportamiento (no cambia con el uso).</summary>
    public static string DatosPrueba { get; } = Path.Combine(AppContext.BaseDirectory, "DatosPrueba");

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

/// <summary>
/// API en memoria que lee y escribe en una carpeta de datos indicada (copiada de la semilla). Con <paramref name="reloj"/>,
/// la API usa esa hora en lugar de la real.
/// </summary>
public sealed class FabricaApi(string directorioDatos, string? semilla = null, TimeProvider? reloj = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("AlmacenJson:Directorio", directorioDatos);
        builder.UseSetting("AlmacenJson:DirectorioSemilla", semilla ?? Rutas.DatosPrueba);
        if (reloj is not null)
            builder.ConfigureTestServices(servicios => servicios.AddSingleton(reloj));
    }
}

/// <summary>Atajos para preparar un Documento Consolidado en las pruebas del editor.</summary>
public static class Consolidados
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = EditorResoluciones.Dominio.Comun.OpcionesJson.Predeterminadas;

    /// <summary>Crea un consolidado (con el usuario predeterminado, analista) y devuelve su identificador.</summary>
    public static async Task<string> CrearAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsync("/api/consolidados", null);
        respuesta.EnsureSuccessStatusCode();
        return (await System.Net.Http.Json.HttpContentJsonExtensions
            .ReadFromJsonAsync<EditorResoluciones.Dominio.Modelos.DocumentoConsolidado>(respuesta.Content, Json))!.Id;
    }

    /// <summary>Crea un consolidado con la propuesta MODIFICAR de la plantilla y devuelve las propuestas agregadas.</summary>
    public static async Task<List<EditorResoluciones.Dominio.Modelos.Propuesta>> ModificarAsync(HttpClient cliente, string plantillaId, string? consolidadoId = null)
    {
        consolidadoId ??= await CrearAsync(cliente);
        var respuesta = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(
            cliente, $"/api/consolidados/{consolidadoId}/propuestas/modificar", new { plantillaId }, Json);
        respuesta.EnsureSuccessStatusCode();
        return (await System.Net.Http.Json.HttpContentJsonExtensions
            .ReadFromJsonAsync<List<EditorResoluciones.Dominio.Modelos.Propuesta>>(respuesta.Content, Json))!;
    }
}

/// <summary>Reloj que marca la hora indicada (se puede cambiar durante la prueba).</summary>
public sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;

    public override DateTimeOffset GetUtcNow() => Ahora.ToUniversalTime();
}
