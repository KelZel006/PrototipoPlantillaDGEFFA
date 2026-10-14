using EditorResoluciones.Dominio.Comun;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EditorResoluciones.Api.Configuracion;

/// <summary>Convierte las excepciones del dominio en respuestas ProblemDetails en español, sin exponer detalles internos.</summary>
public sealed class ManejadorExcepciones(IProblemDetailsService problemas, ILogger<ManejadorExcepciones> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        ProblemDetails detalle = excepcion switch
        {
            ValidacionException ex => new ValidationProblemDetails(ex.Errores.ToDictionary(p => p.Key, p => p.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = ex.Message,
            },
            EntidadNoEncontradaException ex => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Registro no encontrado.",
                Detail = ex.Message,
            },
            ConflictoException ex => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto con los datos guardados.",
                Detail = ex.Message,
            },
            AlmacenamientoException ex => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Error en el almacenamiento de datos de prueba.",
                Detail = ex.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error inesperado.",
                Detail = "El error quedó registrado en el servidor.",
            },
        };

        if (detalle.Status >= StatusCodes.Status500InternalServerError)
            logger.LogError(excepcion, "Error al procesar {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);

        contexto.Response.StatusCode = detalle.Status!.Value;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = detalle,
            Exception = excepcion,
        });
    }
}
