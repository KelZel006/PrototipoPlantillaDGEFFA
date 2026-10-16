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
/// La Certificación también se edita directamente; lo que después cambie en la Resolución la reemplaza.
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
        // Se compara el orden, no el número: la copia se numera 1, 2, 3… aunque la Resolución tenga huecos.
        Assert.Equal(origen.Select(p => (p.Id, p.Seccion, cambio.Aplicar(Texto(p.Contenido)), p.Activo)),
            copia.Select(p => (p.OrigenParrafoId!, p.Seccion, Texto(p.Contenido), p.Activo)));
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
    public async Task La_Certificacion_se_edita_y_conserva_sus_cambios_hasta_que_la_Resolucion_cambia_lo_mismo()
    {
        var copias = (await Parrafos("PLA-004")).Where(p => p.Seccion == Seccion.Considerando).OrderBy(p => p.Orden).ToList();
        Assert.True(copias.Count >= 2);
        var (editada, otra) = (copias[0], copias[1]);

        // Editar directamente en la Certificación: texto y estado. El cliente no puede quitar el vínculo con la Resolución.
        editada.Contenido = Doc("Texto propio de la certificación");
        editada.Activo = false;
        editada.OrigenParrafoId = null;
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{editada.Id}", editada, Json)).EnsureSuccessStatusCode();
        // Agregar un párrafo propio.
        var propio = new Parrafo { PlantillaId = "PLA-004", Seccion = Seccion.Considerando, Contenido = Doc("Párrafo solo de la certificación") };
        var creado = await (await _cliente.PostAsJsonAsync("/api/parrafos", propio, Json)).Content.ReadFromJsonAsync<Parrafo>(Json);
        Assert.Null(creado!.OrigenParrafoId);

        // Un cambio en otro párrafo de la Resolución llega a la Certificación sin tocar lo editado ni lo propio.
        var origenOtra = (await Parrafos("PLA-001")).Single(p => p.Id == otra.OrigenParrafoId);
        origenOtra.Contenido = Doc("Otro texto de la resolución");
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{origenOtra.Id}", origenOtra, Json)).EnsureSuccessStatusCode();

        var certificacion = await Parrafos("PLA-004");
        Assert.Equal("Otro texto de la certificación", Texto(certificacion.Single(p => p.Id == otra.Id).Contenido));
        var conservada = certificacion.Single(p => p.Id == editada.Id);
        Assert.Equal("Texto propio de la certificación", Texto(conservada.Contenido));
        Assert.False(conservada.Activo);
        Assert.NotNull(conservada.OrigenParrafoId);
        Assert.Contains(certificacion, p => p.Id == creado.Id);

        // Cuando la Resolución cambia ese mismo texto, gana la Resolución; el estado (no cambió allá) se conserva.
        var origenEditada = (await Parrafos("PLA-001")).Single(p => p.Id == conservada.OrigenParrafoId);
        origenEditada.Contenido = Doc("La resolución manda");
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{origenEditada.Id}", origenEditada, Json)).EnsureSuccessStatusCode();

        certificacion = await Parrafos("PLA-004");
        conservada = certificacion.Single(p => p.Id == editada.Id);
        Assert.Equal("La certificación manda", Texto(conservada.Contenido));
        Assert.False(conservada.Activo);
        Assert.Contains(certificacion, p => p.Id == creado.Id);
        // Los cambios de la Certificación no vuelven a la Resolución.
        Assert.DoesNotContain(await Parrafos("PLA-001"), p => Texto(p.Contenido).Contains("solo de la certificación"));
    }

    [Fact]
    public async Task Un_parrafo_nuevo_de_la_Resolucion_se_ubica_despues_de_su_anterior_y_se_conservan_los_propios()
    {
        var propio = new Parrafo { PlantillaId = "PLA-004", Seccion = Seccion.Considerando, Contenido = Doc("Párrafo propio al final") };
        var creado = await (await _cliente.PostAsJsonAsync("/api/parrafos", propio, Json)).Content.ReadFromJsonAsync<Parrafo>(Json);

        var primero = (await Parrafos("PLA-001")).Where(p => p.Seccion == Seccion.Considerando).OrderBy(p => p.Orden).First();
        var duplicado = await (await _cliente.PostAsync($"/api/parrafos/{primero.Id}/duplicar", null)).Content.ReadFromJsonAsync<Parrafo>(Json);

        var orden = (await Parrafos("PLA-004")).Where(p => p.Seccion == Seccion.Considerando).OrderBy(p => p.Orden).ToList();
        Assert.Equal(Enumerable.Range(1, orden.Count), orden.Select(p => p.Orden));
        Assert.Equal(primero.Id, orden[0].OrigenParrafoId);
        Assert.Equal(duplicado!.Id, orden[1].OrigenParrafoId);
        Assert.Equal(creado!.Id, orden[^1].Id);

        // Duplicar dentro de la Certificación crea un párrafo propio, no ligado a la Resolución.
        var dentro = await (await _cliente.PostAsync($"/api/parrafos/{orden[0].Id}/duplicar", null)).Content.ReadFromJsonAsync<Parrafo>(Json);
        Assert.Null(dentro!.OrigenParrafoId);
    }

    [Fact]
    public async Task El_diseno_se_copia_solo_en_lo_que_cambia_en_la_Resolucion()
    {
        var resolucion = (await Plantillas()).Single(p => p.Id == "PLA-001");
        resolucion.PiePagina = "Pie de prueba copiado";
        resolucion.EspacioFirmasCm = 2.5;
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", resolucion, Json)).EnsureSuccessStatusCode();

        var certificacion = (await Plantillas()).Single(p => p.Id == "PLA-004");
        Assert.Equal("Pie de prueba copiado", certificacion.PiePagina);
        Assert.Equal(2.5, certificacion.EspacioFirmasCm);

        // La Certificación cambia su pie; luego la Resolución cambia solo el espacio entre firmas.
        certificacion.PiePagina = "Pie propio";
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-004", certificacion, Json)).EnsureSuccessStatusCode();
        resolucion = (await Plantillas()).Single(p => p.Id == "PLA-001");
        resolucion.EspacioFirmasCm = 3;
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", resolucion, Json)).EnsureSuccessStatusCode();

        certificacion = (await Plantillas()).Single(p => p.Id == "PLA-004");
        Assert.Equal("Pie propio", certificacion.PiePagina);
        Assert.Equal(3, certificacion.EspacioFirmasCm);

        // Si la Resolución cambia el pie, gana la Resolución.
        resolucion = (await Plantillas()).Single(p => p.Id == "PLA-001");
        resolucion.PiePagina = "Pie nuevo de la resolución";
        (await _cliente.PutAsJsonAsync("/api/plantillas/PLA-001", resolucion, Json)).EnsureSuccessStatusCode();
        Assert.Equal("Pie nuevo de la resolución", (await Plantillas()).Single(p => p.Id == "PLA-004").PiePagina);
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

        // La Certificación puede cargar su propio logo; el compartido sigue en uso por la Resolución.
        var propio = (await (await CargarLogo("PLA-004")).Content.ReadFromJsonAsync<Plantilla>(Json))!.LogoId;
        Assert.NotEqual(primero, propio);
        Assert.Equal(primero, (await Plantillas()).Single(p => p.Id == "PLA-001").LogoId);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/archivos/{primero}")).StatusCode);

        // Al reemplazarlo en la Resolución, las dos pasan al nuevo y se borran los que ya nadie usa.
        var segundo = (await (await CargarLogo("PLA-001")).Content.ReadFromJsonAsync<Plantilla>(Json))!.LogoId;
        Assert.Equal(segundo, (await Plantillas()).Single(p => p.Id == "PLA-004").LogoId);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/archivos/{segundo}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/archivos/{primero}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"/api/archivos/{propio}")).StatusCode);
        // Solo queda el archivo en uso.
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
