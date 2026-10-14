using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum EstadoPlantilla
{
    Borrador,
    Publicada
}

/// <summary>
/// Configuración de una resolución para una combinación de Rubro, Sub Rubro y Tipo Documento.
/// Sus párrafos se guardan aparte, en la colección de párrafos.
/// </summary>
public class Plantilla : IEntidad, IConMarcaTiempo
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(200, ErrorMessage = "El nombre admite hasta 200 caracteres.")]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = "El rubro es obligatorio.")]
    public string RubroId { get; set; } = "";

    [Required(ErrorMessage = "El sub rubro es obligatorio.")]
    public string SubRubroId { get; set; } = "";

    [Required(ErrorMessage = "El tipo de documento es obligatorio.")]
    public string TipoDocumentoId { get; set; } = "";

    public EstadoPlantilla Estado { get; set; } = EstadoPlantilla.Borrador;

    [StringLength(500, ErrorMessage = "La descripción admite hasta 500 caracteres.")]
    public string? Descripcion { get; set; }

    public DateTimeOffset FechaModificacion { get; set; }
}
