using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>Solicitud ficticia usada para resolver campos y probar condiciones.</summary>
public class Solicitud : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "El número de solicitud es obligatorio."), StringLength(50)]
    public string NumeroSolicitud { get; set; } = "";

    [StringLength(200)] public string? Operador { get; set; }
    [StringLength(200)] public string? Beneficiario { get; set; }

    [Required(ErrorMessage = "El rubro es obligatorio.")]
    public string RubroId { get; set; } = "";

    [Required(ErrorMessage = "El sub rubro es obligatorio.")]
    public string SubRubroId { get; set; } = "";

    [StringLength(150)] public string? TipoBeneficio { get; set; }
    [StringLength(50)] public string? Estado { get; set; }
    public DateOnly? FechaResolucion { get; set; }

    [Range(2000, 2100, ErrorMessage = "El año de gestión debe estar entre 2000 y 2100.")]
    public int? AnioGestion { get; set; }

    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "El monto no puede ser negativo.")]
    public decimal? MontoExoneracion { get; set; }

    [StringLength(150)] public string? UnidadEjecutora { get; set; }
    [StringLength(100)] public string? Departamento { get; set; }
    [StringLength(100)] public string? Municipio { get; set; }

    /// <summary>Marca explícita de que el registro es ficticio.</summary>
    public bool EsDatoPrueba { get; set; } = true;
}

/// <summary>Bien ficticio relacionado con una solicitud; alimenta las filas de las tablas dinámicas.</summary>
public class DetalleBien : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    [Required(ErrorMessage = "La solicitud es obligatoria.")]
    public string SolicitudId { get; set; } = "";

    [Required(ErrorMessage = "El código arancelario es obligatorio."), StringLength(20)]
    public string CodigoArancelario { get; set; } = "";

    [Required(ErrorMessage = "La descripción es obligatoria."), StringLength(300)]
    public string Descripcion { get; set; } = "";

    [StringLength(50)] public string? UnidadMedida { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "La cantidad no puede ser negativa.")]
    public decimal Cantidad { get; set; }

    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "El valor no puede ser negativo.")]
    public decimal? ValorEstimado { get; set; }

    public bool EsDatoPrueba { get; set; } = true;
}
