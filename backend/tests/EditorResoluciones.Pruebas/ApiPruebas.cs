using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Pruebas;

/// <summary>Pruebas de integración de la API REST sobre una copia de los datos de prueba.</summary>
public sealed class ApiPruebas : IDisposable
{
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public ApiPruebas()
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

    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;

    private async Task<T> Leer<T>(string url) =>
        (await _cliente.GetFromJsonAsync<T>(url, Json))!;

    private static Plantilla NuevaPlantilla(string subRubroId = "SUB-002", string rubroId = "RUB-001") => new()
    {
        Nombre = "Plantilla creada en prueba",
        RubroId = rubroId,
        SubRubroId = subRubroId,
        TipoDocumentoId = "TDO-003",
        ClaseDocumentoId = "CDO-001",
    };

    [Fact]
    public async Task Consulta_las_tres_listas_del_contexto()
    {
        Assert.Equal(3, (await Leer<List<Rubro>>("/api/rubros")).Count);
        Assert.Equal(3, (await Leer<List<TipoDocumento>>("/api/tipos-documento")).Count);

        var subRubros = await Leer<List<SubRubro>>("/api/subrubros?rubroId=RUB-001");
        Assert.Equal(2, subRubros.Count);
        Assert.All(subRubros, s => Assert.Equal("RUB-001", s.RubroId));
    }

    [Fact]
    public async Task Filtra_parrafos_por_plantilla_y_seccion_en_orden()
    {
        var parrafos = await Leer<List<Parrafo>>("/api/parrafos?plantillaId=PLA-001&seccion=considerando");

        Assert.Equal(["PAR-002", "PAR-003", "PAR-004"], parrafos.Select(p => p.Id));
    }

    [Fact]
    public async Task Crear_plantilla_persiste_tras_reiniciar_y_queda_en_el_historial()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/plantillas", NuevaPlantilla(), Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await respuesta.Content.ReadFromJsonAsync<Plantilla>(Json))!;
        Assert.StartsWith("PLA-", creada.Id);
        Assert.Equal(1, creada.Version);

        // Una instancia nueva de la API sobre la misma carpeta equivale a reiniciar la aplicación.
        await using var reiniciada = new FabricaApi(_datos.Ruta);
        using var cliente = reiniciada.CreateClient();
        var leida = await cliente.GetFromJsonAsync<Plantilla>($"/api/plantillas/{creada.Id}", Json);
        Assert.Equal("Plantilla creada en prueba", leida!.Nombre);

        var historial = await cliente.GetFromJsonAsync<List<RegistroHistorial>>($"/api/historial?entidadId={creada.Id}", Json);
        var registro = Assert.Single(historial!);
        Assert.Equal(AccionHistorial.Creacion, registro.Accion);
        Assert.Equal("usuario.prueba", registro.Usuario);
    }

    [Fact]
    public async Task Rechaza_plantilla_repetida_para_la_misma_combinacion()
    {
        var repetida = NuevaPlantilla("SUB-001");
        repetida.TipoDocumentoId = "TDO-001"; // Ya existe PLA-001 para RUB-001 / SUB-001 / TDO-001 / Original.

        var respuesta = await _cliente.PostAsJsonAsync("/api/plantillas", repetida, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Ya existe una plantilla para esta combinación", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_lista_Documento_forma_parte_de_la_combinacion_de_la_plantilla()
    {
        var clases = await Leer<List<ClaseDocumento>>("/api/clases-documento");
        Assert.Equal(["Original", "Modificación", "Aclaratoria"], clases.Select(c => c.Nombre));

        // PLA-001 es RUB-001 / SUB-001 / TDO-001 / Original: la misma combinación como Modificación sí se permite.
        var modificacion = NuevaPlantilla("SUB-001");
        modificacion.TipoDocumentoId = "TDO-001";
        modificacion.ClaseDocumentoId = "CDO-002";
        Assert.Equal(HttpStatusCode.Created, (await _cliente.PostAsJsonAsync("/api/plantillas", modificacion, Json)).StatusCode);

        var filtradas = await Leer<List<Plantilla>>("/api/plantillas?rubroId=RUB-001&subRubroId=SUB-001&tipoDocumentoId=TDO-001&claseDocumentoId=CDO-002");
        Assert.Equal("CDO-002", Assert.Single(filtradas).ClaseDocumentoId);

        var sinClase = NuevaPlantilla();
        sinClase.ClaseDocumentoId = "CDO-NOEXISTE";
        var respuesta = await _cliente.PostAsJsonAsync("/api/plantillas", sinClase, Json);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("claseDocumentoId", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rechaza_sub_rubro_que_no_pertenece_al_rubro()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/plantillas", NuevaPlantilla("SUB-005", "RUB-001"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("no pertenece al rubro", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Actualizar_con_version_desactualizada_devuelve_409()
    {
        var parrafo = await Leer<Parrafo>("/api/parrafos/PAR-003");
        parrafo.Orden = 5;
        var primera = await _cliente.PutAsJsonAsync("/api/parrafos/PAR-003", parrafo, Json);
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(2, (await primera.Content.ReadFromJsonAsync<Parrafo>(Json))!.Version);

        // Se reenvía la versión 1: alguien más ya guardó la versión 2.
        var segunda = await _cliente.PutAsJsonAsync("/api/parrafos/PAR-003", parrafo, Json);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal(5, (await Leer<Parrafo>("/api/parrafos/PAR-003")).Orden);
    }

    [Fact]
    public async Task Editar_un_parrafo_no_altera_los_demas()
    {
        var antes = await _cliente.GetStringAsync("/api/parrafos?plantillaId=PLA-001&seccion=Resuelve");
        var parrafo = await Leer<Parrafo>("/api/parrafos/PAR-003");
        parrafo.Activo = false;

        (await _cliente.PutAsJsonAsync("/api/parrafos/PAR-003", parrafo, Json)).EnsureSuccessStatusCode();

        Assert.Equal(antes, await _cliente.GetStringAsync("/api/parrafos?plantillaId=PLA-001&seccion=Resuelve"));
    }

    [Fact]
    public async Task Eliminar_valida_referencias_y_version()
    {
        var conHijos = await _cliente.DeleteAsync("/api/rubros/RUB-001");
        Assert.Equal(HttpStatusCode.BadRequest, conHijos.StatusCode);
        Assert.Contains("sub rubros", await conHijos.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.DeleteAsync("/api/parrafos/PAR-006?version=9")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _cliente.DeleteAsync("/api/parrafos/PAR-006?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/parrafos/PAR-006")).StatusCode);
    }

    [Fact]
    public async Task Rechaza_campos_con_origen_no_permitido()
    {
        var campo = new Campo
        {
            Id = "clave_secreta",
            Etiqueta = "Intento",
            Categoria = "Prueba",
            Origen = new OrigenCampo { Coleccion = "historial", Propiedad = "usuario" },
            Contextos = ["considerando"],
        };

        var respuesta = await _cliente.PostAsJsonAsync("/api/campos", campo, Json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("no está permitida", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rechaza_parrafos_con_referencias_inexistentes_o_datos_nulos()
    {
        const string referencias = """
            {
              "plantillaId": "PLA-NOEXISTE",
              "seccion": "Considerando",
              "contenido": { "type": "doc", "content": [ { "type": "paragraph", "content": [
                { "type": "campo", "attrs": { "campoId": "campo_inventado" } } ] } ] },
              "condicion": { "operadorLogico": "Y", "grupos": [],
                "reglas": [ { "campoId": "otro_inventado", "operador": "IgualA", "valor": "x" } ] }
            }
            """;
        var respuesta = await _cliente.PostAsync("/api/parrafos", new StringContent(referencias, Encoding.UTF8, "application/json"));
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("PLA-NOEXISTE", texto);
        Assert.Contains("campo_inventado", texto);
        Assert.Contains("otro_inventado", texto);

        const string conNulos = """{ "plantillaId": "PLA-001", "seccion": "Considerando", "contenido": { "type": "doc" }, "camposUsados": null }""";
        var nulos = await _cliente.PostAsync("/api/parrafos", new StringContent(conNulos, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, nulos.StatusCode);
    }

    [Theory]
    [InlineData("""{ "nombre": 123, "rubroId": "RUB-001" }""")]
    [InlineData("{ esto no es json")]
    [InlineData("")]
    public async Task Json_mal_formado_devuelve_errores_en_espanol(string cuerpo)
    {
        var respuesta = await _cliente.PostAsync("/api/plantillas", new StringContent(cuerpo, Encoding.UTF8, "application/json"));
        var texto = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Los datos enviados no son válidos.", texto);
        Assert.DoesNotContain("The JSON value", texto);
        Assert.DoesNotContain("field is required", texto);
    }

    [Fact]
    public async Task Archivo_danado_devuelve_error_claro_y_aparece_en_el_diagnostico()
    {
        await File.WriteAllTextAsync(_datos.Archivo("campos.json"), "{ esto no es json");

        var respuesta = await _cliente.GetAsync("/api/campos");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Contains("dañado", await respuesta.Content.ReadAsStringAsync());

        using var salud = JsonDocument.Parse(await _cliente.GetStringAsync("/api/salud"));
        Assert.Equal("con errores", salud.RootElement.GetProperty("estado").GetString());

        // Las demás colecciones siguen funcionando.
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync("/api/rubros")).StatusCode);
    }

    [Fact]
    public async Task Diagnostico_informa_todas_las_colecciones_disponibles()
    {
        using var salud = JsonDocument.Parse(await _cliente.GetStringAsync("/api/salud"));

        Assert.Equal("correcto", salud.RootElement.GetProperty("estado").GetString());
        Assert.Equal(11, salud.RootElement.GetProperty("colecciones").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/rubros/..%2F..%2Fappsettings.json")]
    [InlineData("/api/rubros/RUB-NOEXISTE")]
    public async Task Identificadores_desconocidos_o_manipulados_devuelven_404(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync(url)).StatusCode);
    }
}
