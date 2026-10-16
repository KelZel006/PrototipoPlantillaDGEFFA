using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>Lista 1 del selector de contexto.</summary>
public class Rubro : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(300, ErrorMessage = "El nombre admite hasta 300 caracteres.")]
    public string Nombre { get; set; } = "";

    public bool Activo { get; set; } = true;
}

/// <summary>Lista 2 del selector de contexto; depende del rubro seleccionado.</summary>
public class SubRubro : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El rubro es obligatorio.")]
    public string RubroId { get; set; } = "";

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(300, ErrorMessage = "El nombre admite hasta 300 caracteres.")]
    public string Nombre { get; set; } = "";

    public bool Activo { get; set; } = true;
}

/// <summary>Lista 4 del selector de contexto, "Documento": Original, Modificación o Aclaratoria.</summary>
public class ClaseDocumento : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
    public string Nombre { get; set; } = "";

    public bool Activo { get; set; } = true;
}

/// <summary>Lista 3 del selector de contexto.</summary>
public class TipoDocumento : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
    public string Nombre { get; set; } = "";

    /// <summary>
    /// Si es verdadero, el documento se genera con el texto corrido: todo seguido, sin separar párrafos; solo las
    /// tablas y las firmas conservan su forma (por ejemplo, la Certificación).
    /// </summary>
    public bool TextoCorrido { get; set; }

    /// <summary>
    /// Si se indica, las plantillas de este tipo son copia de las del tipo indicado con el mismo Rubro, Sub Rubro y
    /// Documento (la Certificación es copia de la Resolución). Se actualizan solas y no se editan directamente.
    /// </summary>
    public string? CopiaDeTipoDocumentoId { get; set; }

    public bool Activo { get; set; } = true;
}
