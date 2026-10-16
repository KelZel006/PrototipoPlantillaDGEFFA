using System.Globalization;
using System.Text.Json;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace EditorResoluciones.Infraestructura.Pdf;

/// <summary>
/// Genera el PDF con MigraDoc (PDFsharp-MigraDoc 6.2.4, licencia MIT).
/// Atención: desde la versión 6.5.0 la librería usa licencia AGPL-3.0; no actualizar sin revisar la licencia.
/// </summary>
public sealed class GeneradorPdfMigraDoc : IGeneradorPdf
{
    private const string Fuente = "Arial";
    private static readonly Color ColorCampoPlantilla = new(0xFF, 0x22, 0x59, 0xBF);
    private static readonly Color ColorSinDato = new(0xFF, 0xC6, 0x2F, 0x3B);
    private static readonly Color ColorBorde = new(0xFF, 0x80, 0x88, 0x96);
    private static readonly Color ColorEncabezadoTabla = new(0xFF, 0xE8, 0xEE, 0xF6);
    private static readonly Color ColorGris = new(0xFF, 0x64, 0x74, 0x8B);
    private static readonly Unit SangriaLista = Unit.FromCentimeter(0.75);
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-HN");

    static GeneradorPdfMigraDoc()
    {
        // PDFsharp 6 no trae fuentes propias; en Windows usa las del sistema.
        if (OperatingSystem.IsWindows())
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public byte[] Generar(DocumentoResuelto documento)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("La generación de PDF del prototipo usa las fuentes de Windows.");

        var pdf = new Document();
        pdf.Info.Title = $"Resolución de prueba – {documento.Plantilla.Nombre}";
        pdf.Info.Author = "Prototipo de editor de resoluciones";
        var diseno = documento.Diseno;
        DefinirEstilos(pdf, diseno.Interlineado);
        var lineasPie = LineasPie(diseno.PiePagina);

        var seccion = pdf.AddSection();
        var pagina = seccion.PageSetup;
        pagina.PageFormat = PageFormat.Letter;
        // Los márgenes crecen con el logo y con las líneas del pie para que no se encimen con el texto.
        // Con logo: distancia del encabezado (1.2) + logo (1.8) + separación.
        pagina.TopMargin = Unit.FromCentimeter(diseno.Logo is null ? 2.5 : 4.0);
        pagina.BottomMargin = Unit.FromCentimeter(2.5 + 0.42 * lineasPie.Count);
        pagina.LeftMargin = Unit.FromCentimeter(2.5);
        pagina.RightMargin = Unit.FromCentimeter(2.5);
        pagina.HeaderDistance = Unit.FromCentimeter(1.2);
        pagina.FooterDistance = Unit.FromCentimeter(1.2);
        var anchoUtil = Unit.FromCentimeter(21.59 - 5.0);

        if (diseno.Logo is not null)
        {
            var parrafoLogo = seccion.Headers.Primary.AddParagraph();
            parrafoLogo.Format.SpaceAfter = Unit.FromPoint(4);
            // Sin esto el párrafo hereda el interlineado del documento (1.5 o doble) y el encabezado invade el texto.
            parrafoLogo.Format.LineSpacingRule = LineSpacingRule.Single;
            parrafoLogo.Format.Alignment = diseno.AlineacionLogo switch
            {
                AlineacionLogo.Izquierda => ParagraphAlignment.Left,
                AlineacionLogo.Derecha => ParagraphAlignment.Right,
                _ => ParagraphAlignment.Center,
            };
            var logo = parrafoLogo.AddImage("base64:" + Convert.ToBase64String(diseno.Logo));
            logo.Height = Unit.FromCentimeter(1.8);
            logo.LockAspectRatio = true;
        }

        foreach (var linea in lineasPie)
            seccion.Footers.Primary.AddParagraph(linea).Style = "Pie";

        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Style = "Aviso";
        pie.AddText("Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();

        var partes = documento.Secciones.Where(s => s.Parrafos.Count > 0).ToList();
        var hayContenido = partes.Count > 0;
        if (diseno.TextoCorrido)
        {
            // Texto corrido: el cuerpo va seguido y solo las tablas cortan el texto. Las firmas se presentan aparte.
            AgregarBloques(TextoCorrido.Unir(partes.Where(p => p.Seccion != Seccion.Firmas)), seccion.AddParagraph, seccion, anchoUtil, 0);
            partes = partes.Where(p => p.Seccion == Seccion.Firmas).ToList();
        }

        foreach (var parte in partes)
        {
            var titulo = Numeracion.Titulo(parte.Seccion);
            if (titulo is not null)
                seccion.AddParagraph(titulo).Style = "TituloSeccion";

            foreach (var parrafo in parte.Parrafos)
            {
                // Cada bloque de firma lleva el espacio configurado antes (separa las firmas entre sí y del texto anterior).
                if (parte.Seccion == Seccion.Firmas)
                    AgregarEspacio(seccion, diseno.EspacioFirmasCm);
                AgregarBloques(parrafo.Contenido.Contenido ?? [], seccion.AddParagraph, seccion, anchoUtil, 0,
                    parte.Seccion == Seccion.Firmas ? "Firma" : null);
            }
        }

        if (!hayContenido)
            seccion.AddParagraph("La plantilla no tiene párrafos para mostrar con la configuración actual.").Style = "Nota";

        var renderizador = new PdfDocumentRenderer { Document = pdf };
        renderizador.RenderDocument();
        using var memoria = new MemoryStream();
        renderizador.PdfDocument.Save(memoria, false);
        return memoria.ToArray();
    }

    /// <summary>Espacio vertical exacto: un párrafo vacío de altura mínima con el espacio antes indicado.</summary>
    private static void AgregarEspacio(Section seccion, double centimetros)
    {
        if (centimetros <= 0)
            return;
        var espacio = seccion.AddParagraph();
        espacio.Format.SpaceBefore = Unit.FromCentimeter(centimetros);
        espacio.Format.SpaceAfter = 0;
        espacio.Format.LineSpacingRule = LineSpacingRule.Exactly;
        espacio.Format.LineSpacing = Unit.FromPoint(1);
    }

    /// <summary>Líneas del pie de página; las vacías se conservan para respetar la separación escrita.</summary>
    private static List<string> LineasPie(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Replace("\r\n", "\n").Trim('\n').Split('\n').Select(l => l.TrimEnd()).Take(8).ToList();

    private static void DefinirEstilos(Document pdf, double interlineado)
    {
        var normal = pdf.Styles[StyleNames.Normal]!;
        normal.Font.Name = Fuente;
        normal.Font.Size = 11;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(8);
        // Con la regla Multiple, el valor se interpreta como factor del interlineado sencillo (0.8 a 2.0).
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = interlineado;
        normal.ParagraphFormat.Alignment = ParagraphAlignment.Justify;

        var titulo = pdf.Styles.AddStyle("TituloSeccion", StyleNames.Normal);
        titulo.Font.Bold = true;
        titulo.ParagraphFormat.SpaceBefore = Unit.FromPoint(10);
        titulo.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        titulo.ParagraphFormat.KeepWithNext = true;

        var aviso = pdf.Styles.AddStyle("Aviso", StyleNames.Normal);
        aviso.Font.Size = 7.5;
        aviso.Font.Color = ColorGris;
        aviso.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        aviso.ParagraphFormat.SpaceAfter = 0;
        aviso.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        // Firmas: con interlineado menor a 1, la línea de firma se montaría sobre el nombre.
        var firma = pdf.Styles.AddStyle("Firma", StyleNames.Normal);
        firma.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        firma.ParagraphFormat.LineSpacing = Math.Max(1.0, interlineado);

        var estiloPie = pdf.Styles.AddStyle("Pie", StyleNames.Normal);
        estiloPie.Font.Size = 8.5;
        estiloPie.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        estiloPie.ParagraphFormat.SpaceAfter = 0;
        estiloPie.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        var celda = pdf.Styles.AddStyle("Celda", StyleNames.Normal);
        celda.Font.Size = 9.5;
        celda.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        celda.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        celda.ParagraphFormat.Alignment = ParagraphAlignment.Left;

        pdf.Styles.AddStyle("CeldaEncabezado", "Celda").Font.Bold = true;

        var nota = pdf.Styles.AddStyle("Nota", StyleNames.Normal);
        nota.Font.Size = 9;
        nota.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        nota.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
        nota.ParagraphFormat.Alignment = ParagraphAlignment.Left;
    }

    /// <summary>Agrega párrafos, listas y tablas. Dentro de una celda no se admiten tablas (lo valida el servidor).</summary>
    private static void AgregarBloques(
        IEnumerable<NodoContenido> bloques,
        Func<Paragraph> nuevoParrafo,
        Section? seccion,
        Unit anchoUtil,
        int nivelLista,
        string? estilo = null)
    {
        foreach (var bloque in bloques)
        {
            switch (bloque.Tipo)
            {
                case "paragraph":
                    var parrafo = nuevoParrafo();
                    if (estilo is not null)
                        parrafo.Style = estilo;
                    Alinear(parrafo, bloque, justificarIzquierda: estilo is null);
                    AgregarEnLinea(parrafo, bloque.Contenido ?? []);
                    break;

                case "bulletList":
                case "orderedList":
                    var numero = bloque.Tipo == "orderedList" ? EnteroAtributo(bloque, "start") ?? 1 : 0;
                    foreach (var elemento in bloque.Contenido ?? [])
                    {
                        var vineta = bloque.Tipo == "bulletList" ? "•" : $"{numero++}.";
                        AgregarElementoLista(elemento, vineta, nuevoParrafo, seccion, anchoUtil, nivelLista + 1, estilo);
                    }
                    break;

                case "table" when seccion is not null:
                    AgregarTabla(seccion, bloque, anchoUtil);
                    break;
            }
        }
    }

    private static void AgregarElementoLista(
        NodoContenido elemento,
        string vineta,
        Func<Paragraph> nuevoParrafo,
        Section? seccion,
        Unit anchoUtil,
        int nivel,
        string? estilo)
    {
        var primero = true;
        foreach (var bloque in elemento.Contenido ?? [])
        {
            if (bloque.Tipo == "paragraph")
            {
                var parrafo = nuevoParrafo();
                if (estilo is not null)
                    parrafo.Style = estilo;
                parrafo.Format.LeftIndent = SangriaLista * nivel;
                parrafo.Format.FirstLineIndent = -SangriaLista;
                parrafo.Format.SpaceAfter = Unit.FromPoint(3);
                parrafo.Format.TabStops.AddTabStop(SangriaLista * nivel);
                if (primero)
                    parrafo.AddText(vineta);
                parrafo.AddTab();
                AgregarEnLinea(parrafo, bloque.Contenido ?? []);
                primero = false;
            }
            else
            {
                AgregarBloques([bloque], nuevoParrafo, seccion, anchoUtil, nivel, estilo);
            }
        }
    }

    private static void AgregarEnLinea(Paragraph parrafo, IEnumerable<NodoContenido> nodos)
    {
        foreach (var nodo in nodos)
        {
            if (nodo.Tipo == "hardBreak")
            {
                parrafo.AddLineBreak();
                continue;
            }
            if (nodo.Tipo != "text" || string.IsNullOrEmpty(nodo.Texto))
                continue;

            var marcas = (nodo.Marcas ?? []).Select(m => m.Tipo).ToHashSet(StringComparer.Ordinal);
            var enlace = (nodo.Marcas ?? []).FirstOrDefault(m => m.Tipo == "link");
            var href = enlace?.Atributos is not null && enlace.Atributos.TryGetValue("href", out var valor) ? valor.GetString() : null;

            FormattedText texto;
            if (href is not null)
            {
                var hipervinculo = parrafo.AddHyperlink(href, HyperlinkType.Web);
                texto = hipervinculo.AddFormattedText(nodo.Texto);
                texto.Color = ColorCampoPlantilla;
                texto.Underline = Underline.Single;
            }
            else
            {
                texto = parrafo.AddFormattedText(nodo.Texto);
            }

            if (marcas.Contains("bold"))
                texto.Bold = true;
            if (marcas.Contains("italic"))
                texto.Italic = true;
            if (marcas.Contains("underline"))
                texto.Underline = Underline.Single;
            if (marcas.Contains(MarcasDocumento.CampoPlantilla))
                texto.Color = ColorCampoPlantilla;
            if (marcas.Contains(MarcasDocumento.CampoSinDato))
            {
                texto.Color = ColorSinDato;
                texto.Bold = true;
            }
        }
    }

    private static void AgregarTabla(Section seccion, NodoContenido tabla, Unit anchoUtil)
    {
        var filas = tabla.Contenido ?? [];
        var columnas = Math.Max(1, filas.Select(f => (f.Contenido ?? []).Sum(Dominio.Servicios.ContenidoEditor.Expansion)).DefaultIfEmpty(1).Max());

        var destino = seccion.AddTable();
        destino.Borders.Width = 0.5;
        destino.Borders.Color = ColorBorde;
        destino.TopPadding = Unit.FromPoint(3);
        destino.BottomPadding = Unit.FromPoint(3);
        destino.LeftPadding = Unit.FromPoint(4);
        destino.RightPadding = Unit.FromPoint(4);
        for (var c = 0; c < columnas; c++)
            destino.AddColumn(anchoUtil / columnas);
        for (var f = 0; f < filas.Count; f++)
            destino.AddRow();

        // Rejilla de celdas ocupadas para respetar celdas combinadas (colspan y rowspan).
        var ocupada = new bool[filas.Count, columnas];
        for (var f = 0; f < filas.Count; f++)
        {
            var fila = destino.Rows[f];
            var columna = 0;
            var soloEncabezados = (filas[f].Contenido ?? []).All(c => c.Tipo == "tableHeader");
            if (soloEncabezados)
                fila.HeadingFormat = true;

            foreach (var celdaOrigen in filas[f].Contenido ?? [])
            {
                while (columna < columnas && ocupada[f, columna])
                    columna++;
                if (columna >= columnas)
                    break;

                var expansion = Math.Min(Dominio.Servicios.ContenidoEditor.Expansion(celdaOrigen), columnas - columna);
                var filasCombinadas = Math.Min(EnteroAtributo(celdaOrigen, "rowspan") ?? 1, filas.Count - f);
                for (var df = 0; df < filasCombinadas; df++)
                    for (var dc = 0; dc < expansion; dc++)
                        ocupada[f + df, columna + dc] = true;

                var celda = fila.Cells[columna];
                celda.MergeRight = expansion - 1;
                celda.MergeDown = filasCombinadas - 1;
                var encabezado = celdaOrigen.Tipo == "tableHeader";
                if (encabezado)
                    celda.Shading.Color = ColorEncabezadoTabla;
                AgregarBloques(celdaOrigen.Contenido ?? [], celda.AddParagraph, null, anchoUtil, 0,
                    encabezado ? "CeldaEncabezado" : "Celda");
                columna += expansion;
            }
        }

        // Separación breve después de la tabla (un párrafo vacío con interlineado fijo).
        var separacion = seccion.AddParagraph();
        separacion.Format.LineSpacingRule = LineSpacingRule.Exactly;
        separacion.Format.LineSpacing = Unit.FromPoint(8);
        separacion.Format.SpaceAfter = 0;
    }

    /// <summary>
    /// Aplica la alineación del editor. En el cuerpo del documento todo el texto va justificado: la alineación a la
    /// izquierda se justifica; centrado y derecha se respetan (títulos, firmas). En las celdas de tabla no se fuerza.
    /// </summary>
    private static void Alinear(Paragraph parrafo, NodoContenido bloque, bool justificarIzquierda)
    {
        var alineacion = bloque.Atributos is not null && bloque.Atributos.TryGetValue("textAlign", out var valor) &&
                         valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
        parrafo.Format.Alignment = alineacion switch
        {
            "center" => ParagraphAlignment.Center,
            "right" => ParagraphAlignment.Right,
            "left" => justificarIzquierda ? ParagraphAlignment.Justify : ParagraphAlignment.Left,
            "justify" => ParagraphAlignment.Justify,
            _ => parrafo.Format.Alignment,
        };
    }

    private static int? EnteroAtributo(NodoContenido nodo, string nombre) =>
        nodo.Atributos is not null && nodo.Atributos.TryGetValue(nombre, out var valor) &&
        valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var numero)
            ? numero
            : null;
}
