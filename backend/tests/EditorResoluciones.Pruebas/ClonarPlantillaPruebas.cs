using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// CREAR una plantilla nueva desde cero o clonando la versión vigente de otra, elegida por su Rubro, Sub Rubro, Tipo
/// Documento y Documento.
/// </summary>
public sealed class ClonarPlantillaPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public ClonarPlantillaPruebas()
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

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    private async Task<HttpResponseMessage> Crear(string consolidado, string rubro, string subRubro, string tipo, string clase, string? origen) =>
        await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidado}/propuestas/crear",
            new SolicitudCrearPlantilla(rubro, subRubro, tipo, clase, null, origen), Json);

    private async Task<VersionPlantilla> Vigente(string plantillaId) =>
        (await _cliente.GetFromJsonAsync<List<VersionPlantilla>>("/api/versiones-plantilla?vigencia=Vigente", Json))!
            .Single(v => v.PlantillaId == plantillaId);

    [Fact]
    public async Task Clonar_copia_el_contenido_vigente_con_identificadores_nuevos_sin_tocar_el_origen()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var origen = await Vigente("PLA-001");

        // Misma combinación que PLA-001, pero como «Modificación» (CDO-002): otra identidad.
        var respuesta = await Crear(consolidado, "RUB-001", "SUB-001", "TDO-001", "CDO-002", "PLA-001");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var nueva = (await respuesta.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single();

        Assert.Equal(OperacionPropuesta.Crear, nueva.Operacion);
        Assert.Null(nueva.PlantillaId);
        Assert.Null(nueva.VersionOrigenId);
        var parrafosOrigen = origen.Contenido.Bloques.SelectMany(b => b.Parrafos.Select(p => (b.Codigo, Texto(p.Contenido)))).ToList();
        var parrafosNuevos = nueva.ContenidoPropuesto.Bloques.SelectMany(b => b.Parrafos.Select(p => (b.Codigo, Texto(p.Contenido)))).ToList();
        Assert.NotEmpty(parrafosOrigen);
        Assert.Equal(parrafosOrigen, parrafosNuevos);
        var idsOrigen = origen.Contenido.Bloques.SelectMany(b => b.Parrafos).Select(p => p.Id).ToHashSet();
        Assert.All(nueva.ContenidoPropuesto.Bloques.SelectMany(b => b.Parrafos), p =>
        {
            Assert.DoesNotContain(p.Id, idsOrigen);
            Assert.Equal(1, p.Version);
            Assert.Null(p.OrigenParrafoId);
        });
        Assert.Equal(origen.Contenido.Diseno.PiePagina, nueva.ContenidoPropuesto.Diseno.PiePagina);

        // Editar la copia no cambia la plantilla de origen.
        var parrafo = nueva.ContenidoPropuesto.Bloques.SelectMany(b => b.Parrafos).First();
        (await _cliente.DeleteAsync($"/api/propuestas/{nueva.Id}/parrafos/{parrafo.Id}?version=1")).EnsureSuccessStatusCode();
        Assert.Equal(HuellaContenido.De(origen.Contenido), (await Vigente("PLA-001")).Huella);

        // La bitácora indica de dónde salió.
        var detalle = await _cliente.GetStringAsync($"/api/consolidados/{consolidado}");
        Assert.Contains("copia de «Aprobación – cooperación bilateral (prueba)», versión 1", detalle);
    }

    [Fact]
    public async Task Clonar_otro_tipo_conserva_sus_bloques_y_desde_cero_queda_vacia()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);

        // PLA-002 es de otro tipo (TDO-002); la nueva será TDO-003.
        var copia = (await (await Crear(consolidado, "RUB-002", "SUB-003", "TDO-003", "CDO-001", "PLA-002"))
            .Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single();
        var origen = await Vigente("PLA-002");
        Assert.Equal(origen.Contenido.Bloques.Sum(b => b.Parrafos.Count), copia.ContenidoPropuesto.Bloques.Sum(b => b.Parrafos.Count));

        var vacia = (await (await Crear(consolidado, "RUB-002", "SUB-003", "TDO-003", "CDO-002", null))
            .Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single();
        Assert.All(vacia.ContenidoPropuesto.Bloques, b => Assert.Empty(b.Parrafos));
    }

    [Fact]
    public async Task No_se_clona_una_plantilla_inexistente_y_no_se_agrega_nada()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);

        var respuesta = await Crear(consolidado, "RUB-001", "SUB-001", "TDO-001", "CDO-002", "PLA-999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var detalle = await _cliente.GetFromJsonAsync<JsonElement>($"/api/consolidados/{consolidado}", Json);
        Assert.Equal(0, detalle.GetProperty("propuestas").GetArrayLength());
    }
}

/// <summary>Al clonar una Resolución, su Certificación también nace como copia (D3), con la palabra cambiada.</summary>
public sealed class ClonarResolucionPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public ClonarResolucionPruebas()
    {
        foreach (var coleccion in new[] { "consolidados", "propuestas", "presentaciones", "devoluciones", "movimientos" })
            File.WriteAllText(_datos.Archivo($"{coleccion}.json"), "[]");
        _fabrica = new FabricaApi(_datos.Ruta, Rutas.DatosSemilla);
        _cliente = _fabrica.CreateClient();
    }

    public void Dispose()
    {
        _cliente.Dispose();
        _fabrica.Dispose();
        _datos.Dispose();
    }

    [Fact]
    public async Task La_Certificacion_de_una_Resolucion_clonada_tambien_es_copia()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);

        // PLA-001: Resolución 1100 / 1107 Original. La nueva: la misma combinación como Modificación.
        var respuesta = await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidado}/propuestas/crear",
            new SolicitudCrearPlantilla("1100", "1107", "TDO-001", "CDO-002", null, "PLA-001"), Json);
        respuesta.EnsureSuccessStatusCode();
        var agregadas = (await respuesta.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!;

        Assert.Equal(2, agregadas.Count);
        var resolucion = agregadas[0];
        var certificacion = agregadas.Single(p => p.DerivadaDePropuestaId == resolucion.Id);
        var parrafosResolucion = resolucion.ContenidoPropuesto.Bloques.Sum(b => b.Parrafos.Count);
        Assert.True(parrafosResolucion > 0);
        Assert.Equal(parrafosResolucion, certificacion.ContenidoPropuesto.Bloques.Sum(b => b.Parrafos.Count));
    }
}
