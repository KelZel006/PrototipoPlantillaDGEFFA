using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>Cómo se numeran los párrafos de un bloque en el documento.</summary>
public enum NumeracionBloque
{
    Ninguna,

    /// <summary>«CONSIDERANDO(1):», «CONSIDERANDO(2):»…</summary>
    Considerando,

    /// <summary>«PRIMERO:», «SEGUNDO:»…</summary>
    Ordinal
}

/// <summary>
/// Bloque del catálogo (CABECERA, VISTOS, CONSIDERANDO, FUNDAMENTACIÓN LEGAL…). Define cómo se presenta en el editor
/// y en el documento. Cada tipo documental elige qué bloques usa (<see cref="TipoDocumento.Bloques"/>), decisión D2.
/// </summary>
public class BloqueDocumental : IEntidad
{
    /// <summary>Código estable, por ejemplo <c>CONSIDERANDO</c>. La comparación empareja los bloques por este código.</summary>
    public string Id { get; set; } = "";

    public int Version { get; set; }

    /// <summary>Nombre de la pestaña del editor, por ejemplo «Fundamentación legal».</summary>
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(80, ErrorMessage = "El nombre admite hasta 80 caracteres.")]
    public string Nombre { get; set; } = "";

    /// <summary>Título que precede al bloque en el documento, por ejemplo «POR TANTO:»; null si no lleva.</summary>
    [StringLength(80, ErrorMessage = "El título admite hasta 80 caracteres.")]
    public string? Titulo { get; set; }

    public NumeracionBloque Numeracion { get; set; }

    /// <summary>Bloque de firmas: en el documento conserva su forma (también con texto corrido) y su espacio antes.</summary>
    public bool EsFirmas { get; set; }

    /// <summary>Contexto de los campos que se pueden insertar (uno de <see cref="Campo.ContextosValidos"/>, sin «tabla»).</summary>
    [Required(ErrorMessage = "El contexto de los campos es obligatorio.")]
    public string ContextoCampos { get; set; } = "";

    [StringLength(300, ErrorMessage = "La descripción admite hasta 300 caracteres.")]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Bloques anteriores cuyo contenido pasa a este cuando un tipo documental los deja de usar. Por ejemplo, en el
    /// Dictamen, «Dictamina» reemplaza a «Resuelve»: al modificar o clonar un Dictamen hecho con el formato anterior, sus
    /// párrafos de Resuelve pasan a Dictamina.
    /// </summary>
    public List<string> Reemplaza { get; set; } = [];
}

/// <summary>Bloque que usa un tipo documental, en su orden, y si es obligatorio.</summary>
public class BloqueDeTipo
{
    public string BloqueId { get; set; } = "";
    public int Orden { get; set; }
    public bool Obligatorio { get; set; }
}

/// <summary>Bloque tal como lo usa una plantilla de un tipo documental: datos del catálogo, orden y obligatoriedad.</summary>
public sealed record BloqueConfigurado(
    string Codigo,
    string Nombre,
    string? Titulo,
    NumeracionBloque Numeracion,
    bool EsFirmas,
    string ContextoCampos,
    bool Obligatorio,
    int Orden,
    IReadOnlyList<string>? Reemplaza = null);
