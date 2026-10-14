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

/// <summary>
/// Condición de visualización: reglas y subgrupos combinados con Y u O.
/// Se guarda dentro del elemento al que pertenece (párrafo, y más adelante tabla, columna o fila),
/// para que el elemento y su condición se guarden en una sola escritura.
/// </summary>
public class GrupoCondiciones
{
    /// <summary>Profundidad máxima de grupos anidados (el grupo raíz cuenta como 1).</summary>
    public const int ProfundidadMaxima = 5;

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
