using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// Cada cambio en la Resolución se copia a la Certificación de la misma combinación (datos semilla: PLA-001 → PLA-004).
/// </summary>
public sealed class CopiaCertificacionPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public CopiaCertificacionPruebas()
    {
        _fabrica = new FabricaApi(_datos.Ruta, Rutas.DatosSemilla);
        _cliente = _fabrica.CreateClient();
    }

    public void Dispose()
    {
        _cliente.Dispose();
        _fabrica.Dispose();
        _datos.Dispose();
    }

    private async Task<List<Parrafo>> Parrafos(string plantillaId) =>
        (await _cliente.GetFromJsonAsync<List<Parrafo>>($"/api/parrafos?plantillaId={plantillaId}", Json))!;

    private async Task<List<Plantilla>> Plantillas() =>
        (await _cliente.GetFromJsonAsync<List<Plantilla>>("/api/plantillas", Json))!;

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    /// <summary>La copia tiene los mismos párrafos, en el mismo orden y con el mismo contenido.</summary>
    private async Task AfirmarIguales(string origenId, string copiaId)
    {
        var origen = (await Parrafos(origenId)).OrderBy(p => p.Seccion).ThenBy(p => p.Orden).ToList();
        var copia = (await Parrafos(copiaId)).OrderBy(p => p.Seccion).ThenBy(p => p.Orden).ToList();
        // En la copia, «resolución» se cambia por «certificación».
        var cambio = new Dominio.Servicios.CambioPalabra("Resolución", "Certificación");
        Assert.Equal(origen.Select(p => (p.Id, p.Seccion, p.Orden, cambio.Aplicar(Texto(p.Contenido)), p.Activo)),
            copia.Select(p => (p.OrigenParrafoId!, p.Seccion, p.Orden, Texto(p.Contenido), p.Activo)));
    }

    [Fact]
    public async Task Crear_actualizar_y_eliminar_en_la_Resolucion_se_copia_a_la_Certificacion()
    {
        // Crear
        var nuevo = new Parrafo { PlantillaId = "PLA-001", Seccion = Seccion.Considerando, Contenido = Doc("Que el nuevo considerando…") };
        var creado = await (await _cliente.PostAsJsonAsync("/api/parrafos", nuevo, Json)).Content.ReadFromJsonAsync<Parrafo>(Json);
        await AfirmarIguales("PLA-001", "PLA-004");

        // Actualizar
        creado!.Contenido = Doc("Que el considerando cambió.");
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{creado.Id}", creado, Json)).EnsureSuccessStatusCode();
        await AfirmarIguales("PLA-001", "PLA-004");
        Assert.Contains(await Parrafos("PLA-004"), p => Texto(p.Contenido) == "Que el considerando cambió.");

        // Duplicar y reordenar
        (await _cliente.PostAsync($"/api/parrafos/{creado.Id}/duplicar", null)).EnsureSuccessStatusCode();
        var considerandos = (await Parrafos("PLA-001")).Where(p => p.Seccion == Seccion.Considerando).OrderBy(p => p.Orden).ToList();
        considerandos.Reverse();
        var reorden = new
        {
            plantillaId = "PLA-001",
            seccion = "Considerando",
            elementos = considerandos.Select(p => new { id = p.Id, version = p.Version }),
        };
        (await _cliente.PostAsJsonAsync("/api/parrafos/reordenar", reorden, Json)).EnsureSuccessStatusCode();
        await AfirmarIguales("PLA-001", "PLA-004");

        // Eliminar
        var actual = (await Parrafos("PLA-001")).Single(p => p.Id == creado.Id);
        (await _cliente.DeleteAsync($"/api/parrafos/{creado.Id}?version={actual.Version}")).EnsureSuccessStatusCode();
        await AfirmarIguales("PLA-001", "PLA-004");
    }

    [Fact]
    public async Task En_la_Certificacion_la_palabra_resolucion_se_cambia_por_certificacion()
    {
        var nuevo = new Parrafo
        {
            PlantillaId = "PLA-001",
            Seccion = Seccion.Resuelve,
            Contenido = Doc("RESOLUCIÓN No. 1: Notificar la presente resolución; las resoluciones quedan firmes."),
        };
        var creado = await (await _cliente.PostAsJsonAsync("/api/parrafos", nuevo, Json)).Content.ReadFromJsonAsync<Parrafo>(Json);

        var copia = (await Parrafos("PLA-004")).Single(p => p.OrigenParrafoId == creado!.Id);
        Assert.Equal("CERTIFICACIÓN No. 1: Notificar la presente certificación; las certificaciones quedan firmes.", Texto(copia.Contenido));
        // La Resolución conserva su texto.
        Assert.StartsWith("RESOLUCIÓN No. 1", Texto((await Parrafos("PLA-001")).Single(p => p.Id == creado!.Id).Contenido));
    }

    [Fact]
    public async Task La_Certificacion_no_se_edita_directamente()
    {
        var copia = (await Parrafos("PLA-004")).First();
        copia.Contenido = Doc("Cambio directo");

        var respuesta = await _cliente.PutAsJsonAsync($"/api/parrafos/{copia.Id}", copia, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("se copia automáticamente de la Resolución", await respuesta.Content.ReadAsStringAsync());
        var nuevo = new Parrafo { PlantillaId = "PLA-004", Seccion = Seccion.Cabecera, Contenido = Doc("Otro") };
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync("/api/parrafos", nuevo, Json)).StatusCode);
    }

    [Fact]
    public async Task El_diseno_de_la_Resolucion_se_copia_y_el_de_la_Certificacion_no_se_cambia()
    {
        var plantillas = await Plantillas();
        var resolucion = plantillas.Single(p => p.Id == "PLA-001");
        resolucion.PiePagina = "Pie de prueba copiado";
        resolucion.EspacioFirmasCm = 2.5;
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", resolucion, Json)).EnsureSuccessStatusCode();

        var certificacion = (await Plantillas()).Single(p => p.Id == "PLA-004");
        Assert.Equal("Pie de prueba copiado", certificacion.PiePagina);
        Assert.Equal(2.5, certificacion.EspacioFirmasCm);

        certificacion.PiePagina = "Otro pie";
        var respuesta = await _cliente.PutAsJsonAsync("/api/plantillas/PLA-004", certificacion, Json);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private async Task<HttpResponseMessage> CargarLogo(string plantillaId)
    {
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Png);
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        formulario.Add(archivo, "archivo", "logo.png");
        return await _cliente.PostAsync($"/api/plantillas/{plantillaId}/logo", formulario);
    }

    [Fact]
    public async Task El_logo_se_comparte_con_la_Certificacion_sin_perder_archivos()
    {
        var primero = (await (await CargarLogo("PLA-001")).Content.ReadFromJsonAsync<Plantilla>(Json))!.LogoId;
        Assert.Equal(primero, (await Plantillas()).Single(p => p.Id == "PLA-004").LogoId);

        // Cargar un logo en la Certificación se rechaza y no deja archivos sueltos ni borra el compartido.
        Assert.Equal(HttpStatusCode.BadRequest, (await CargarLogo("PLA-004")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/archivos/{primero}")).StatusCode);

        // Al reemplazarlo en la Resolución, las dos pasan al nuevo y el anterior se borra (nadie lo usa).
        var segundo = (await (await CargarLogo("PLA-001")).Content.ReadFromJsonAsync<Plantilla>(Json))!.LogoId;
        Assert.Equal(segundo, (await Plantillas()).Single(p => p.Id == "PLA-004").LogoId);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/archivos/{segundo}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/archivos/{primero}")).StatusCode);
        // Solo queda el archivo en uso: ni el rechazado ni el reemplazado.
        using var alcance = _fabrica.Services.CreateScope();
        var archivos = await alcance.ServiceProvider.GetRequiredService<IRepositorio<ArchivoRecurso>>().ListarAsync();
        Assert.Equal([segundo], archivos.Select(a => a.Id));
    }

    [Fact]
    public async Task Si_no_existe_la_Certificacion_se_crea_al_guardar_la_Resolucion()
    {
        // PLA-003 (1600 / 1601, Resolución, Original) no tiene Certificación en los datos semilla.
        Assert.DoesNotContain(await Plantillas(), p => p is { RubroId: "1600", SubRubroId: "1601", TipoDocumentoId: "TDO-004" });

        var parrafo = (await Parrafos("PLA-003")).First();
        parrafo.Contenido = Doc("Texto actualizado de la resolución.");
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{parrafo.Id}", parrafo, Json)).EnsureSuccessStatusCode();

        var certificacion = Assert.Single(await Plantillas(), p => p is { RubroId: "1600", SubRubroId: "1601", TipoDocumentoId: "TDO-004" });
        Assert.Equal("CDO-001", certificacion.ClaseDocumentoId);
        Assert.StartsWith("Certificación (Original) – ", certificacion.Nombre);
        await AfirmarIguales("PLA-003", certificacion.Id);
    }

    [Fact]
    public async Task Una_Resolucion_de_otra_combinacion_no_toca_la_Certificacion()
    {
        var antes = await Parrafos("PLA-004");
        var parrafo = (await Parrafos("PLA-002")).First();
        parrafo.Contenido = Doc("Cambio en el dictamen");
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{parrafo.Id}", parrafo, Json)).EnsureSuccessStatusCode();

        Assert.Equal(antes.Select(p => (p.Id, p.Version)), (await Parrafos("PLA-004")).Select(p => (p.Id, p.Version)));
        // Un Dictamen no tiene copia: no se crea ninguna plantilla nueva.
        Assert.DoesNotContain(await Plantillas(), p => p.Descripcion?.Contains("PLA-002") == true);
    }
}

/// <summary>Cambio de «resolución» por «certificación» en el texto copiado.</summary>
public class CambioPalabraPruebas
{
    private static readonly Dominio.Servicios.CambioPalabra Cambio = new("Resolución", "Certificación");

    [Theory]
    [InlineData("RESOLUCIÓN No. 15", "CERTIFICACIÓN No. 15")]
    [InlineData("RESOLUCION No. 15", "CERTIFICACIÓN No. 15")]
    [InlineData("Resolución de prueba", "Certificación de prueba")]
    [InlineData("la presente resolución.", "la presente certificación.")]
    [InlineData("la presente resolucion,", "la presente certificación,")]
    [InlineData("las resoluciones anteriores", "las certificaciones anteriores")]
    [InlineData("RESOLUCIONES", "CERTIFICACIONES")]
    [InlineData("(Resolución)", "(Certificación)")]
    public void Cambia_la_palabra_con_tilde_y_respetando_mayusculas(string texto, string esperado) =>
        Assert.Equal(esperado, Cambio.Aplicar(texto));

    [Theory]
    [InlineData("resolutivo")]
    [InlineData("irresolución")]
    [InlineData("Considerando")]
    public void Solo_cambia_palabras_completas(string texto) => Assert.Equal(texto, Cambio.Aplicar(texto));
}
