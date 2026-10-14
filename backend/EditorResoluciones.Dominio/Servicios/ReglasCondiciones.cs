using System.Globalization;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>Operadores permitidos por tipo de dato y validación de una condición de visualización.</summary>
public static class ReglasCondiciones
{
    private static readonly OperadorComparacion[] Nulidad = [OperadorComparacion.EsNulo, OperadorComparacion.NoEsNulo];

    private static readonly OperadorComparacion[] Comparables =
    [
        OperadorComparacion.IgualA, OperadorComparacion.DistintoDe,
        OperadorComparacion.MayorQue, OperadorComparacion.MenorQue,
        OperadorComparacion.MayorOIgualQue, OperadorComparacion.MenorOIgualQue,
        .. Nulidad,
    ];

    public static IReadOnlyDictionary<TipoDato, OperadorComparacion[]> OperadoresPorTipo { get; } =
        new Dictionary<TipoDato, OperadorComparacion[]>
        {
            [TipoDato.Texto] =
            [
                OperadorComparacion.IgualA, OperadorComparacion.DistintoDe,
                OperadorComparacion.Contiene, OperadorComparacion.NoContiene,
                .. Nulidad,
            ],
            [TipoDato.Numero] = Comparables,
            [TipoDato.Moneda] = Comparables,
            [TipoDato.Fecha] = Comparables,
            [TipoDato.Booleano] = [OperadorComparacion.IgualA, OperadorComparacion.DistintoDe, .. Nulidad],
        };

    public static bool RequiereValor(OperadorComparacion operador) => !Nulidad.Contains(operador);

    public static IEnumerable<ReglaCondicion> Reglas(GrupoCondiciones grupo) =>
        grupo.Reglas.Concat(grupo.Grupos.SelectMany(Reglas));

    public static HashSet<string> CamposUsados(GrupoCondiciones? grupo) =>
        grupo is null ? [] : Reglas(grupo).Select(r => r.CampoId).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Valida la condición de un elemento del documento. Los campos deben salir de la colección indicada
    /// (para un párrafo, los datos generales de la solicitud) y el valor debe ser compatible con su tipo.
    /// </summary>
    public static void Validar(
        GrupoCondiciones raiz,
        string coleccionPermitida,
        IReadOnlyDictionary<string, Campo> catalogo,
        ResultadoValidacion resultado,
        string clave = "condicion")
    {
        if (Profundidad(raiz) > GrupoCondiciones.ProfundidadMaxima)
            resultado.Agregar(clave, $"Los grupos de condiciones admiten hasta {GrupoCondiciones.ProfundidadMaxima} niveles.");

        if (GrupoVacio(raiz))
            resultado.Agregar(clave, "Hay un grupo de condiciones sin reglas.");

        foreach (var regla in Reglas(raiz))
        {
            if (!catalogo.TryGetValue(regla.CampoId, out var campo))
            {
                resultado.Agregar(clave, $"El campo '{regla.CampoId}' no existe en el catálogo.");
                continue;
            }
            if (campo.Origen.Coleccion != coleccionPermitida)
            {
                resultado.Agregar(clave, $"El campo '{campo.Etiqueta}' no se puede usar en esta condición.");
                continue;
            }
            if (!Enum.IsDefined(regla.Operador) || !OperadoresPorTipo[campo.TipoDato].Contains(regla.Operador))
            {
                resultado.Agregar(clave, $"El operador elegido no es válido para '{campo.Etiqueta}' ({campo.TipoDato}).");
                continue;
            }
            if (RequiereValor(regla.Operador))
            {
                if (string.IsNullOrWhiteSpace(regla.Valor))
                    resultado.Agregar(clave, $"La regla sobre '{campo.Etiqueta}' necesita un valor de comparación.");
                else if (!ValorCompatible(campo.TipoDato, regla.Valor))
                    resultado.Agregar(clave, $"El valor '{regla.Valor}' no es válido para '{campo.Etiqueta}' ({DescripcionFormato(campo.TipoDato)}).");
            }
        }
    }

    /// <summary>Comprueba que el valor escrito se pueda interpretar según el tipo de dato.</summary>
    public static bool ValorCompatible(TipoDato tipo, string valor) => tipo switch
    {
        TipoDato.Numero or TipoDato.Moneda => decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
        TipoDato.Fecha => DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        TipoDato.Booleano => valor is "true" or "false",
        _ => true,
    };

    private static string DescripcionFormato(TipoDato tipo) => tipo switch
    {
        TipoDato.Numero or TipoDato.Moneda => "use un número, con punto decimal",
        TipoDato.Fecha => "use el formato AAAA-MM-DD",
        TipoDato.Booleano => "use true o false",
        _ => "texto",
    };

    private static bool GrupoVacio(GrupoCondiciones grupo) =>
        (grupo.Reglas.Count == 0 && grupo.Grupos.Count == 0) || grupo.Grupos.Any(GrupoVacio);

    private static int Profundidad(GrupoCondiciones grupo) =>
        1 + (grupo.Grupos.Count == 0 ? 0 : grupo.Grupos.Max(Profundidad));
}
