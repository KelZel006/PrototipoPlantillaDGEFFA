using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>Estados del Documento Consolidado. Se aplican al documento completo.</summary>
public enum EstadoConsolidado
{
    EnRegistro,

    /// <summary>Enviado y pendiente de la revisión del Oficial Jurídico (decisión D6).</summary>
    Verificado,

    Aprobado
}

/// <summary>Documento Consolidado (PLAN-AAAANNNN): agrupa las propuestas de creación y modificación de plantillas.</summary>
public class DocumentoConsolidado : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    /// <summary>PLAN-AAAANNNN. Lo genera el servidor; no se escribe a mano.</summary>
    public string Numero { get; set; } = "";

    public int Anio { get; set; }

    /// <summary>Correlativo dentro del año (1 a 9999).</summary>
    public int Secuencia { get; set; }

    public EstadoConsolidado Estado { get; set; } = EstadoConsolidado.EnRegistro;

    /// <summary>Usuario analista responsable. Solo él lo ve en su bandeja (decisión D10).</summary>
    public string AnalistaId { get; set; } = "";

    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }

    /// <summary>Cantidad de veces que se envió a verificación (0 mientras no se envía).</summary>
    public int NumeroPresentacion { get; set; }

    public DateTimeOffset? FechaEnvio { get; set; }
    public string? EnviadoPor { get; set; }

    /// <summary>Oficial Jurídico de la última revisión (devolución o aprobación).</summary>
    public string? RevisorId { get; set; }

    public DateTimeOffset? FechaRevision { get; set; }
    public string? AprobadorId { get; set; }
    public DateTimeOffset? FechaAprobacion { get; set; }
}

public enum OperacionPropuesta
{
    Crear,
    Modificar
}

/// <summary>
/// Propuesta de creación o modificación de una plantilla dentro de un consolidado. Cada plantilla aparece una sola vez
/// por consolidado: todos sus ajustes van en la misma propuesta.
/// </summary>
public class Propuesta : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    public string ConsolidadoId { get; set; } = "";
    public OperacionPropuesta Operacion { get; set; }

    // Identidad funcional de la plantilla (Rubro, Sub Rubro, Tipo Documento y Documento).
    public string RubroId { get; set; } = "";
    public string SubRubroId { get; set; } = "";
    public string TipoDocumentoId { get; set; } = "";
    public string ClaseDocumentoId { get; set; } = "";

    public string Nombre { get; set; } = "";

    /// <summary>Plantilla que se modifica; null en una creación (la plantilla se crea al aprobar).</summary>
    public string? PlantillaId { get; set; }

    /// <summary>Versión vigente tomada como original (solo en una modificación).</summary>
    public string? VersionOrigenId { get; set; }

    /// <summary>Huella de la versión original al preparar la propuesta; al aprobar se comprueba que no cambió.</summary>
    public string? HuellaOrigen { get; set; }

    public ContenidoPlantilla ContenidoPropuesto { get; set; } = new();

    /// <summary>Si es la Certificación que acompaña a una Resolución, la propuesta de esa Resolución (decisión D3).</summary>
    public string? DerivadaDePropuestaId { get; set; }

    /// <summary>Eliminación lógica: la propuesta se retiró del borrador y queda registrada.</summary>
    public bool Retirada { get; set; }

    public string? MotivoRetiro { get; set; }

    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }
}

/// <summary>Envío a verificación. Congela lo enviado para reconstruir exactamente qué se revisó.</summary>
public class Presentacion : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    public string ConsolidadoId { get; set; } = "";

    /// <summary>1 en la primera presentación, 2 en la segunda…</summary>
    public int Numero { get; set; }

    public string Usuario { get; set; } = "";
    public DateTimeOffset Fecha { get; set; }
    public int CantidadPlantillas { get; set; }

    /// <summary>SHA-256 de las propuestas enviadas; al aprobar se comprueba que nada cambió desde el envío.</summary>
    public string HuellaIntegridad { get; set; } = "";

    /// <summary>Copia de las propuestas vigentes (no retiradas) en el momento del envío.</summary>
    public List<Propuesta> Propuestas { get; set; } = [];
}

/// <summary>Devolución a registro por el Oficial Jurídico. Se agregan; nunca se sobrescriben.</summary>
public class Devolucion : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    public string ConsolidadoId { get; set; } = "";

    /// <summary>Presentación revisada.</summary>
    public int NumeroPresentacion { get; set; }

    public string Usuario { get; set; } = "";
    public DateTimeOffset Fecha { get; set; }

    /// <summary>Justificación general, obligatoria.</summary>
    public string Justificacion { get; set; } = "";

    public List<Observacion> Observaciones { get; set; } = [];
}

/// <summary>Observación puntual de una devolución: una plantilla y, si corresponde, un bloque y un párrafo.</summary>
public class Observacion
{
    public string PropuestaId { get; set; } = "";
    public string? BloqueCodigo { get; set; }
    public string? ParrafoId { get; set; }
    public string Problema { get; set; } = "";
    public string? Recomendacion { get; set; }
}

public enum TipoMovimiento
{
    Creacion,
    PropuestaAgregada,
    PropuestaModificada,
    PropuestaRetirada,
    EnvioVerificacion,
    Devolucion,
    Aprobacion,
    Publicacion,
    CambioVigencia,
    ErrorPublicacion,
    Reasignacion
}

/// <summary>Bitácora del consolidado: la línea de tiempo de su ciclo de vida. Solo se agregan registros.</summary>
public class Movimiento : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    public string ConsolidadoId { get; set; } = "";
    public TipoMovimiento Tipo { get; set; }
    public string Usuario { get; set; } = "";
    public DateTimeOffset Fecha { get; set; }
    public EstadoConsolidado? EstadoOrigen { get; set; }
    public EstadoConsolidado? EstadoDestino { get; set; }
    public int? NumeroPresentacion { get; set; }
    public string Detalle { get; set; } = "";
}
