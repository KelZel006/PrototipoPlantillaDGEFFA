using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Cambia una palabra por otra en el texto de un párrafo, por ejemplo «resolución» por «certificación» al copiar la
/// Resolución a la Certificación. Reconoce la palabra con o sin tilde y en plural, y escribe el reemplazo con tilde
/// respetando mayúsculas: RESOLUCIÓN → CERTIFICACIÓN, Resolución → Certificación, resolucion → certificación,
/// resoluciones → certificaciones. Solo cambia palabras completas; los campos dinámicos no se tocan.
/// </summary>
public sealed class CambioPalabra
{
    private readonly Regex _patron;
    private readonly string _singular;
    private readonly string _plural;

    public CambioPalabra(string original, string reemplazo)
    {
        var palabra = new StringBuilder();
        foreach (var letra in QuitarTildes(original.Trim().ToLowerInvariant()))
        {
            palabra.Append(letra switch
            {
                'a' => "[aá]",
                'e' => "[eé]",
                'i' => "[ií]",
                'o' => "[oó]",
                'u' => "[uúü]",
                _ => Regex.Escape(letra.ToString()),
            });
        }
        _patron = new Regex($@"(?<!\p{{L}}){palabra}(?<plural>es)?(?!\p{{L}})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        _singular = reemplazo.Trim().ToLowerInvariant();
        // En plural la palabra aguda pierde la tilde: certificación → certificaciones.
        _plural = QuitarTildes(_singular) + "es";
    }

    public string Aplicar(string texto) => _patron.Replace(texto, coincidencia =>
    {
        var nuevo = coincidencia.Groups["plural"].Success ? _plural : _singular;
        var letras = coincidencia.Value.Where(char.IsLetter).ToList();
        if (letras.Count > 1 && letras.All(char.IsUpper))
            return nuevo.ToUpper(CultureInfo.GetCultureInfo("es"));
        return char.IsUpper(coincidencia.Value[0]) ? char.ToUpperInvariant(nuevo[0]) + nuevo[1..] : nuevo;
    });

    /// <summary>Devuelve una copia del contenido con la palabra cambiada en cada texto.</summary>
    public NodoContenido Aplicar(NodoContenido nodo) => new()
    {
        Tipo = nodo.Tipo,
        Texto = nodo.Tipo == "text" && nodo.Texto is not null ? Aplicar(nodo.Texto) : nodo.Texto,
        Atributos = nodo.Atributos,
        Marcas = nodo.Marcas,
        Contenido = nodo.Contenido?.Select(Aplicar).ToList(),
    };

    private static string QuitarTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            // Se conserva la ñ: solo se quitan las tildes de las vocales.
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark && c != '̃')
                continue;
            resultado.Append(c);
        }
        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
