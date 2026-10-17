using EditorResoluciones.Api.Configuracion;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Infraestructura;
using EditorResoluciones.Infraestructura.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Límite del tamaño de las peticiones: la configuración de una plantilla no necesita más.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1_048_576);

// Las rutas relativas de los datos se resuelven desde la carpeta del proyecto de la API.
builder.Services.Configure<OpcionesAlmacenJson>(builder.Configuration.GetSection("AlmacenJson"));
builder.Services.PostConfigure<OpcionesAlmacenJson>(opciones =>
{
    var raiz = builder.Environment.ContentRootPath;
    opciones.Directorio = Path.GetFullPath(opciones.Directorio, raiz);
    if (!string.IsNullOrWhiteSpace(opciones.DirectorioSemilla))
        opciones.DirectorioSemilla = Path.GetFullPath(opciones.DirectorioSemilla, raiz);
});

builder.Services.AgregarAlmacenamientoJson();
builder.Services.AgregarDominio();

builder.Services
    .AddControllers()
    .AddJsonOptions(json => OpcionesJson.Configurar(json.JsonSerializerOptions));
builder.Services.Configure<ApiBehaviorOptions>(opciones =>
{
    opciones.InvalidModelStateResponseFactory = contexto =>
    {
        var parametros = contexto.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet();
        var errores = contexto.ModelState
            .Where(entrada => entrada.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entrada => entrada.Key,
                entrada => entrada.Value!.Errors.Select(error => MensajeEnEspanol(entrada.Key, error)).Distinct().ToArray());
        var problema = new ValidationProblemDetails(errores)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Los datos enviados no son válidos.",
        };
        return new BadRequestObjectResult(problema);

        // Los mensajes de las anotaciones del dominio ya están en español; los del lector de JSON no.
        string MensajeEnEspanol(string clave, Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error) =>
            clave.StartsWith('$') || error.Exception is not null || error.ErrorMessage.Contains("JSON", StringComparison.Ordinal)
                ? $"El valor de '{clave.TrimStart('$', '.')}' no tiene el formato esperado o el JSON no es válido."
                : parametros.Contains(clave)
                    ? "Debe enviar los datos del registro en formato JSON."
                    : error.ErrorMessage;
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Decisión D5: las plantillas sin versión vigente reciben su versión 1 «Carga inicial»; se completan las huellas de
// los datos semilla y se informan las inconsistencias de vigencia.
using (var alcance = app.Services.CreateScope())
{
    var migracion = await alcance.ServiceProvider.GetRequiredService<EditorResoluciones.Dominio.Consolidados.MigracionCargaInicial>().EjecutarAsync();
    if (migracion.VersionesCreadas > 0 || migracion.HuellasCompletadas > 0)
        app.Logger.LogInformation("Carga inicial: {Versiones} versiones vigentes creadas y {Huellas} huellas completadas.",
            migracion.VersionesCreadas, migracion.HuellasCompletadas);
    foreach (var inconsistencia in migracion.Inconsistencias)
        app.Logger.LogError("Inconsistencia de vigencia: {Detalle}", inconsistencia);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

/// <summary>Expuesto para las pruebas de integración.</summary>
public partial class Program;
