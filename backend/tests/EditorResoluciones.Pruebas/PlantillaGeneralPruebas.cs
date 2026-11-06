using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// Plantillas que solo indican algunas listas: Rubro y Sub Rubro son obligatorios, y a cada registro le corresponde la
/// plantilla más específica que coincida.
/// </summary>
public sealed class PlantillaGeneralPruebas : IDisposable
{
    private const string Aclaratoria = "CDO-003";
    private const string Isv = "BEN-001";
    private const string Isr = "BEN-002";

    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public PlantillaGeneralPruebas()
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

    private static PlantillaVigente Vigente(string id, string tipo = "", string clase = "", string beneficio = "", string subRubro = "1801") =>
        new(id, id, "exoneraciones", "1800", subRubro, tipo, "", clase, beneficio, "", "V", 1, DateTimeOffset.UnixEpoch, null);

    private static ConsultaPlantillaAplicable Registro(string? tipo = null, string? clase = Aclaratoria, string? beneficio = Isv) =>
        new("exoneraciones", "1800", "1801", tipo, clase, beneficio);

    [Theory]
    [InlineData(Isv, "ESPECIFICA")]
    [InlineData(Isr, "GENERAL")]
    public void Manda_la_plantilla_mas_especifica_que_coincide(string beneficio, string esperada)
    {
        var general = Vigente("GENERAL", clase: Aclaratoria);
        var especifica = Vigente("ESPECIFICA", clase: Aclaratoria, beneficio: Isv);

        Assert.Equal(esperada, ReglaPlantillaAplicable.Elegir([general, especifica], Registro(beneficio: beneficio))?.PlantillaId);
    }

    [Fact]
    public void Una_lista_de_la_plantilla_distinta_a_la_del_registro_la_descarta()
    {
        Assert.Null(ReglaPlantillaAplicable.Elegir([Vigente("ISV", beneficio: Isv)], Registro(beneficio: Isr)));
        Assert.Null(ReglaPlantillaAplicable.Elegir([Vigente("ISV", beneficio: Isv)], Registro(beneficio: null)));
        Assert.Null(ReglaPlantillaAplicable.Elegir([Vigente("OTRO", subRubro: "1802")], Registro()));
    }

    [Fact]
    public void Si_empatan_gana_la_lista_de_mas_a_la_izquierda()
    {
        var porBeneficio = Vigente("BENEFICIO", beneficio: Isv);
        var porDocumento = Vigente("DOCUMENTO", clase: Aclaratoria);

        Assert.Equal("DOCUMENTO", ReglaPlantillaAplicable.Elegir([porBeneficio, porDocumento], Registro())?.PlantillaId);
    }

    [Theory]
    [InlineData(Isv, "PLA-006")]
    [InlineData(Isr, "PLA-005")]
    public async Task La_API_responde_la_plantilla_que_corresponde_al_registro(string beneficio, string esperada)
    {
        var vigente = await _cliente.GetFromJsonAsync<PlantillaVigente>(
            $"/api/plantillas-vigentes/aplicable?unidadId=exoneraciones&rubroId=1800&subRubroId=1801&claseDocumentoId={Aclaratoria}&beneficioId={beneficio}", Json);

        Assert.Equal(esperada, vigente?.PlantillaId);
    }

    [Fact]
    public async Task Sin_plantilla_que_coincida_la_API_responde_sin_contenido()
    {
        var respuesta = await _cliente.GetAsync("/api/plantillas-vigentes/aplicable?unidadId=exoneraciones&rubroId=1800&subRubroId=1802");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Solo_Rubro_y_Sub_Rubro_son_obligatorios()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var url = $"/api/consolidados/{consolidado}/propuestas/crear";

        var creada = await _cliente.PostAsJsonAsync(url, new SolicitudCrearPlantilla("1800", "1802", null, null, null, null), Json);
        creada.EnsureSuccessStatusCode();
        var propuesta = Assert.Single((await creada.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!);
        Assert.StartsWith("Plantilla – ", propuesta.Nombre);
        Assert.Equal("", propuesta.BeneficioId);
        // La misma combinación general no se repite, pero sí una más específica.
        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsJsonAsync(url, new SolicitudCrearPlantilla("1800", "1802", null, null, null, null), Json)).StatusCode);
        (await _cliente.PostAsJsonAsync(url, new SolicitudCrearPlantilla("1800", "1802", null, Aclaratoria, null, null), Json)).EnsureSuccessStatusCode();

        var sinSubRubro = await _cliente.PostAsJsonAsync(url, new SolicitudCrearPlantilla("1800", "", null, null, null, null), Json);
        Assert.Equal(HttpStatusCode.BadRequest, sinSubRubro.StatusCode);
    }
}
