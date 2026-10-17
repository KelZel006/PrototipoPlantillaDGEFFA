using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

public class EvaluadorCondicionesPruebas
{
    private static readonly Dictionary<string, Campo> Campos = new()
    {
        ["estado"] = new Campo { Id = "estado", Etiqueta = "Estado", TipoDato = TipoDato.Texto },
        ["monto"] = new Campo { Id = "monto", Etiqueta = "Monto", TipoDato = TipoDato.Moneda },
        ["fecha"] = new Campo { Id = "fecha", Etiqueta = "Fecha", TipoDato = TipoDato.Fecha },
    };

    private static Func<string, (Campo?, object?)> Datos(object? estado, object? monto, object? fecha) =>
        id => (Campos.GetValueOrDefault(id), id switch { "estado" => estado, "monto" => monto, "fecha" => fecha, _ => null });

    private static ReglaCondicion Regla(string campo, OperadorComparacion operador, string? valor = null) =>
        new() { CampoId = campo, Operador = operador, Valor = valor };

    [Theory]
    [InlineData("En Curso", OperadorComparacion.IgualA, "en curso", ResultadoCondicion.Verdadero)]
    [InlineData("En Curso", OperadorComparacion.DistintoDe, "En Curso", ResultadoCondicion.Falso)]
    [InlineData("Exoneración de importación", OperadorComparacion.Contiene, "IMPORTACIÓN", ResultadoCondicion.Verdadero)]
    [InlineData("Exoneración de ventas", OperadorComparacion.NoContiene, "importación", ResultadoCondicion.Verdadero)]
    [InlineData(null, OperadorComparacion.IgualA, "En Curso", ResultadoCondicion.Indeterminado)]
    [InlineData(null, OperadorComparacion.EsNulo, null, ResultadoCondicion.Verdadero)]
    [InlineData("  ", OperadorComparacion.EsNulo, null, ResultadoCondicion.Verdadero)]
    [InlineData("x", OperadorComparacion.NoEsNulo, null, ResultadoCondicion.Verdadero)]
    public void Compara_textos_sin_distinguir_mayusculas(string? valor, OperadorComparacion operador, string? esperado, ResultadoCondicion resultado)
    {
        Assert.Equal(resultado, EvaluadorCondiciones.EvaluarRegla(Regla("estado", operador, esperado), Datos(valor, null, null)));
    }

    [Fact]
    public void Compara_montos_y_fechas()
    {
        var datos = Datos(null, 250000.00m, new DateOnly(2026, 10, 1));
        Assert.Equal(ResultadoCondicion.Verdadero, EvaluadorCondiciones.EvaluarRegla(Regla("monto", OperadorComparacion.MayorQue, "100000"), datos));
        Assert.Equal(ResultadoCondicion.Verdadero, EvaluadorCondiciones.EvaluarRegla(Regla("monto", OperadorComparacion.MenorOIgualQue, "250000.00"), datos));
        Assert.Equal(ResultadoCondicion.Falso, EvaluadorCondiciones.EvaluarRegla(Regla("fecha", OperadorComparacion.MayorQue, "2026-12-31"), datos));
        Assert.Equal(ResultadoCondicion.Verdadero, EvaluadorCondiciones.EvaluarRegla(Regla("fecha", OperadorComparacion.IgualA, "2026-10-01"), datos));
    }

    [Theory]
    // Y: un falso decide; si no, un indeterminado impide afirmar que se cumple.
    [InlineData(OperadorLogico.Y, "En Curso", 250000, ResultadoCondicion.Verdadero)]
    [InlineData(OperadorLogico.Y, "Aprobada", null, ResultadoCondicion.Falso)]
    [InlineData(OperadorLogico.Y, "En Curso", null, ResultadoCondicion.Indeterminado)]
    // O: un verdadero decide; si no, un indeterminado impide afirmar que no se cumple.
    [InlineData(OperadorLogico.O, "En Curso", null, ResultadoCondicion.Verdadero)]
    [InlineData(OperadorLogico.O, "Aprobada", null, ResultadoCondicion.Indeterminado)]
    [InlineData(OperadorLogico.O, "Aprobada", 50000, ResultadoCondicion.Falso)]
    public void Combina_reglas_con_tres_resultados(OperadorLogico logico, string estado, int? monto, ResultadoCondicion resultado)
    {
        var grupo = new GrupoCondiciones
        {
            OperadorLogico = logico,
            Reglas = [Regla("estado", OperadorComparacion.IgualA, "En Curso"), Regla("monto", OperadorComparacion.MayorQue, "100000")],
        };

        Assert.Equal(resultado, EvaluadorCondiciones.Evaluar(grupo, Datos(estado, monto is null ? null : (decimal)monto, null)));
    }

    [Fact]
    public void Formatea_los_valores_para_el_documento()
    {
        Assert.Equal("L 250,000.00", FormatoValores.Formatear(TipoDato.Moneda, 250000m));
        Assert.Equal("1 de octubre de 2026", FormatoValores.Formatear(TipoDato.Fecha, new DateOnly(2026, 10, 1)));
        Assert.Equal("2026", FormatoValores.Formatear(TipoDato.Numero, 2026));
        Assert.Equal("Sí", FormatoValores.Formatear(TipoDato.Booleano, true));
        Assert.Null(FormatoValores.Formatear(TipoDato.Texto, "  "));
    }
}

/// <summary>Generación del documento y del PDF con los datos de prueba fijos.</summary>
/// <summary>Documento generado a partir de una propuesta que modifica PLA-001 (el mismo armado de la vista previa).</summary>
public sealed class DocumentoPruebas : IAsyncLifetime, IDisposable
{
    private string _propuesta = "";
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public DocumentoPruebas()
    {
        _fabrica = new FabricaApi(_datos.Ruta);
        _cliente = _fabrica.CreateClient();
    }

    public async Task InitializeAsync() => _propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).Single().Id;

    public Task DisposeAsync() => Task.CompletedTask;

    private string Ruta => $"/api/propuestas/{_propuesta}";

    public void Dispose()
    {
        _cliente.Dispose();
        _fabrica.Dispose();
        _datos.Dispose();
    }

    private async Task<DocumentoResuelto> Generar(string? solicitudId)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var propuesta = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(_propuesta))!;
        var (plantilla, parrafos) = ConstructorContenido.ADocumento(propuesta.ContenidoPropuesto, propuesta.Id, propuesta.Nombre,
            propuesta.RubroId, propuesta.SubRubroId, propuesta.TipoDocumentoId, propuesta.ClaseDocumentoId);
        return await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync(plantilla, parrafos, solicitudId);
    }

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    private static SeccionDocumento Seccion(DocumentoResuelto documento, Seccion seccion) =>
        documento.Secciones.Single(s => s.Seccion == seccion);

    [Fact]
    public async Task Sin_solicitud_incluye_los_parrafos_activos_en_el_orden_de_las_pestanas()
    {
        var documento = await Generar(null);

        Assert.Equal(
            [
                Dominio.Modelos.Seccion.Cabecera, Dominio.Modelos.Seccion.Considerando, Dominio.Modelos.Seccion.PorTanto,
                Dominio.Modelos.Seccion.Resuelve, Dominio.Modelos.Seccion.Firmas,
            ],
            documento.Secciones.Select(s => s.Seccion));
        Assert.Equal(["PAR-002", "PAR-003", "PAR-004"], Seccion(documento, Dominio.Modelos.Seccion.Considerando).Parrafos.Select(p => p.Id));

        var resuelve = Seccion(documento, Dominio.Modelos.Seccion.Resuelve);
        Assert.Equal(["PAR-005"], resuelve.Parrafos.Select(p => p.Id));
        Assert.Equal("Párrafo inactivo.", Assert.Single(resuelve.Omitidos).Motivo);

        Assert.Contains("{Operador}", Texto(Seccion(documento, Dominio.Modelos.Seccion.Considerando).Parrafos[0].Contenido));
    }

    [Fact]
    public async Task Con_solicitud_sustituye_los_campos_y_aplica_las_condiciones()
    {
        // SOL-001: En Curso y monto 250000, así que se cumplen las dos condiciones.
        var documento = await Generar("SOL-001");

        var considerandos = Seccion(documento, Dominio.Modelos.Seccion.Considerando).Parrafos;
        Assert.Equal(["PAR-002", "PAR-003", "PAR-004"], considerandos.Select(p => p.Id));
        var primero = Texto(considerandos[0].Contenido);
        Assert.Contains("Operador Ficticio Uno, S. de R.L. (prueba)", primero);
        Assert.Contains("PRUEBA-0001-2026", primero);
        Assert.DoesNotContain("{", primero);
        Assert.Contains("L 250,000.00", Texto(considerandos[2].Contenido));
        Assert.Contains("Distrito Central", Texto(Seccion(documento, Dominio.Modelos.Seccion.Resuelve).Parrafos[0].Contenido));
        Assert.Empty(documento.Advertencias);
    }

    [Fact]
    public void El_resumen_conserva_el_texto_y_muestra_campos_y_tablas()
    {
        var catalogo = new Dictionary<string, Campo> { ["beneficiario"] = new() { Id = "beneficiario", Etiqueta = "Beneficiario" } };
        var contenido = System.Text.Json.JsonSerializer.Deserialize<NodoContenido>(
            """
            {"type":"doc","content":[
              {"type":"paragraph","content":[{"type":"text","text":"esta es  una prueba para "},{"type":"campo","attrs":{"campoId":"beneficiario"}}]},
              {"type":"table","content":[{"type":"tableRow","content":[{"type":"tableCell","content":[{"type":"paragraph","content":[{"type":"text","text":"celda"}]}]}]}]}
            ]}
            """)!;

        Assert.Equal("esta es una prueba para {Beneficiario} [Tabla]",
            Dominio.Servicios.ContenidoEditor.TextoPlano(contenido, catalogo));
    }

    [Fact]
    public async Task Numera_los_considerandos_segun_aparecen_en_el_documento()
    {
        // Con SOL-001 se incluyen los tres considerandos; con SOL-002 solo PAR-003, que pasa a ser el número 1.
        var todos = Seccion(await Generar("SOL-001"), Dominio.Modelos.Seccion.Considerando).Parrafos;
        Assert.Equal(["CONSIDERANDO(1): ", "CONSIDERANDO(2): ", "CONSIDERANDO(3): "],
            todos.Select(p => p.Contenido.Contenido![0].Contenido![0].Texto));
        Assert.Equal("bold", todos[0].Contenido.Contenido![0].Contenido![0].Marcas![0].Tipo);
        Assert.StartsWith("CONSIDERANDO(1): Que mediante resolución", Texto(todos[0].Contenido));

        var unSolo = Seccion(await Generar("SOL-002"), Dominio.Modelos.Seccion.Considerando).Parrafos;
        Assert.StartsWith("CONSIDERANDO(1): Que la solicitud cumple", Texto(Assert.Single(unSolo).Contenido));

        // La cabecera no se numera.
        Assert.StartsWith("RESOLUCIÓN DE PRUEBA", Texto(Seccion(await Generar("SOL-001"), Dominio.Modelos.Seccion.Cabecera).Parrafos[0].Contenido));
    }

    [Fact]
    public async Task Numera_el_resuelve_con_ordinales_sin_contar_los_omitidos()
    {
        // PAR-005 está activo (orden 1) y PAR-006 inactivo (orden 2). Se agrega un tercero activo.
        var nuevo = new JsonObject
        {
            ["plantillaId"] = "PLA-001",
            ["bloque"] = "RESUELVE",
            ["contenido"] = new JsonObject
            {
                ["type"] = "doc",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(TextoNodo("Notificar al interesado.")) }),
            },
        };
        (await _cliente.PostAsync($"{Ruta}/parrafos", new StringContent(nuevo.ToJsonString(), Encoding.UTF8, "application/json"))).EnsureSuccessStatusCode();

        var resuelve = Seccion(await Generar("SOL-001"), Dominio.Modelos.Seccion.Resuelve).Parrafos;

        Assert.Equal(["PRIMERO: ", "SEGUNDO: "], resuelve.Select(p => p.Contenido.Contenido![0].Contenido![0].Texto));
        Assert.StartsWith("PRIMERO: Autorizar, en calidad de prueba", Texto(resuelve[0].Contenido));
        Assert.Equal("SEGUNDO: Notificar al interesado.", Texto(resuelve[1].Contenido));
    }

    [Fact]
    public async Task Por_tanto_y_firmas_van_en_su_lugar_sin_numeracion_y_admiten_campos()
    {
        foreach (var (seccion, texto) in new[] { ("FIRMAS", "Firma de "), ("POR_TANTO", "Con base en lo anterior para ") })
        {
            var parrafo = new JsonObject
            {
                ["plantillaId"] = "PLA-001",
                ["bloque"] = seccion,
                ["contenido"] = new JsonObject
                {
                    ["type"] = "doc",
                    ["content"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "paragraph",
                        ["content"] = new JsonArray(TextoNodo(texto), new JsonObject { ["type"] = "campo", ["attrs"] = new JsonObject { ["campoId"] = "beneficiario" } }),
                    }),
                },
            };
            var respuesta = await _cliente.PostAsync($"{Ruta}/parrafos", new StringContent(parrafo.ToJsonString(), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        var documento = await Generar("SOL-001");
        var orden = documento.Secciones.Where(s => s.Parrafos.Count > 0).Select(s => s.Seccion).ToList();

        Assert.Equal(
            [
                Dominio.Modelos.Seccion.Cabecera, Dominio.Modelos.Seccion.Considerando, Dominio.Modelos.Seccion.PorTanto,
                Dominio.Modelos.Seccion.Resuelve, Dominio.Modelos.Seccion.Firmas,
            ], orden);
        Assert.Equal("Con base en lo anterior para Beneficiario Ficticio Uno (prueba)",
            Texto(Seccion(documento, Dominio.Modelos.Seccion.PorTanto).Parrafos[0].Contenido));
        Assert.Equal("Firma de Beneficiario Ficticio Uno (prueba)",
            Texto(Seccion(documento, Dominio.Modelos.Seccion.Firmas).Parrafos[0].Contenido));

        var pdf = await _cliente.GetAsync($"{Ruta}/pdf?solicitudId=SOL-001");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
    }

    [Theory]
    [InlineData(1, "PRIMERO")]
    [InlineData(3, "TERCERO")]
    [InlineData(7, "SÉPTIMO")]
    [InlineData(10, "DÉCIMO")]
    [InlineData(11, "DÉCIMO PRIMERO")]
    [InlineData(19, "DÉCIMO NOVENO")]
    [InlineData(20, "VIGÉSIMO")]
    [InlineData(23, "VIGÉSIMO TERCERO")]
    [InlineData(99, "NONAGÉSIMO NOVENO")]
    [InlineData(100, "100°")]
    public void Escribe_los_ordinales_en_palabras(int numero, string esperado)
    {
        Assert.Equal(esperado, Numeracion.Ordinal(numero));
    }

    [Fact]
    public async Task Un_considerando_que_empieza_con_tabla_lleva_la_etiqueta_en_un_parrafo_propio()
    {
        var tabla = new JsonObject
        {
            ["type"] = "table",
            ["content"] = new JsonArray(Fila(Celda("tableCell", TextoNodo("celda")))),
        };
        (await PublicarParrafo(tabla)).EnsureSuccessStatusCode();

        var ultimo = Seccion(await Generar("SOL-001"), Dominio.Modelos.Seccion.Considerando).Parrafos.Last().Contenido;

        Assert.Equal(["paragraph", "table"], ultimo.Contenido!.Select(n => n.Tipo));
        Assert.Equal("CONSIDERANDO(4): ", ultimo.Contenido![0].Contenido![0].Texto);
    }

    [Fact]
    public async Task Omite_los_parrafos_cuya_condicion_no_se_cumple()
    {
        // SOL-002: En Revisión, monto 45000.50 y beneficio de ventas.
        var documento = await Generar("SOL-002");

        var considerando = Seccion(documento, Dominio.Modelos.Seccion.Considerando);
        Assert.Equal(["PAR-003"], considerando.Parrafos.Select(p => p.Id));
        Assert.All(considerando.Omitidos, o => Assert.StartsWith("No se cumple la condición", o.Motivo));
        Assert.Contains(considerando.Omitidos, o => o.Motivo.Contains("Estado de Solicitud igual a En Curso"));
    }

    [Fact]
    public async Task Con_datos_faltantes_no_da_la_condicion_por_cumplida_y_lo_advierte()
    {
        // SOL-004: sin monto, sin tipo de beneficio ni municipio, y de otro rubro.
        var documento = await Generar("SOL-004");

        var considerando = Seccion(documento, Dominio.Modelos.Seccion.Considerando);
        Assert.DoesNotContain(considerando.Parrafos, p => p.Id == "PAR-004");
        Assert.Contains(considerando.Omitidos, o => o.Id == "PAR-004" && o.Motivo.StartsWith("Faltan datos"));

        Assert.Contains("[sin dato: Municipio]", Texto(Seccion(documento, Dominio.Modelos.Seccion.Resuelve).Parrafos[0].Contenido));
        Assert.Contains(documento.Advertencias, a => a.Contains("faltan datos para evaluar"));
        Assert.Contains(documento.Advertencias, a => a.Contains("«Municipio»"));
        Assert.Contains(documento.Advertencias, a => a.Contains("otro rubro"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?solicitudId=SOL-003")]
    public async Task El_endpoint_devuelve_un_pdf(string consulta)
    {
        var respuesta = await _cliente.GetAsync($"{Ruta}/pdf{consulta}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"propuesta_{_propuesta}", respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Solicitud_o_plantilla_inexistente_devuelve_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync($"{Ruta}/pdf?solicitudId=SOL-NO")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/propuestas/PRO-NO/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.GetAsync("/api/versiones-plantilla/VER-NO/pdf")).StatusCode);
    }

    private static JsonObject Celda(string tipo, params JsonNode[] enLinea) => new()
    {
        ["type"] = tipo,
        ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(enLinea) }),
    };

    private static JsonObject Fila(params JsonObject[] celdas) => new() { ["type"] = "tableRow", ["content"] = new JsonArray(celdas) };

    private static JsonObject TextoNodo(string texto) => new() { ["type"] = "text", ["text"] = texto };

    private Task<HttpResponseMessage> PublicarParrafo(JsonObject tabla)
    {
        var parrafo = new JsonObject
        {
            ["plantillaId"] = "PLA-001",
            ["bloque"] = "CONSIDERANDO",
            ["contenido"] = new JsonObject { ["type"] = "doc", ["content"] = new JsonArray(tabla) },
        };
        return _cliente.PostAsync($"{Ruta}/parrafos", new StringContent(parrafo.ToJsonString(), Encoding.UTF8, "application/json"));
    }

    [Fact]
    public async Task Acepta_tablas_con_campos_y_las_lleva_al_pdf()
    {
        var tabla = new JsonObject
        {
            ["type"] = "table",
            ["content"] = new JsonArray(
                Fila(Celda("tableHeader", TextoNodo("Dato")), Celda("tableHeader", TextoNodo("Valor"))),
                Fila(Celda("tableCell", TextoNodo("Beneficiario")),
                    Celda("tableCell", new JsonObject { ["type"] = "campo", ["attrs"] = new JsonObject { ["campoId"] = "beneficiario" } })),
                Fila(new JsonObject
                {
                    ["type"] = "tableCell",
                    ["attrs"] = new JsonObject { ["colspan"] = 2, ["rowspan"] = 1, ["colwidth"] = null },
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(TextoNodo("Celda combinada")) }),
                })),
        };

        var respuesta = await PublicarParrafo(tabla);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var documento = await Generar("SOL-001");
        var conTabla = Seccion(documento, Dominio.Modelos.Seccion.Considerando).Parrafos.Last();
        Assert.Contains("Beneficiario Ficticio Uno (prueba)", Texto(conTabla.Contenido));

        var pdf = await _cliente.GetAsync($"{Ruta}/pdf?solicitudId=SOL-001");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
    }

    [Fact]
    public async Task Rechaza_tablas_anidadas_demasiado_anchas_o_mal_formadas()
    {
        var anidada = new JsonObject
        {
            ["type"] = "table",
            ["content"] = new JsonArray(Fila(new JsonObject
            {
                ["type"] = "tableCell",
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "table",
                    ["content"] = new JsonArray(Fila(Celda("tableCell", TextoNodo("interior")))),
                }),
            })),
        };
        var ancha = new JsonObject
        {
            ["type"] = "table",
            ["content"] = new JsonArray(Fila(Enumerable.Range(1, 13).Select(i => Celda("tableCell", TextoNodo($"c{i}"))).ToArray())),
        };
        var malFormada = new JsonObject
        {
            ["type"] = "table",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "paragraph", ["content"] = new JsonArray(TextoNodo("x")) }),
        };

        var r1 = await PublicarParrafo(anidada);
        var r2 = await PublicarParrafo(ancha);
        var r3 = await PublicarParrafo(malFormada);

        Assert.Contains("tablas dentro de otra tabla", await r1.Content.ReadAsStringAsync());
        Assert.Contains("hasta 12 columnas", await r2.Content.ReadAsStringAsync());
        Assert.Contains("estructura de la tabla", await r3.Content.ReadAsStringAsync());
    }
}
