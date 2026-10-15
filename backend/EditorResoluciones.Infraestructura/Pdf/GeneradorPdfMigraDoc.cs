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
    private const string AvisoPrueba = "DOCUMENTO DE PRUEBA · DATOS SIMULADOS · SIN VALIDEZ OFICIAL";
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
        pdf.Info.Subject = AvisoPrueba;
        pdf.Info.Author = "Prototipo de editor de resoluciones";
        var diseno = documento.Diseno;
        DefinirEstilos(pdf, diseno.Interlineado);
        var lineasPie = LineasPie(diseno.PiePagina);

        var seccion = pdf.AddSection();
        var pagina = seccion.PageSetup;
        pagina.PageFormat = PageFormat.Letter;
        // Los márgenes crecen con el logo y con las líneas del pie para que no se encimen con el texto.
        // Con logo: distancia del encabezado (1.2) + logo (1.8) + aviso de prueba (~0.5) + separación.
        pagina.TopMargin = Unit.FromCentimeter(diseno.Logo is null ? 2.8 : 4.6);
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

        var encabezado = seccion.Headers.Primary.AddParagraph(AvisoPrueba);
        encabezado.Style = "Aviso";

        foreach (var linea in lineasPie)
            seccion.Footers.Primary.AddParagraph(linea).Style = "Pie";

        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Style = "Aviso";
        pie.AddText($"{documento.Plantilla.Id} · generado el {documento.Fecha.ToOffset(TimeSpan.FromHours(-6)):dd/MM/yyyy HH:mm} · Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();

        var hayContenido = false;
        foreach (var parte in documento.Secciones.Where(s => s.Parrafos.Count > 0))
        {
            hayContenido = true;
            var titulo = TituloSeccion(parte.Seccion);
            if (titulo is not null)
                seccion.AddParagraph(titulo).Style = "TituloSeccion";

            foreach (var parrafo in parte.Parrafos)
                AgregarBloques(parrafo.Contenido.Contenido ?? [], seccion.AddParagraph, seccion, anchoUtil, 0);
        }

        if (!hayContenido)
            seccion.AddParagraph("La plantilla no tiene párrafos para mostrar con la configuración actual.").Style = "Nota";

        AgregarResumen(seccion, documento, anchoUtil);

        var renderizador = new PdfDocumentRenderer { Document = pdf };
        renderizador.RenderDocument();
        using var memoria = new MemoryStream();
        renderizador.PdfDocument.Save(memoria, false);
        return memoria.ToArray();
    }

    private static string? TituloSeccion(Seccion seccion) => seccion switch
    {
        Seccion.Resuelve => "POR TANTO, RESUELVE:",
        _ => null,
    };

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
        switch (interlineado)
        {
            case <= 1.0:
                normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
                break;
            case >= 2.0:
                normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Double;
                break;
            case 1.5:
                normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.OnePtFive;
                break;
            default:
                // Con la regla Multiple, el valor se interpreta como factor (por ejemplo, 1.15).
                normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
                normal.ParagraphFormat.LineSpacing = interlineado;
                break;
        }
        normal.ParagraphFormat.Alignment = ParagraphAlignment.Justify;

        var titulo = pdf.Styles.AddStyle("TituloSeccion", StyleNames.Normal);
        titulo.Font.Bold = true;
        titulo.ParagraphFormat.SpaceBefore = Unit.FromPoint(10);
        titulo.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        titulo.ParagraphFormat.KeepWithNext = true;

        var aviso = pdf.Styles.AddStyle("Aviso", StyleNames.Normal);
        aviso.Font.Size = 7.5;
        aviso.Font.Color = ColorGris;
        aviso.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        aviso.ParagraphFormat.SpaceAfter = 0;
        aviso.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

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
                    Alinear(parrafo, bloque);
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

    private static void AgregarResumen(Section seccion, DocumentoResuelto documento, Unit anchoUtil)
    {
        seccion.AddPageBreak();
        seccion.AddParagraph("Resumen de la generación (solo para el prototipo)").Style = "TituloSeccion";

        var solicitud = documento.Solicitud is { } s
            ? $"{s.NumeroSolicitud} – {s.Beneficiario} ({s.Id})"
            : "Ninguna: los campos se muestran como {Etiqueta} y no se evaluaron las condiciones.";
        seccion.AddParagraph($"Plantilla: {documento.Plantilla.Nombre} ({documento.Plantilla.Id}, versión {documento.Plantilla.Version}).").Style = "Nota";
        seccion.AddParagraph($"Solicitud de prueba: {solicitud}").Style = "Nota";
        seccion.AddParagraph(
            "Incluidos: " + string.Join(" · ", documento.Secciones.Select(p => $"{p.Seccion} {p.Parrafos.Count}")) + ".").Style = "Nota";

        var omitidos = documento.Secciones.SelectMany(p => p.Omitidos.Select(o => (p.Seccion, o))).ToList();
        if (omitidos.Count > 0)
        {
            seccion.AddParagraph("Párrafos no incluidos:").Style = "TituloSeccion";
            var tabla = seccion.AddTable();
            tabla.Borders.Width = 0.5;
            tabla.Borders.Color = ColorBorde;
            tabla.LeftPadding = Unit.FromPoint(4);
            tabla.RightPadding = Unit.FromPoint(4);
            tabla.AddColumn(anchoUtil * 0.2);
            tabla.AddColumn(anchoUtil * 0.42);
            tabla.AddColumn(anchoUtil * 0.38);
            var titulo = tabla.AddRow();
            titulo.HeadingFormat = true;
            titulo.Shading.Color = ColorEncabezadoTabla;
            string[] cabeceras = ["Sección", "Texto", "Motivo"];
            for (var i = 0; i < cabeceras.Length; i++)
            {
                var parrafo = titulo.Cells[i].AddParagraph(cabeceras[i]);
                parrafo.Style = "Celda";
                parrafo.Format.Font.Bold = true;
            }
            foreach (var (seccionOmitida, omitido) in omitidos)
            {
                var fila = tabla.AddRow();
                fila.Cells[0].AddParagraph($"{seccionOmitida} {omitido.Posicion}").Style = "Celda";
                fila.Cells[1].AddParagraph(omitido.Resumen).Style = "Celda";
                fila.Cells[2].AddParagraph(omitido.Motivo).Style = "Celda";
            }
        }

        if (documento.Advertencias.Count > 0)
        {
            seccion.AddParagraph("Advertencias:").Style = "TituloSeccion";
            foreach (var advertencia in documento.Advertencias)
            {
                var parrafo = seccion.AddParagraph("• " + advertencia);
                parrafo.Style = "Nota";
                parrafo.Format.Font.Color = ColorSinDato;
            }
        }
    }

    private static void Alinear(Paragraph parrafo, NodoContenido bloque)
    {
        var alineacion = bloque.Atributos is not null && bloque.Atributos.TryGetValue("textAlign", out var valor) &&
                         valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
        parrafo.Format.Alignment = alineacion switch
        {
            "center" => ParagraphAlignment.Center,
            "right" => ParagraphAlignment.Right,
            "left" => ParagraphAlignment.Left,
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
