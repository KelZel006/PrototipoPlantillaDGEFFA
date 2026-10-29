using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>
/// Presentación de texto corrido (por ejemplo, la Certificación): el texto de todas las secciones se transcribe
/// seguido, en un solo párrafo, sin saltos ni espacios entre párrafos. Solo las tablas conservan su forma: cortan el
/// texto, se presentan como cuadro y el texto continúa después. Los títulos (POR TANTO:, RESUELVE:) quedan en negrita
/// dentro del texto. Los párrafos marcados como título también cortan el texto y van centrados. Las firmas no se pasan
/// por aquí: conservan su presentación.
/// </summary>
public static class TextoCorrido
{
    /// <summary>Une las secciones en párrafos corridos y tablas, en el orden del documento.</summary>
    public static IReadOnlyList<NodoContenido> Unir(IEnumerable<SeccionDocumento> secciones)
    {
        var bloques = new List<NodoContenido>();
        List<NodoContenido>? enCurso = null;

        void Cerrar()
        {
            if (enCurso is { Count: > 0 })
                bloques.Add(new NodoContenido { Tipo = "paragraph", Contenido = enCurso });
            enCurso = null;
        }

        void Agregar(IEnumerable<NodoContenido> nodos)
        {
            // Los saltos de línea se vuelven espacios: el texto va seguido.
            var lista = nodos.Select(n => n.Tipo == "hardBreak" ? Espacio() : n).ToList();
            if (!lista.Any(n => !string.IsNullOrWhiteSpace(n.Texto)))
                return;
            enCurso ??= [];
            if (enCurso.Count > 0 && !TerminaEnEspacio(enCurso[^1]) && !EmpiezaConEspacio(lista[0]))
                enCurso.Add(Espacio());
            enCurso.AddRange(lista);
        }

        void Recorrer(IEnumerable<NodoContenido> nodos)
        {
            foreach (var bloque in nodos)
            {
                switch (bloque.Tipo)
                {
                    case "paragraph":
                        Agregar(bloque.Contenido ?? []);
                        break;
                    case "table":
                        Cerrar();
                        bloques.Add(bloque);
                        break;
                    case "bulletList":
                    case "orderedList":
                        var numero = 1;
                        foreach (var elemento in bloque.Contenido ?? [])
                        {
                            Agregar([Texto(bloque.Tipo == "orderedList" ? $"{numero++}." : "•")]);
                            Recorrer(elemento.Contenido ?? []);
                        }
                        break;
                    default:
                        Recorrer(bloque.Contenido ?? []);
                        break;
                }
            }
        }

        foreach (var parte in secciones.Where(s => s.Parrafos.Count > 0))
        {
            if (parte.Bloque.Titulo is { } titulo)
                Agregar([Texto(titulo, negrita: true)]);
            foreach (var parrafo in parte.Parrafos)
            {
                if (parrafo.EsTitulo)
                {
                    Cerrar();
                    bloques.AddRange(parrafo.Contenido.Contenido ?? []);
                    continue;
                }
                Recorrer(parrafo.Contenido.Contenido ?? []);
            }
        }
        Cerrar();
        return bloques;
    }

    private static NodoContenido Espacio() => Texto(" ");

    private static NodoContenido Texto(string texto, bool negrita = false) => new()
    {
        Tipo = "text",
        Texto = texto,
        Marcas = negrita ? [new MarcaContenido { Tipo = "bold" }] : null,
    };

    private static bool TerminaEnEspacio(NodoContenido nodo) => nodo.Texto is { Length: > 0 } t && char.IsWhiteSpace(t[^1]);

    private static bool EmpiezaConEspacio(NodoContenido nodo) => nodo.Texto is { Length: > 0 } t && char.IsWhiteSpace(t[0]);
}
