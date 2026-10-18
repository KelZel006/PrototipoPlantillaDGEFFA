using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// «Unir con el párrafo anterior»: un texto de más de 2000 caracteres se escribe en dos párrafos y el documento los
/// muestra como uno solo, sin numeración propia para el segundo.
/// </summary>
public sealed class UnionParrafosPruebas : IAsyncLifetime, IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private string _propuesta = "";
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public UnionParrafosPruebas()
    {
        _fabrica = new FabricaApi(_datos.Ruta);
        _cliente = _fabrica.CreateClient();
    }

    public async Task InitializeAsync() => _propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).Single().Id;

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _cliente.Dispose();
        _fabrica.Dispose();
        _datos.Dispose();
    }

    private static NodoContenido Doc(params NodoContenido[] bloques) => new() { Tipo = "doc", Contenido = [.. bloques] };

    private static NodoContenido Parrafo(string texto) =>
        new() { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] };

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    /// <summary>Crea un considerando en la posición indicada (0 = al final).</summary>
    private async Task<ParrafoPropuesta> Considerando(string texto, int orden, bool unir)
    {
        var respuesta = await _cliente.PostAsJsonAsync($"/api/propuestas/{_propuesta}/parrafos",
            new DatosParrafo(CodigosBloque.Considerando, orden, Doc(Parrafo(texto)), false, null, 0, UnirConAnterior: unir), Json);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ParrafoPropuesta>(Json))!;
    }

    private async Task<SeccionDocumento> Considerandos(string? solicitudId)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var propuesta = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(_propuesta))!;
        var (plantilla, bloques) = ConstructorContenido.ADocumento(propuesta.ContenidoPropuesto, propuesta.Id, propuesta.Nombre,
            propuesta.RubroId, propuesta.SubRubroId, propuesta.TipoDocumentoId, propuesta.ClaseDocumentoId);
        var documento = await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync(plantilla, bloques, solicitudId);
        return documento.Secciones.Single(s => s.Codigo == CodigosBloque.Considerando);
    }

    [Fact]
    public void Unir_funde_el_ultimo_parrafo_con_el_primero_del_siguiente_separados_por_un_espacio()
    {
        var unido = ServicioDocumento.Unir(Doc(Parrafo("Primera parte.")), Doc(Parrafo("Segunda parte.")));
        Assert.Equal("Primera parte. Segunda parte.", Texto(Assert.Single(unido.Contenido!)));

        // Si ya hay un espacio, no se agrega otro.
        var conEspacio = ServicioDocumento.Unir(Doc(Parrafo("Primera parte, ")), Doc(Parrafo("segunda parte.")));
        Assert.Equal("Primera parte, segunda parte.", Texto(Assert.Single(conEspacio.Contenido!)));

        // Una tabla al inicio del siguiente se agrega a continuación, sin fundirse.
        var tabla = new NodoContenido { Tipo = "table", Contenido = [] };
        var conTabla = ServicioDocumento.Unir(Doc(Parrafo("Texto.")), Doc(tabla, Parrafo("Después.")));
        Assert.Equal(["paragraph", "table", "paragraph"], conTabla.Contenido!.Select(b => b.Tipo));
    }

    [Fact]
    public async Task Dos_parrafos_unidos_se_muestran_como_uno_solo_con_un_solo_numero()
    {
        // Se inserta en la posición 2: continúa PAR-002 (CONSIDERANDO 1).
        var continuacion = await Considerando("Continuación del primer considerando.", 2, unir: true);
        Assert.True(continuacion.UnirConAnterior);

        var considerandos = (await Considerandos("SOL-001")).Parrafos;

        Assert.Equal(["PAR-002", "PAR-003", "PAR-004"], considerandos.Select(p => p.Id));
        var primero = Texto(considerandos[0].Contenido);
        Assert.StartsWith("CONSIDERANDO(1): Que mediante resolución", primero);
        Assert.EndsWith(" Continuación del primer considerando.", primero);
        Assert.Single(considerandos[0].Contenido.Contenido!);
        Assert.StartsWith("CONSIDERANDO(2): ", Texto(considerandos[1].Contenido));
        Assert.StartsWith("CONSIDERANDO(3): ", Texto(considerandos[2].Contenido));
    }

    [Fact]
    public async Task Un_texto_de_3600_caracteres_en_dos_parrafos_queda_en_un_solo_parrafo_del_documento()
    {
        var parte1 = new string('a', 1990);
        var parte2 = new string('b', 1610);
        await Considerando(parte1, 0, unir: false);
        await Considerando(parte2, 0, unir: true);

        var ultimo = (await Considerandos(null)).Parrafos[^1];

        Assert.Equal($"CONSIDERANDO(4): {parte1} {parte2}", Texto(Assert.Single(ultimo.Contenido.Contenido!)));
    }

    [Fact]
    public async Task Si_el_parrafo_anterior_no_se_incluye_su_continuacion_tampoco()
    {
        // PAR-004 tiene una condición que SOL-002 no cumple; su continuación sigue la misma suerte.
        var continuacion = await Considerando("Continuación de PAR-004.", 0, unir: true);

        var seccion = await Considerandos("SOL-002");

        Assert.Equal(["PAR-003"], seccion.Parrafos.Select(p => p.Id));
        Assert.Equal("Continúa un párrafo que no se incluyó.", seccion.Omitidos.Single(o => o.Id == continuacion.Id).Motivo);
    }

    [Fact]
    public async Task El_primer_parrafo_de_la_seccion_no_tiene_con_que_unirse_y_se_muestra_solo()
    {
        await Considerando("Va primero aunque esté marcado.", 1, unir: true);

        var considerandos = (await Considerandos("SOL-001")).Parrafos;

        Assert.Equal(4, considerandos.Count);
        Assert.Equal("CONSIDERANDO(1): Va primero aunque esté marcado.", Texto(considerandos[0].Contenido));
    }

    [Fact]
    public async Task Unir_o_separar_queda_como_ajuste_y_en_la_comparacion()
    {
        var par003 = (await _cliente.GetFromJsonAsync<List<ParrafoPropuesta>>($"/api/propuestas/{_propuesta}/parrafos", Json))!
            .Single(p => p.Id == "PAR-003");
        (await _cliente.PutAsJsonAsync($"/api/propuestas/{_propuesta}/parrafos/PAR-003",
            new DatosParrafo(par003.Bloque, par003.Orden, par003.Contenido, par003.MostrarSoloSiCumple, par003.Condicion, par003.Version,
                par003.Activo, UnirConAnterior: true), Json)).EnsureSuccessStatusCode();

        var ajustes = await _cliente.GetStringAsync($"/api/propuestas/{_propuesta}/ajustes");
        Assert.Contains("CONSIDERANDO 2: se unió con el párrafo anterior", ajustes);
    }

    [Fact]
    public void La_marca_apagada_no_cambia_la_huella_de_los_contenidos_ya_publicados()
    {
        var parrafo = new ParrafoContenido { Id = "PAR-1", Contenido = Doc(Parrafo("Texto")) };
        Assert.DoesNotContain("unirConAnterior", JsonSerializer.Serialize(parrafo, Json));

        parrafo.UnirConAnterior = true;
        Assert.Contains("\"unirConAnterior\": true", JsonSerializer.Serialize(parrafo, Json));
    }
}

/// <summary>La marca pasa de la Resolución a su Certificación derivada, como los demás datos del párrafo (D3).</summary>
public sealed class UnionParrafosCertificacionPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public UnionParrafosCertificacionPruebas()
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
    public async Task La_Certificacion_copia_la_union_de_la_Resolucion()
    {
        var agregadas = await Consolidados.ModificarAsync(_cliente, "PLA-001");
        var resolucion = agregadas.Single(p => p.PlantillaId == "PLA-001");
        var certificacion = agregadas.Single(p => p.PlantillaId == "PLA-004");
        var contenido = new NodoContenido
        {
            Tipo = "doc",
            Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = "Continúa la resolución." }] }],
        };

        (await _cliente.PostAsJsonAsync($"/api/propuestas/{resolucion.Id}/parrafos",
            new DatosParrafo(CodigosBloque.Considerando, 0, contenido, false, null, 0, UnirConAnterior: true), Json)).EnsureSuccessStatusCode();

        var copia = (await _cliente.GetFromJsonAsync<List<ParrafoPropuesta>>($"/api/propuestas/{certificacion.Id}/parrafos", Json))!
            .Single(p => p.Contenido.Contenido![0].Contenido![0].Texto == "Continúa la certificación.");
        Assert.True(copia.UnirConAnterior);
    }
}
