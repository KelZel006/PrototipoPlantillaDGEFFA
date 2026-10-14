using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EditorResoluciones.Dominio.Comun;

/// <summary>Opciones de serialización compartidas por la API, el almacenamiento JSON y el historial.</summary>
public static class OpcionesJson
{
    public static JsonSerializerOptions Predeterminadas { get; } = Configurar(new JsonSerializerOptions());

    public static JsonSerializerOptions Configurar(JsonSerializerOptions opciones)
    {
        opciones.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opciones.PropertyNameCaseInsensitive = true;
        opciones.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        // Conserva tildes y eñes legibles en los archivos JSON.
        opciones.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        opciones.WriteIndented = true;
        // Rechaza null en propiedades no anulables (por ejemplo, una lista de reglas enviada como null).
        opciones.RespectNullableAnnotations = true;
        if (!opciones.Converters.OfType<JsonStringEnumConverter>().Any())
        {
            opciones.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        }
        return opciones;
    }
}
