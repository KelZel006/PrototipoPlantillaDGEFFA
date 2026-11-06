using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum AlineacionLogo
{
    Izquierda,
    Centro,
    Derecha
}

public enum EstadoPlantilla
{
    Borrador,
    Publicada
}

/// <summary>
/// Configuración de un documento para una combinación de Rubro, Sub Rubro, Tipo Documento, Documento y Beneficios.
/// Rubro y Sub Rubro son obligatorios; una lista opcional vacía significa que la plantilla sirve para cualquier valor de
/// esa lista. Sus párrafos se guardan aparte, en la colección de párrafos.
/// </summary>
public class Plantilla : IEntidad, IConMarcaTiempo
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(200, ErrorMessage = "El nombre admite hasta 200 caracteres.")]
    public string Nombre { get; set; } = "";

    /// <summary>
    /// Nombre de referencia que le da el analista («Nombre Plantilla»), por ejemplo «Exoneración a docentes 2026». Solo
    /// sirve para reconocerla: no cambia su identidad ni el nombre técnico.
    /// </summary>
    [StringLength(200, ErrorMessage = "El nombre de la plantilla admite hasta 200 caracteres.")]
    public string? NombreReferencia { get; set; }

    /// <summary>Unidad que elabora la plantilla. Forma parte de su identidad.</summary>
    public string UnidadId { get; set; } = "";

    [Required(ErrorMessage = "El rubro es obligatorio.")]
    public string RubroId { get; set; } = "";

    [Required(ErrorMessage = "El sub rubro es obligatorio.")]
    public string SubRubroId { get; set; } = "";

    /// <summary>Opcional: vacío si la plantilla sirve para cualquier tipo de documento.</summary>
    public string TipoDocumentoId { get; set; } = "";

    /// <summary>Lista "Documento": Original, Modificación o Aclaratoria. Opcional.</summary>
    public string ClaseDocumentoId { get; set; } = "";

    /// <summary>Lista "Beneficios": ISV, ISR, ACPV o Aportación Solidaria. Opcional.</summary>
    public string BeneficioId { get; set; } = "";

    public EstadoPlantilla Estado { get; set; } = EstadoPlantilla.Borrador;

    [StringLength(500, ErrorMessage = "La descripción admite hasta 500 caracteres.")]
    public string? Descripcion { get; set; }

    /// <summary>Logo del encabezado del documento (archivo de la colección de archivos); null si no tiene.</summary>
    public string? LogoId { get; set; }

    public AlineacionLogo AlineacionLogo { get; set; } = AlineacionLogo.Centro;

    /// <summary>Texto del pie de página del documento. Admite varias líneas.</summary>
    [StringLength(500, ErrorMessage = "El pie de página admite hasta 500 caracteres.")]
    public string? PiePagina { get; set; }

    /// <summary>Espacio en centímetros antes de cada bloque de firma (también separa las firmas del texto anterior).</summary>
    [Range(0.0, 10.0, ErrorMessage = "El espacio entre firmas debe estar entre 0 y 10 cm.")]
    public double EspacioFirmasCm { get; set; } = 1.2;

    public DateTimeOffset FechaModificacion { get; set; }
}
