using System.Text.Json;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Reglas del contenido del editor. El servidor solo acepta los nodos, formatos y atributos de esta lista;
/// cualquier otra cosa (HTML, scripts, imágenes, estilos libres) se rechaza.
/// </summary>
public static class ContenidoEditor
{
    public const string NodoCampo = "campo";

    private const int ProfundidadMaxima = 20;
    private const int NodosMaximos = 2000;

    private static readonly string[] Alineaciones = ["left", "center", "right", "justify"];
    private static readonly string[] ProtocolosEnlace = ["http", "https", "mailto"];

    /// <summary>Nodos permitidos y los atributos que admite cada uno.</summary>
    private static readonly Dictionary<string, string[]> Nodos = new(StringComparer.Ordinal)
    {
        ["doc"] = [],
        ["paragraph"] = ["textAlign"],
        ["text"] = [],
        ["hardBreak"] = [],
        ["bulletList"] = [],
        ["orderedList"] = ["start", "type"],
        ["listItem"] = [],
        [NodoCampo] = ["campoId"],
        ["table"] = [],
        ["tableRow"] = [],
        ["tableHeader"] = ["colspan", "rowspan", "colwidth"],
        ["tableCell"] = ["colspan", "rowspan", "colwidth"],
    };

    /// <summary>Hijos permitidos de los nodos con estructura fija (las tablas).</summary>
    private static readonly Dictionary<string, string[]> HijosPermitidos = new(StringComparer.Ordinal)
    {
        ["table"] = ["tableRow"],
        ["tableRow"] = ["tableHeader", "tableCell"],
    };

    /// <summary>Límites de una tabla dentro de un párrafo.</summary>
    public const int ColumnasMaximas = 12;
    public const int FilasMaximas = 100;

    private static readonly string[] EnLinea = ["text", NodoCampo, "hardBreak"];

    /// <summary>Formatos de texto permitidos y sus atributos.</summary>
    private static readonly Dictionary<string, string[]> Marcas = new(StringComparer.Ordinal)
    {
        ["bold"] = [],
        ["italic"] = [],
        ["underline"] = [],
        ["link"] = ["href", "target", "rel", "class", "title"],
    };

    /// <summary>Identificadores de los campos insertados, en orden de aparición y sin repetir.</summary>
    public static List<string> CamposUsados(NodoContenido raiz) =>
        Recorrer(raiz)
            .Where(n => n.Tipo == NodoCampo)
            .Select(CampoId)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Caracteres visibles: el texto escrito más cada campo contado como "{Etiqueta}".
    /// El contador del editor en Angular usa la misma regla.
    /// </summary>
    public static int ContarCaracteres(NodoContenido raiz, IReadOnlyDictionary<string, Campo> catalogo) =>
        Recorrer(raiz).Sum(n => n.Tipo switch
        {
            "text" => n.Texto?.Length ?? 0,
            NodoCampo => CampoId(n) is { } id && catalogo.TryGetValue(id, out var campo) ? campo.Etiqueta.Length + 2 : 0,
            _ => 0,
        });

    /// <summary>Texto plano con los campos como "{Etiqueta}" y las tablas como "[Tabla]"; sirve para resúmenes.</summary>
    public static string TextoPlano(NodoContenido raiz, IReadOnlyDictionary<string, Campo> catalogo)
    {
        var partes = new System.Text.StringBuilder();
        void Visitar(NodoContenido nodo)
        {
            switch (nodo.Tipo)
            {
                case "text":
                    partes.Append(nodo.Texto);
                    return;
                case NodoCampo:
                    var id = CampoId(nodo) ?? "";
                    partes.Append('{').Append(catalogo.TryGetValue(id, out var campo) ? campo.Etiqueta : id).Append('}');
                    return;
                case "table":
                    partes.Append(" [Tabla] ");
                    return;
                case "hardBreak":
                    partes.Append(' ');
                    return;
            }
            foreach (var hijo in nodo.Contenido ?? [])
                Visitar(hijo);
            if (nodo.Tipo is "paragraph" or "listItem")
                partes.Append(' ');
        }
        Visitar(raiz);
        return System.Text.RegularExpressions.Regex.Replace(partes.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>Verdadero si hay texto visible o al menos un campo.</summary>
    public static bool TieneContenido(NodoContenido raiz) =>
        Recorrer(raiz).Any(n => n.Tipo == NodoCampo || (n.Tipo == "text" && !string.IsNullOrWhiteSpace(n.Texto)));

    /// <summary>Valida estructura, formatos, enlaces y campos según el contexto (por ejemplo, "considerando").</summary>
    public static void Validar(
        NodoContenido raiz,
        string contexto,
        IReadOnlyDictionary<string, Campo> catalogo,
        ResultadoValidacion resultado)
    {
        const string clave = "contenido";
        if (raiz.Tipo != "doc")
        {
            resultado.Agregar(clave, "El contenido debe ser un documento del editor.");
            return;
        }

        var nodos = 0;
        if (!ValidarNodo(raiz, 1, ref nodos, contexto, catalogo, resultado))
        {
            resultado.Agregar(clave, "El contenido es demasiado grande o tiene demasiados niveles.");
            return;
        }

        if (!TieneContenido(raiz))
            resultado.Agregar(clave, "El párrafo no puede estar vacío.");

        var caracteres = ContarCaracteres(raiz, catalogo);
        if (caracteres > Parrafo.MaximoCaracteres)
            resultado.Agregar(clave, $"El párrafo tiene {caracteres} caracteres; el máximo es {Parrafo.MaximoCaracteres}.");
    }

    private static bool ValidarNodo(
        NodoContenido nodo,
        int profundidad,
        ref int nodos,
        string contexto,
        IReadOnlyDictionary<string, Campo> catalogo,
        ResultadoValidacion resultado)
    {
        const string clave = "contenido";
        if (++nodos > NodosMaximos || profundidad > ProfundidadMaxima)
            return false;

        if (!Nodos.TryGetValue(nodo.Tipo, out var atributosPermitidos))
        {
            resultado.Agregar(clave, $"El elemento '{nodo.Tipo}' no está permitido en el contenido.");
            return true;
        }

        ValidarAtributos(nodo.Tipo, nodo.Atributos, atributosPermitidos, resultado);

        switch (nodo.Tipo)
        {
            case "text" when string.IsNullOrEmpty(nodo.Texto):
                resultado.Agregar(clave, "Hay un fragmento de texto vacío.");
                break;
            case "paragraph" when Texto(nodo.Atributos, "textAlign") is { } alineacion && !Alineaciones.Contains(alineacion):
                resultado.Agregar(clave, $"La alineación '{alineacion}' no está permitida.");
                break;
            case NodoCampo:
                ValidarCampo(nodo, contexto, catalogo, resultado);
                break;
            case "table":
                ValidarTabla(nodo, resultado);
                break;
        }

        if (HijosPermitidos.TryGetValue(nodo.Tipo, out var hijosPermitidos) &&
            (nodo.Contenido ?? []).Any(h => !hijosPermitidos.Contains(h.Tipo)))
        {
            resultado.Agregar(clave, $"La estructura de la tabla no es válida ({nodo.Tipo}).");
        }

        if (nodo.Tipo != "text" && nodo.Texto is not null)
            resultado.Agregar(clave, $"El elemento '{nodo.Tipo}' no puede contener texto directo.");

        foreach (var marca in nodo.Marcas ?? [])
        {
            // El formato se aplica a elementos en línea: texto, campos (por ejemplo, un campo en negrita) y saltos de línea.
            if (!EnLinea.Contains(nodo.Tipo) || !Marcas.TryGetValue(marca.Tipo, out var atributosMarca))
            {
                resultado.Agregar(clave, $"El formato '{marca.Tipo}' no está permitido.");
                continue;
            }
            ValidarAtributos(marca.Tipo, marca.Atributos, atributosMarca, resultado);
            if (marca.Tipo == "link" && !EnlaceSeguro(Texto(marca.Atributos, "href")))
                resultado.Agregar(clave, "Los enlaces solo pueden usar http, https o mailto.");
        }

        foreach (var hijo in nodo.Contenido ?? [])
        {
            if (!ValidarNodo(hijo, profundidad + 1, ref nodos, contexto, catalogo, resultado))
                return false;
        }
        return true;
    }

    private static void ValidarCampo(
        NodoContenido nodo,
        string contexto,
        IReadOnlyDictionary<string, Campo> catalogo,
        ResultadoValidacion resultado)
    {
        var campoId = CampoId(nodo);
        if (string.IsNullOrEmpty(campoId))
            resultado.Agregar("contenido", "Hay un campo dinámico sin identificador.");
        else if (!catalogo.TryGetValue(campoId, out var campo))
            resultado.Agregar("contenido", $"El campo '{campoId}' no existe en el catálogo.");
        else if (!campo.Contextos.Contains(contexto))
            resultado.Agregar("contenido", $"El campo '{campo.Etiqueta}' no se puede usar en {contexto}.");
    }

    private static void ValidarTabla(NodoContenido tabla, ResultadoValidacion resultado)
    {
        var filas = tabla.Contenido ?? [];
        if (filas.Count == 0)
            resultado.Agregar("contenido", "La tabla no tiene filas.");
        if (filas.Count > FilasMaximas)
            resultado.Agregar("contenido", $"La tabla admite hasta {FilasMaximas} filas.");

        foreach (var celda in filas.SelectMany(f => f.Contenido ?? []))
        {
            foreach (var nombre in (string[])["colspan", "rowspan"])
            {
                if (celda.Atributos is not null && celda.Atributos.TryGetValue(nombre, out var valor) &&
                    valor.ValueKind != JsonValueKind.Null &&
                    (valor.ValueKind != JsonValueKind.Number || !valor.TryGetInt32(out var numero) || numero < 1 || numero > ColumnasMaximas))
                {
                    resultado.Agregar("contenido", $"El valor de '{nombre}' de una celda no es válido.");
                }
            }
        }

        if (filas.SelectMany(f => f.Contenido ?? []).Any(celda => Recorrer(celda).Any(n => n.Tipo == "table")))
            resultado.Agregar("contenido", "No se admiten tablas dentro de otra tabla.");

        var columnas = filas.Select(f => (f.Contenido ?? []).Sum(Expansion)).DefaultIfEmpty(0).Max();
        if (columnas > ColumnasMaximas)
            resultado.Agregar("contenido", $"La tabla admite hasta {ColumnasMaximas} columnas.");
    }

    /// <summary>Columnas que ocupa una celda (colspan).</summary>
    public static int Expansion(NodoContenido celda) =>
        celda.Atributos is not null && celda.Atributos.TryGetValue("colspan", out var valor) &&
        valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var numero) && numero > 0
            ? numero
            : 1;

    private static void ValidarAtributos(
        string elemento,
        Dictionary<string, JsonElement>? atributos,
        string[] permitidos,
        ResultadoValidacion resultado)
    {
        foreach (var nombre in (atributos ?? []).Keys.Where(k => !permitidos.Contains(k)))
            resultado.Agregar("contenido", $"El atributo '{nombre}' no está permitido en '{elemento}'.");
    }

    private static bool EnlaceSeguro(string? href) =>
        Uri.TryCreate(href, UriKind.Absolute, out var uri) && ProtocolosEnlace.Contains(uri.Scheme);

    private static string? CampoId(NodoContenido nodo) => Texto(nodo.Atributos, "campoId");

    private static string? Texto(Dictionary<string, JsonElement>? atributos, string nombre) =>
        atributos is not null && atributos.TryGetValue(nombre, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static IEnumerable<NodoContenido> Recorrer(NodoContenido nodo)
    {
        var pendientes = new Stack<NodoContenido>();
        pendientes.Push(nodo);
        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();
            yield return actual;
            var hijos = actual.Contenido ?? [];
            for (var i = hijos.Count - 1; i >= 0; i--)
                pendientes.Push(hijos[i]);
        }
    }
}
