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
/// <summary>
/// Unidad que elabora sus propias plantillas: Exoneraciones, Dispensas, Registro Exonerados, Notas de Crédito y
/// Devoluciones. Cada unidad tiene su menú, sus Documentos PLAN y sus plantillas.
/// </summary>
public class Unidad : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
    public string Nombre { get; set; } = "";

    /// <summary>Posición en el menú.</summary>
    [Range(1, 100, ErrorMessage = "El orden debe estar entre 1 y 100.")]
    public int Orden { get; set; } = 1;

    public bool Activo { get; set; } = true;
}

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
    /// Si se indica, las plantillas de este tipo acompañan a las del tipo indicado con el mismo Rubro, Sub Rubro y
    /// Documento (la Certificación acompaña a la Resolución): en un Documento Consolidado, la propuesta de la Resolución
    /// lleva consigo la de su Certificación, que se actualiza con cada cambio de la Resolución (decisión D3).
    /// </summary>
    public string? CopiaDeTipoDocumentoId { get; set; }

    /// <summary>
    /// Bloques del documento, en su orden (decisión D2). Vacío: los cinco de siempre (Cabecera, Considerando, Por Tanto,
    /// Resuelve y Firmas).
    /// </summary>
    public List<BloqueDeTipo> Bloques { get; set; } = [];

    public bool Activo { get; set; } = true;
}
