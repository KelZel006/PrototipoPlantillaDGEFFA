using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum TipoDato
{
    Texto,
    Numero,
    Moneda,
    Fecha,
    Booleano
}

/// <summary>Campo dinámico del catálogo "Campos disponibles".</summary>
public class Campo : IEntidad
{
    /// <summary>Contextos en los que se puede usar un campo.</summary>
    public static readonly string[] ContextosValidos = ["cabecera", "considerando", "resuelve", "tabla"];

    /// <summary>Identificador técnico estable, por ejemplo <c>numero_solicitud</c>.</summary>
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "La etiqueta es obligatoria."), StringLength(100, ErrorMessage = "La etiqueta admite hasta 100 caracteres.")]
    public string Etiqueta { get; set; } = "";

    [Required(ErrorMessage = "La categoría es obligatoria."), StringLength(100, ErrorMessage = "La categoría admite hasta 100 caracteres.")]
    public string Categoria { get; set; } = "";

    public TipoDato TipoDato { get; set; }

    [Required(ErrorMessage = "El origen es obligatorio.")]
    public OrigenCampo Origen { get; set; } = new();

    public List<string> Contextos { get; set; } = [];

    [StringLength(300, ErrorMessage = "La descripción admite hasta 300 caracteres.")]
    public string? Descripcion { get; set; }
}

/// <summary>De dónde sale el valor del campo: una colección de datos permitida y una de sus propiedades.</summary>
public class OrigenCampo
{
    public string Coleccion { get; set; } = "";
    public string Propiedad { get; set; } = "";
}
