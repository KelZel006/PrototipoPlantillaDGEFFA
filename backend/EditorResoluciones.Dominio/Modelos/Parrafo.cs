using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum Seccion
{
    Cabecera,
    Considerando,
    Resuelve
}

/// <summary>Párrafo de una sección de la plantilla.</summary>
public class Parrafo : IEntidad, IConMarcaTiempo
{
    /// <summary>Límite de caracteres de texto visible por párrafo (definido en el diseño de referencia).</summary>
    public const int MaximoCaracteres = 2000;

    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "La plantilla es obligatoria.")]
    public string PlantillaId { get; set; } = "";

    public Seccion Seccion { get; set; }

    [Range(1, 10_000, ErrorMessage = "El orden debe estar entre 1 y 10000.")]
    public int Orden { get; set; } = 1;

    /// <summary>Contenido estructurado del editor (formato de documento de TipTap/ProseMirror), nunca HTML libre.</summary>
    [Required(ErrorMessage = "El contenido es obligatorio.")]
    public NodoContenido Contenido { get; set; } = new();

    public bool Activo { get; set; } = true;

    /// <summary>Si es verdadero y hay condición, el párrafo solo se muestra cuando la condición se cumple.</summary>
    public bool MostrarSoloSiCumple { get; set; }

    public string? CondicionId { get; set; }

    /// <summary>Identificadores de los campos dinámicos usados en el contenido.</summary>
    public List<string> CamposUsados { get; set; } = [];

    public DateTimeOffset FechaModificacion { get; set; }
}

/// <summary>Nodo del documento del editor. Usa los nombres de propiedad del formato de TipTap/ProseMirror.</summary>
public class NodoContenido
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = "doc";

    [JsonPropertyName("text")]
    public string? Texto { get; set; }

    [JsonPropertyName("attrs")]
    public Dictionary<string, JsonElement>? Atributos { get; set; }

    [JsonPropertyName("marks")]
    public List<MarcaContenido>? Marcas { get; set; }

    [JsonPropertyName("content")]
    public List<NodoContenido>? Contenido { get; set; }
}

/// <summary>Formato aplicado a un fragmento de texto (negrita, cursiva, subrayado, enlace).</summary>
public class MarcaContenido
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = "";

    [JsonPropertyName("attrs")]
    public Dictionary<string, JsonElement>? Atributos { get; set; }
}
