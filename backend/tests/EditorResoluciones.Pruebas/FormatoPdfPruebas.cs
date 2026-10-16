using System.Text.Json;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;
using EditorResoluciones.Infraestructura.Pdf;
using MigraDoc.DocumentObjectModel;

namespace EditorResoluciones.Pruebas;

/// <summary>Alineación justificada y tamaño del texto en el PDF.</summary>
public class FormatoPdfPruebas
{
    private static NodoContenido Texto(string texto, params MarcaContenido[] marcas) =>
        new() { Tipo = "text", Texto = texto, Marcas = marcas.Length > 0 ? [.. marcas] : null };

    private static MarcaContenido Tamano(string tamano) => new()
    {
        Tipo = ContenidoEditor.MarcaEstiloTexto,
        Atributos = new Dictionary<string, JsonElement> { ["fontSize"] = JsonSerializer.SerializeToElement(tamano) },
    };

    private static NodoContenido Parrafo(string? alineacion, params NodoContenido[] contenido) => new()
    {
        Tipo = "paragraph",
        Atributos = alineacion is null ? null : new Dictionary<string, JsonElement> { ["textAlign"] = JsonSerializer.SerializeToElement(alineacion) },
        Contenido = [.. contenido],
    };

    private static Document Construir(params NodoContenido[] bloques)
    {
        var parrafo = new ParrafoDocumento("PAR", 1, new NodoContenido { Tipo = "doc", Contenido = [.. bloques] });
        var documento = new DocumentoResuelto(
            new Plantilla { Id = "PLA", Nombre = "Prueba" },
            null,
            DateTimeOffset.UnixEpoch,
            [new SeccionDocumento(Seccion.Cabecera, [parrafo], [])],
            [],
            new DisenoDocumento(null, AlineacionLogo.Centro, null, 1.5, 1.2));
        return GeneradorPdfMigraDoc.Construir(documento);
    }

    private static List<Paragraph> Parrafos(Document documento) =>
        documento.Sections[0].Elements.OfType<Paragraph>().ToList();

    [Fact]
    public void Sin_alineacion_guardada_el_parrafo_hereda_el_justificado()
    {
        // El editor no guarda la alineación cuando es la predeterminada (justificada).
        var documento = Construir(Parrafo(null, Texto("Texto sin alineación guardada.")), Parrafo("center", Texto("Título")));

        var parrafos = Parrafos(documento);
        Assert.Null(parrafos[0].Format.Values.Alignment);
        Assert.Equal(ParagraphAlignment.Justify, documento.Styles[StyleNames.Normal]!.ParagraphFormat.Alignment);
        Assert.Equal(ParagraphAlignment.Center, parrafos[1].Format.Alignment);
    }

    [Fact]
    public void Aplica_el_tamano_de_texto_elegido()
    {
        var documento = Construir(Parrafo(null, Texto("Grande", Tamano("14pt")), Texto(" normal")));

        var textos = Parrafos(documento)[0].Elements.OfType<FormattedText>().ToList();
        Assert.Equal(14, textos[0].Size.Point, 3);
        Assert.Null(textos[1].Values.Size);
    }

    [Theory]
    [InlineData("12pt", true)]
    [InlineData("24pt", true)]
    [InlineData("99pt", false)]
    [InlineData("16px", false)]
    public void Solo_acepta_los_tamanos_de_la_lista(string tamano, bool valido)
    {
        var contenido = new NodoContenido { Tipo = "doc", Contenido = [Parrafo(null, Texto("Texto", Tamano(tamano)))] };
        var resultado = new ResultadoValidacion();

        ContenidoEditor.Validar(contenido, "cabecera", new Dictionary<string, Campo>(), resultado);

        Assert.Equal(valido, resultado.EsValido);
    }
}
