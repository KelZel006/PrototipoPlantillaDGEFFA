using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Pruebas;

/// <summary>Pruebas de la sección Considerando: contenido del editor, condiciones, orden y duplicación.</summary>
public sealed class ParrafosPruebas : IDisposable
{
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;

    public ParrafosPruebas()
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

    private static JsonObject CampoNodo(string id) => new() { ["type"] = "campo", ["attrs"] = new JsonObject { ["campoId"] = id } };

    private static JsonObject Documento(params JsonNode[] enLinea) => new()
    {
        ["type"] = "doc",
        ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(enLinea) }),
    };

    private static JsonObject Parrafo(JsonObject contenido, JsonObject? condicion = null)
    {
        var parrafo = new JsonObject
        {
            ["plantillaId"] = "PLA-001",
            ["seccion"] = "Considerando",
            ["contenido"] = contenido,
            ["mostrarSoloSiCumple"] = condicion is not null,
        };
        if (condicion is not null)
            parrafo["condicion"] = condicion;
        return parrafo;
    }

    private static JsonObject Condicion(string operadorLogico, params (string Campo, string Operador, string? Valor)[] reglas) => new()
    {
        ["operadorLogico"] = operadorLogico,
        ["grupos"] = new JsonArray(),
        ["reglas"] = new JsonArray(reglas
            .Select(r => (JsonNode)new JsonObject { ["campoId"] = r.Campo, ["operador"] = r.Operador, ["valor"] = r.Valor })
            .ToArray()),
    };

    private Task<HttpResponseMessage> Publicar(JsonNode cuerpo, string url = "/api/parrafos") =>
        _cliente.PostAsync(url, new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json"));

    private async Task<List<Parrafo>> Considerandos() =>
        (await _cliente.GetFromJsonAsync<List<Parrafo>>("/api/parrafos?plantillaId=PLA-001&seccion=Considerando", Json))!;

    [Fact]
    public async Task Nuevo_parrafo_va_al_final_y_el_servidor_calcula_los_campos_usados()
    {
        var cuerpo = Parrafo(Documento(Texto("Operador: "), CampoNodo("operador"), Texto(" y "), CampoNodo("operador")));
        cuerpo["camposUsados"] = new JsonArray("beneficiario"); // Valor del cliente que el servidor debe ignorar.

        var respuesta = await Publicar(cuerpo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = (await respuesta.Content.ReadFromJsonAsync<Parrafo>(Json))!;
        Assert.Equal(4, creado.Orden);
        Assert.Equal(["operador"], creado.CamposUsados);
    }

    public static TheoryData<string, string> ContenidosRechazados => new()
    {
        { """{"type":"doc","content":[{"type":"image","attrs":{"src":"x.png"}}]}""", "'image' no está permitido" },
        { """{"type":"doc","content":[{"type":"paragraph","attrs":{"style":"color:red"},"content":[{"type":"text","text":"a"}]}]}""", "'style' no está permitido" },
        { """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"a","marks":[{"type":"textStyle"}]}]}]}""", "'textStyle' no está permitido" },
        { """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"a","marks":[{"type":"link","attrs":{"href":"javascript:alert(1)"}}]}]}]}""", "http, https o mailto" },
        { """{"type":"doc","content":[{"type":"paragraph","attrs":{"textAlign":"diagonal"},"content":[{"type":"text","text":"a"}]}]}""", "alineación" },
        { """{"type":"doc","content":[{"type":"paragraph"}]}""", "no puede estar vacío" },
        { """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"campo","attrs":{"campoId":"codigo_arancelario"}}]}]}""", "es de tipo lista y solo se puede usar dentro de una tabla" },
    };

    [Theory]
    [MemberData(nameof(ContenidosRechazados))]
    public async Task Rechaza_contenido_no_permitido(string contenido, string mensajeEsperado)
    {
        var respuesta = await Publicar(Parrafo(JsonNode.Parse(contenido)!.AsObject()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensajeEsperado, await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Acepta_formato_en_campos_pero_no_en_bloques()
    {
        var campoEnNegrita = CampoNodo("numero_solicitud");
        campoEnNegrita["marks"] = new JsonArray(new JsonObject { ["type"] = "bold" });
        var aceptado = await Publicar(Parrafo(Documento(Texto("Solicitud "), campoEnNegrita)));
        Assert.Equal(HttpStatusCode.Created, aceptado.StatusCode);

        const string parrafoConMarca = """{"type":"doc","content":[{"type":"paragraph","marks":[{"type":"bold"}],"content":[{"type":"text","text":"a"}]}]}""";
        var rechazado = await Publicar(Parrafo(JsonNode.Parse(parrafoConMarca)!.AsObject()));
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.StatusCode);
    }

    [Fact]
    public async Task Limita_el_parrafo_a_2000_caracteres_contando_los_campos_como_etiqueta()
    {
        // "{Operador}" cuenta 10 caracteres: 1990 + 10 = 2000 se acepta; 1991 + 10 se rechaza.
        var justo = await Publicar(Parrafo(Documento(Texto(new string('a', 1990)), CampoNodo("operador"))));
        var excedido = await Publicar(Parrafo(Documento(Texto(new string('a', 1991)), CampoNodo("operador"))));

        Assert.Equal(HttpStatusCode.Created, justo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, excedido.StatusCode);
        Assert.Contains("2001 caracteres", await excedido.Content.ReadAsStringAsync());
    }

    public static TheoryData<string, string, string?, string> CondicionesRechazadas => new()
    {
        // Campo, operador, valor, mensaje esperado.
        { "codigo_arancelario", "IgualA", "x", "no se puede usar en esta condición" },
        { "monto_exoneracion", "Contiene", "100", "no es válido para" },
        { "monto_exoneracion", "MayorQue", "cien mil", "no es válido para" },
        { "fecha_resolucion", "MenorQue", "01/10/2026", "AAAA-MM-DD" },
        { "estado_solicitud", "IgualA", null, "necesita un valor" },
    };

    [Theory]
    [MemberData(nameof(CondicionesRechazadas))]
    public async Task Rechaza_condiciones_incompatibles(string campo, string operador, string? valor, string mensajeEsperado)
    {
        var condicion = Condicion("Y", (campo, operador, valor));

        var respuesta = await Publicar(Parrafo(Documento(Texto("Texto")), condicion));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensajeEsperado, await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Acepta_condiciones_validas_con_grupos_y_operadores_de_nulidad()
    {
        var condicion = Condicion("O", ("estado_solicitud", "IgualA", "En Curso"), ("fecha_resolucion", "EsNulo", null));
        condicion["grupos"] = new JsonArray(Condicion("Y",
            ("monto_exoneracion", "MayorOIgualQue", "50000.50"),
            ("fecha_resolucion", "MenorQue", "2026-12-31")));

        var respuesta = await Publicar(Parrafo(Documento(Texto("Con grupos")), condicion));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = (await respuesta.Content.ReadFromJsonAsync<Parrafo>(Json))!;
        Assert.Single(creado.Condicion!.Grupos);
    }

    [Fact]
    public async Task Rechaza_grupos_vacios_y_guarda_sin_condicion_si_no_hay_reglas()
    {
        var conGrupoVacio = Condicion("Y", ("estado_solicitud", "IgualA", "En Curso"));
        conGrupoVacio["grupos"] = new JsonArray(Condicion("O"));
        var rechazada = await Publicar(Parrafo(Documento(Texto("x")), conGrupoVacio));
        Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
        Assert.Contains("grupo de condiciones sin reglas", await rechazada.Content.ReadAsStringAsync());

        var sinReglas = await Publicar(Parrafo(Documento(Texto("x")), Condicion("Y")));
        Assert.Equal(HttpStatusCode.Created, sinReglas.StatusCode);
        Assert.Null((await sinReglas.Content.ReadFromJsonAsync<Parrafo>(Json))!.Condicion);
    }

    [Fact]
    public async Task Reordena_la_seccion_en_una_sola_operacion()
    {
        var actuales = await Considerandos();
        var invertido = actuales.AsEnumerable().Reverse().Select(p => new ElementoOrden(p.Id, p.Version)).ToList();

        var respuesta = await _cliente.PostAsJsonAsync("/api/parrafos/reordenar",
            new SolicitudReorden("PLA-001", Seccion.Considerando, invertido), Json);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var nuevos = await Considerandos();
        Assert.Equal(["PAR-004", "PAR-003", "PAR-002"], nuevos.Select(p => p.Id));
        Assert.Equal([1, 2, 3], nuevos.Select(p => p.Orden));
        // PAR-003 ya estaba en la posición 2: no se modifica ni cambia de versión.
        Assert.Equal(1, nuevos.Single(p => p.Id == "PAR-003").Version);
    }

    [Fact]
    public async Task Rechaza_reordenar_con_datos_desactualizados_sin_cambiar_nada()
    {
        var actuales = await Considerandos();
        var antes = await _cliente.GetStringAsync("/api/parrafos?plantillaId=PLA-001");

        var versionVieja = actuales.AsEnumerable().Reverse().Select(p => new ElementoOrden(p.Id, p.Id == "PAR-002" ? 7 : p.Version)).ToList();
        var incompleta = actuales.Skip(1).Select(p => new ElementoOrden(p.Id, p.Version)).ToList();

        var r1 = await _cliente.PostAsJsonAsync("/api/parrafos/reordenar", new SolicitudReorden("PLA-001", Seccion.Considerando, versionVieja), Json);
        var r2 = await _cliente.PostAsJsonAsync("/api/parrafos/reordenar", new SolicitudReorden("PLA-001", Seccion.Considerando, incompleta), Json);

        Assert.Equal(HttpStatusCode.Conflict, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
        Assert.Equal(antes, await _cliente.GetStringAsync("/api/parrafos?plantillaId=PLA-001"));
    }

    [Fact]
    public async Task Duplicar_inserta_la_copia_debajo_con_su_condicion()
    {
        var respuesta = await _cliente.PostAsync("/api/parrafos/PAR-002/duplicar", null);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var copia = (await respuesta.Content.ReadFromJsonAsync<Parrafo>(Json))!;
        Assert.NotEqual("PAR-002", copia.Id);
        Assert.Equal(1, copia.Version);
        Assert.Equal("estado_solicitud", copia.Condicion!.Reglas[0].CampoId);

        var orden = (await Considerandos()).Select(p => p.Id).ToList();
        Assert.Equal(["PAR-002", copia.Id, "PAR-003", "PAR-004"], orden);
        Assert.Equal([1, 2, 3, 4], (await Considerandos()).Select(p => p.Orden));

        // Historial: la creación de la copia y el desplazamiento de los dos párrafos siguientes.
        var historial = await _cliente.GetFromJsonAsync<List<RegistroHistorial>>("/api/historial?coleccion=parrafos", Json);
        Assert.Equal(3, historial!.Count);
        Assert.Single(historial, r => r.Accion == AccionHistorial.Creacion && r.EntidadId == copia.Id);

        // Editar la condición de la copia no altera la del original.
        copia.Condicion.Reglas[0].Valor = "Aprobada";
        (await _cliente.PutAsJsonAsync($"/api/parrafos/{copia.Id}", copia, Json)).EnsureSuccessStatusCode();
        var original = await _cliente.GetFromJsonAsync<Parrafo>("/api/parrafos/PAR-002", Json);
        Assert.Equal("En Curso", original!.Condicion!.Reglas[0].Valor);
    }

    [Fact]
    public async Task Informa_operadores_por_tipo_y_valores_de_prueba_de_un_campo()
    {
        var operadores = await _cliente.GetFromJsonAsync<Dictionary<TipoDato, OperadorComparacion[]>>("/api/campos/operadores", Json);
        Assert.Contains(OperadorComparacion.Contiene, operadores![TipoDato.Texto]);
        Assert.DoesNotContain(OperadorComparacion.Contiene, operadores[TipoDato.Moneda]);
        Assert.Contains(OperadorComparacion.MayorQue, operadores[TipoDato.Fecha]);

        var estados = await _cliente.GetFromJsonAsync<List<string>>("/api/campos/estado_solicitud/valores", Json);
        Assert.Equal(["Aprobada", "En Curso", "En Revisión"], estados);

        var fechas = await _cliente.GetFromJsonAsync<List<string>>("/api/campos/fecha_resolucion/valores", Json);
        Assert.Contains("2026-10-01", fechas!);
    }

    [Fact]
    public async Task No_permite_eliminar_un_campo_usado_en_una_condicion()
    {
        var respuesta = await _cliente.DeleteAsync("/api/campos/estado_solicitud");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("condiciones de párrafos", await respuesta.Content.ReadAsStringAsync());
    }
}
