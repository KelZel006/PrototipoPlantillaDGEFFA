using System.ComponentModel.DataAnnotations;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum OperadorLogico
{
    Y,
    O
}

public enum OperadorComparacion
{
    IgualA,
    DistintoDe,
    Contiene,
    NoContiene,
    EsNulo,
    NoEsNulo,
    MayorQue,
    MenorQue,
    MayorOIgualQue,
    MenorOIgualQue
}

/// <summary>Condición de visualización reutilizable por párrafos, tablas, columnas y filas.</summary>
public class Condicion : IEntidad
{
    /// <summary>Profundidad máxima de grupos anidados.</summary>
    public const int ProfundidadMaxima = 5;

    public string Id { get; set; } = "";
    public int Version { get; set; }

    [StringLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres.")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "La condición debe tener un grupo raíz.")]
    public GrupoCondiciones Raiz { get; set; } = new();
}

/// <summary>Conjunto de reglas y subgrupos combinados con Y u O.</summary>
public class GrupoCondiciones
{
    public OperadorLogico OperadorLogico { get; set; } = OperadorLogico.Y;
    public List<ReglaCondicion> Reglas { get; set; } = [];
    public List<GrupoCondiciones> Grupos { get; set; } = [];
}

public class ReglaCondicion
{
    public string CampoId { get; set; } = "";
    public OperadorComparacion Operador { get; set; }
    public string? Valor { get; set; }
}
