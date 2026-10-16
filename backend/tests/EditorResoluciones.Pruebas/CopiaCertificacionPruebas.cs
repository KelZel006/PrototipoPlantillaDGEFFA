using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;

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
        Assert.Equal(origen.Select(p => (p.Id, p.Seccion, p.Orden, Texto(p.Contenido), p.Activo)),
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
