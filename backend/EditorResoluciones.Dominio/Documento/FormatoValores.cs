using System.Globalization;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>Presentación de los valores de los campos en el documento (convenciones de Honduras).</summary>
public static class FormatoValores
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-HN");

    /// <summary>Texto del valor según su tipo, o null si no hay dato.</summary>
    public static string? Formatear(TipoDato tipo, object? valor) => valor switch
    {
        null => null,
        string texto when string.IsNullOrWhiteSpace(texto) => null,
        DateOnly fecha => fecha.ToString("d 'de' MMMM 'de' yyyy", Cultura),
        bool logico => logico ? "Sí" : "No",
        _ when tipo == TipoDato.Moneda => "L " + Convert.ToDecimal(valor, CultureInfo.InvariantCulture).ToString("#,##0.00", CultureInfo.InvariantCulture),
        _ when tipo == TipoDato.Numero && valor is int entero => entero.ToString(CultureInfo.InvariantCulture),
        _ when tipo == TipoDato.Numero => Convert.ToDecimal(valor, CultureInfo.InvariantCulture).ToString("#,##0.##", CultureInfo.InvariantCulture),
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture),
    };
}
