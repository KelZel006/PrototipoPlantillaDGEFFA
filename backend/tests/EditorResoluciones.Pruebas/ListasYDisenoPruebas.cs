using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>Filas que se repiten por registro (campos de lista), logo, pie de página e interlineado.</summary>
public sealed class ListasYDisenoPruebas : IDisposable
{
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;

    /// <summary>Imagen PNG válida de 1×1 píxel.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    public ListasYDisenoPruebas()
    {
        _fabrica = new FabricaApi(_datos.Ruta);
        _cliente = _fabrica.CreateClient();
    }

    public void Dispose()
    {
        _cliente.Dispose();
        _fabrica.Dispose();
        _datos.Dispose();
    }

    private static JsonObject Texto(string texto) => new() { ["type"] = "text", ["text"] = texto };

    private static JsonObject Campo(string id) => new() { ["type"] = "campo", ["attrs"] = new JsonObject { ["campoId"] = id } };

    private static JsonObject Celda(string tipo, params JsonNode[] enLinea) => new()
    {
        ["type"] = tipo,
        ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(enLinea) }),
    };

    private static JsonObject Fila(params JsonObject[] celdas) => new() { ["type"] = "tableRow", ["content"] = new JsonArray(celdas) };

    /// <summary>Tabla de bienes: encabezado y una fila con los tres campos de lista más el número de solicitud.</summary>
    private static JsonObject TablaDeBienes() => new()
    {
        ["type"] = "table",
        ["content"] = new JsonArray(
            Fila(Celda("tableHeader", Texto("Código")), Celda("tableHeader", Texto("Descripción")),
                Celda("tableHeader", Texto("Monto")), Celda("tableHeader", Texto("Solicitud"))),
            Fila(Celda("tableCell", Campo("codigo_arancelario")), Celda("tableCell", Campo("descripcion_arancelaria")),
                Celda("tableCell", Campo("monto_autorizado")), Celda("tableCell", Campo("numero_solicitud")))),
    };

    private async Task<HttpResponseMessage> PublicarParrafo(params JsonNode[] bloques)
    {
        var parrafo = new JsonObject
        {
            ["plantillaId"] = "PLA-001",
            ["seccion"] = "Considerando",
            ["contenido"] = new JsonObject { ["type"] = "doc", ["content"] = new JsonArray(bloques) },
        };
        return await _cliente.PostAsync("/api/parrafos", new StringContent(parrafo.ToJsonString(), Encoding.UTF8, "application/json"));
    }

    private async Task<DocumentoResuelto> Generar(string? solicitudId, double interlineado = Interlineados.Predeterminado)
    {
        using var alcance = _fabrica.Services.CreateScope();
        return await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync("PLA-001", solicitudId, interlineado);
    }

    /// <summary>Filas de la tabla del último considerando, como texto por celda.</summary>
    private static List<string[]> FilasDeLaTabla(DocumentoResuelto documento)
    {
        var tabla = documento.Secciones.Single(s => s.Seccion == Seccion.Considerando).Parrafos.Last()
            .Contenido.Contenido!.Single(n => n.Tipo == "table");
        static string TextoDe(NodoContenido n) => n.Tipo == "text" ? n.Texto ?? "" : string.Concat((n.Contenido ?? []).Select(TextoDe));
        return tabla.Contenido!.Select(f => f.Contenido!.Select(TextoDe).ToArray()).ToList();
    }

    [Fact]
    public async Task Repite_la_fila_de_lista_por_cada_registro_de_la_solicitud()
    {
        (await PublicarParrafo(TablaDeBienes())).EnsureSuccessStatusCode();

        // SOL-003 tiene tres bienes.
        var filas = FilasDeLaTabla(await Generar("SOL-003"));

        Assert.Equal(4, filas.Count);
        Assert.Equal(["Código", "Descripción", "Monto", "Solicitud"], filas[0]);
        Assert.Equal(["9999.20.01", "9999.20.02", "9999.30.01"], filas.Skip(1).Select(f => f[0]));
        Assert.Equal(["Excavadoras autopropulsadas (prueba)", "L 720,000.00", "PRUEBA-0003-2025"], filas[1][1..]);
        Assert.All(filas.Skip(1), f => Assert.Equal("PRUEBA-0003-2025", f[3]));
    }

    [Fact]
    public async Task Con_un_registro_genera_una_fila_y_sin_registros_lo_indica()
    {
        (await PublicarParrafo(TablaDeBienes())).EnsureSuccessStatusCode();

        var conUno = FilasDeLaTabla(await Generar("SOL-002"));
        Assert.Equal(2, conUno.Count);
        Assert.Equal("L 40,500.45", conUno[1][2]);

        var sinRegistros = await Generar("SOL-001");
        var filas = FilasDeLaTabla(sinRegistros);
        Assert.Equal(2, filas.Count);
        Assert.Equal([ServicioDocumento.TextoSinRegistros], filas[1]);
        Assert.Contains(sinRegistros.Advertencias, a => a.Contains("no tiene registros"));
    }

    [Fact]
    public async Task Sin_solicitud_deja_la_fila_una_vez_con_las_etiquetas()
    {
        (await PublicarParrafo(TablaDeBienes())).EnsureSuccessStatusCode();

        var filas = FilasDeLaTabla(await Generar(null));

        Assert.Equal(2, filas.Count);
        Assert.Equal(["{Código Arancelario}", "{Descripción Arancelaria}", "{Monto Autorizado}", "{Número de Solicitud}"], filas[1]);
    }

    [Fact]
    public async Task Un_campo_de_lista_solo_se_acepta_dentro_de_una_tabla()
    {
        var fuera = await PublicarParrafo(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(Campo("monto_autorizado")) });
        Assert.Equal(HttpStatusCode.BadRequest, fuera.StatusCode);
        Assert.Contains("solo se puede usar dentro de una tabla", await fuera.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Created, (await PublicarParrafo(TablaDeBienes())).StatusCode);
    }

    private async Task<HttpResponseMessage> CargarLogo(byte[] contenido, string nombre, string tipo = "image/png")
    {
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        formulario.Add(archivo, "archivo", nombre);
        return await _cliente.PostAsync("/api/plantillas/PLA-001/logo", formulario);
    }

    [Fact]
    public async Task Carga_reemplaza_y_quita_el_logo_de_la_plantilla()
    {
        var primera = await CargarLogo(Png, "logo.png");
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        var conLogo = (await primera.Content.ReadFromJsonAsync<Plantilla>(Json))!;
        Assert.StartsWith("ARC-", conLogo.LogoId);

        var imagen = await _cliente.GetAsync($"/api/archivos/{conLogo.LogoId}");
        Assert.Equal("image/png", imagen.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await imagen.Content.ReadAsByteArrayAsync());

        // Reemplazar elimina el archivo anterior.
        var reemplazo = (await (await CargarLogo(Png, "otro.png")).Content.ReadFromJsonAsync<Plantilla>(Json))!;
        Assert.NotEqual(conLogo.LogoId, reemplazo.LogoId);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/archivos/{conLogo.LogoId}")).StatusCode);

        var sinLogo = (await (await _cliente.DeleteAsync("/api/plantillas/PLA-001/logo")).Content.ReadFromJsonAsync<Plantilla>(Json))!;
        Assert.Null(sinLogo.LogoId);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/archivos/{reemplazo.LogoId}")).StatusCode);
    }

    [Theory]
    [InlineData("no soy una imagen", "logo.png", "PNG o JPEG")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "logo.svg", "PNG o JPEG")]
    public async Task Rechaza_archivos_que_no_son_png_ni_jpeg(string contenido, string nombre, string mensaje)
    {
        var respuesta = await CargarLogo(Encoding.UTF8.GetBytes(contenido), nombre);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensaje, await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rechaza_logos_de_mas_de_un_mega()
    {
        var grande = new byte[ServicioLogosTamano + 10];
        Png.CopyTo(grande, 0);

        var respuesta = await CargarLogo(grande, "grande.png");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("1 MB", await respuesta.Content.ReadAsStringAsync());
    }

    private const int ServicioLogosTamano = Dominio.Servicios.ServicioLogos.TamanoMaximo;

    [Fact]
    public async Task Guarda_el_pie_de_pagina_y_lo_lleva_al_documento_con_el_logo()
    {
        await CargarLogo(Png, "logo.png");
        var plantilla = (await _cliente.GetFromJsonAsync<Plantilla>("/api/plantillas/PLA-001", Json))!;
        plantilla.PiePagina = "Dirección de prueba\nTeléfono 0000-0000";
        plantilla.AlineacionLogo = AlineacionLogo.Izquierda;
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", plantilla, Json)).EnsureSuccessStatusCode();

        var documento = await Generar("SOL-001", 1.1);

        Assert.Equal("Dirección de prueba\nTeléfono 0000-0000", documento.Diseno.PiePagina);
        Assert.Equal(AlineacionLogo.Izquierda, documento.Diseno.AlineacionLogo);
        Assert.Equal(Png, documento.Diseno.Logo);
        Assert.Equal(1.1, documento.Diseno.Interlineado);

        var pdf = await _cliente.GetAsync("/api/plantillas/PLA-001/pdf?solicitudId=SOL-001&interlineado=1.1");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
    }

    [Fact]
    public void Ofrece_interlineados_de_0_8_a_2_de_0_1_en_0_1()
    {
        Assert.Equal([0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 2.0], Interlineados.Permitidos);
    }

    [Fact]
    public async Task Guarda_el_espacio_entre_firmas_y_lo_lleva_al_documento()
    {
        var plantilla = (await _cliente.GetFromJsonAsync<Plantilla>("/api/plantillas/PLA-001", Json))!;
        Assert.Equal(1.2, plantilla.EspacioFirmasCm);

        plantilla.EspacioFirmasCm = 3.5;
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", plantilla, Json)).EnsureSuccessStatusCode();

        Assert.Equal(3.5, (await Generar("SOL-001")).Diseno.EspacioFirmasCm);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync("/api/plantillas/PLA-001/pdf?solicitudId=SOL-001")).StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10.5)]
    public async Task Rechaza_un_espacio_entre_firmas_fuera_de_rango(double espacio)
    {
        var plantilla = (await _cliente.GetFromJsonAsync<Plantilla>("/api/plantillas/PLA-001", Json))!;
        plantilla.EspacioFirmasCm = espacio;

        var respuesta = await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", plantilla, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("entre 0 y 10 cm", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rechaza_un_logo_inexistente_al_actualizar_la_plantilla()
    {
        var plantilla = (await _cliente.GetFromJsonAsync<Plantilla>("/api/plantillas/PLA-001", Json))!;
        plantilla.LogoId = "ARC-NOEXISTE";

        var respuesta = await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", plantilla, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("logoId", await respuesta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("0.8", HttpStatusCode.OK)]
    [InlineData("0.9", HttpStatusCode.OK)]
    [InlineData("1", HttpStatusCode.OK)]
    [InlineData("1.0", HttpStatusCode.OK)]
    [InlineData("1.3", HttpStatusCode.OK)]
    [InlineData("2", HttpStatusCode.OK)]
    [InlineData("0.7", HttpStatusCode.BadRequest)]
    [InlineData("1.15", HttpStatusCode.BadRequest)]
    [InlineData("2.1", HttpStatusCode.BadRequest)]
    [InlineData("doble", HttpStatusCode.BadRequest)]
    public async Task Valida_el_interlineado_del_pdf(string interlineado, HttpStatusCode esperado)
    {
        var respuesta = await _cliente.GetAsync($"/api/plantillas/PLA-001/pdf?interlineado={interlineado}");
        Assert.Equal(esperado, respuesta.StatusCode);
    }
}
