using System.Globalization;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>
/// Etiquetas con que empieza cada bloque numerado del documento:
/// "CONSIDERANDO(1): " en Considerando y "PRIMERO: ", "SEGUNDO: "… en Resuelve.
/// El frontend aplica las mismas reglas (core/numeracion.ts) para mostrar la numeración prevista.
/// </summary>
public static class Numeracion
{
    private static readonly string[] Unidades =
        ["", "PRIMERO", "SEGUNDO", "TERCERO", "CUARTO", "QUINTO", "SEXTO", "SÉPTIMO", "OCTAVO", "NOVENO"];

    private static readonly string[] Decenas =
        ["", "DÉCIMO", "VIGÉSIMO", "TRIGÉSIMO", "CUADRAGÉSIMO", "QUINCUAGÉSIMO", "SEXAGÉSIMO", "SEPTUAGÉSIMO", "OCTOGÉSIMO", "NONAGÉSIMO"];

    /// <summary>Etiqueta del bloque número <paramref name="numero"/> de la sección, o null si la sección no se numera.</summary>
    public static string? Etiqueta(Seccion seccion, int numero) => seccion switch
    {
        Seccion.Considerando => Etiqueta(NumeracionBloque.Considerando, numero),
        Seccion.Resuelve => Etiqueta(NumeracionBloque.Ordinal, numero),
        _ => null,
    };

    /// <summary>Etiqueta del párrafo número <paramref name="numero"/> según la numeración del bloque; null si no se numera.</summary>
    public static string? Etiqueta(NumeracionBloque numeracion, int numero) => numeracion switch
    {
        NumeracionBloque.Considerando => string.Create(CultureInfo.InvariantCulture, $"CONSIDERANDO({numero}): "),
        NumeracionBloque.Ordinal => $"{Ordinal(numero)}: ",
        _ => null,
    };

    /// <summary>Título que precede a la sección; Cabecera, Considerando y Firmas no llevan título.</summary>
    public static string? Titulo(Seccion seccion) => seccion switch
    {
        Seccion.PorTanto => "POR TANTO:",
        Seccion.Resuelve => "RESUELVE:",
        _ => null,
    };

    /// <summary>Ordinal en palabras y mayúsculas: 1 → PRIMERO, 11 → DÉCIMO PRIMERO, 20 → VIGÉSIMO. Desde 100, "100°".</summary>
    public static string Ordinal(int numero)
    {
        if (numero is < 1 or > 99)
            return string.Create(CultureInfo.InvariantCulture, $"{numero}°");
        var decena = Decenas[numero / 10];
        var unidad = Unidades[numero % 10];
        return (decena, unidad) switch
        {
            ("", _) => unidad,
            (_, "") => decena,
            _ => $"{decena} {unidad}",
        };
    }
}
