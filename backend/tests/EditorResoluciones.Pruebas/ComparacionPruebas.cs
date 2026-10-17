using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>Diferencia de texto palabra por palabra.</summary>
public class DiferenciaTextoPruebas
{
    private static string Marcar(IReadOnlyList<Segmento> segmentos) => string.Concat(segmentos.Select(s => s.Tipo switch
    {
        TipoSegmento.Igual => s.Texto,
        TipoSegmento.Agregado => $"[+{s.Texto}]",
        TipoSegmento.Eliminado => $"[-{s.Texto}]",
        _ => $"[~{s.Texto}]",
    }));

    [Theory]
    [InlineData("Que el monto es alto.", "Que el monto es muy alto.", "Que el monto es alto.", "Que el monto es [+muy ]alto.")]
    [InlineData("Que el monto es muy alto.", "Que el monto es alto.", "Que el monto es [-muy ]alto.", "Que el monto es alto.")]
    [InlineData("Que el monto asciende a L 10.", "Que el monto alcanza L 10.", "Que el monto [~asciende a] L 10.", "Que el monto [~alcanza] L 10.")]
    [InlineData("Sin cambios.", "Sin cambios.", "Sin cambios.", "Sin cambios.")]
    public void Marca_lo_agregado_eliminado_y_sustituido(string original, string propuesto, string izquierda, string derecha)
    {
        var (a, b) = ComparadorContenido.DiferenciaTexto(original, propuesto);

        Assert.Equal(izquierda, Marcar(a));
        Assert.Equal(derecha, Marcar(b));
        // Cada lado conserva su texto completo.
        Assert.Equal(original, string.Concat(a.Select(s => s.Texto)));
        Assert.Equal(propuesto, string.Concat(b.Select(s => s.Texto)));
    }
}

/// <summary>Comparación de una propuesta con su versión original, por bloque y por párrafo.</summary>
public class ComparadorContenidoPruebas
{
    private static readonly IReadOnlyDictionary<string, Campo> SinCampos = new Dictionary<string, Campo>();

    private static NodoContenido Doc(string texto, bool negrita = false) => new()
    {
        Tipo = "doc",
        Contenido =
        [
            new NodoContenido
            {
                Tipo = "paragraph",
                Contenido = [new NodoContenido { Tipo = "text", Texto = texto, Marcas = negrita ? [new MarcaContenido { Tipo = "bold" }] : null }],
            },
        ],
    };

    private static ParrafoContenido P(string id, int orden, string texto, bool negrita = false) =>
        new() { Id = id, Orden = orden, Contenido = Doc(texto, negrita) };

    private static ContenidoPlantilla Contenido(params (string Codigo, ParrafoContenido[] Parrafos)[] bloques) =>
        new() { Bloques = bloques.Select(b => new BloqueContenido { Codigo = b.Codigo, Parrafos = [.. b.Parrafos] }).ToList() };

    private static ComparacionPropuesta Comparar(ContenidoPlantilla original, ContenidoPlantilla propuesto) =>
        ComparadorContenido.Comparar("PRO-1", OperacionPropuesta.Modificar, original, propuesto, CatalogoBloques.Predeterminados, SinCampos);

    [Fact]
    public void Empareja_por_identificador_y_ubica_los_eliminados_donde_estaban()
    {
        var original = Contenido(("CONSIDERANDO", [P("A", 1, "Primero."), P("B", 2, "Segundo."), P("C", 3, "Tercero.")]));
        var propuesto = Contenido(("CONSIDERANDO", [P("A", 1, "Primero modificado."), P("N", 2, "Nuevo."), P("C", 3, "Tercero.")]));

        var bloque = Assert.Single(Comparar(original, propuesto).Bloques);

        Assert.Equal(EstadoBloque.Modificado, bloque.Estado);
        // B se eliminó: aparece justo después de A, que lo precedía en el original.
        Assert.Equal(["A", "B", "N", "C"], bloque.Parrafos.Select(p => p.ParrafoId));
        Assert.Equal([EstadoParrafo.Modificado, EstadoParrafo.Eliminado, EstadoParrafo.Agregado, EstadoParrafo.SinCambios],
            bloque.Parrafos.Select(p => p.Estado));
        var modificado = bloque.Parrafos[0];
        Assert.Contains(modificado.Propuesto!, s => s is { Tipo: TipoSegmento.Agregado, Texto: " modificado" });
        Assert.Null(bloque.Parrafos[1].Propuesto);
        Assert.Null(bloque.Parrafos[2].Original);
        Assert.Equal(["CONSIDERANDO 1 modificado"], modificado.Ajustes);
    }

    [Fact]
    public void Detecta_movidos_cambios_de_formato_y_de_configuracion()
    {
        var original = Contenido(("RESUELVE", [P("A", 1, "Uno."), P("B", 2, "Dos."), P("C", 3, "Tres.")]));
        var b = P("B", 3, "Dos.", negrita: true);
        b.Activo = false;
        var propuesto = Contenido(("RESUELVE", [P("A", 1, "Uno."), P("C", 2, "Tres."), b]));

        var parrafos = Assert.Single(Comparar(original, propuesto).Bloques).Parrafos;

        var movido = Assert.Single(parrafos, p => p.Movido);
        Assert.Equal(("B", 2, 3), (movido.ParrafoId, movido.PosicionOriginal, movido.PosicionPropuesta));
        Assert.True(movido.SoloFormato);
        Assert.Equal(["Se desactivó"], movido.Configuracion);
        Assert.Equal(EstadoParrafo.Modificado, movido.Estado);
    }

    [Fact]
    public void Compara_el_texto_de_las_celdas_de_una_tabla()
    {
        static NodoContenido Celda(string texto) => new()
        {
            Tipo = "tableCell",
            Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
        };
        static ParrafoContenido Tabla(string monto) => new()
        {
            Id = "T",
            Orden = 1,
            Contenido = new NodoContenido
            {
                Tipo = "doc",
                Contenido = [new NodoContenido { Tipo = "table", Contenido = [new NodoContenido { Tipo = "tableRow", Contenido = [Celda("Monto"), Celda(monto)] }] }],
            },
        };

        var parrafo = Comparar(Contenido(("CONSIDERANDO", [Tabla("L 100")])), Contenido(("CONSIDERANDO", [Tabla("L 250")])))
            .Bloques.Single().Parrafos.Single();

        Assert.False(parrafo.SoloFormato);
        Assert.Contains(parrafo.Original!, s => s is { Tipo: TipoSegmento.Sustituido, Texto: "100" });
        Assert.Contains(parrafo.Propuesto!, s => s is { Tipo: TipoSegmento.Sustituido, Texto: "250" });
    }

    [Fact]
    public void Marca_bloques_nuevos_y_eliminados_y_el_diseno()
    {
        var original = Contenido(("CABECERA", [P("A", 1, "Encabezado.")]), ("POR_TANTO", [P("B", 1, "Por tanto.")]));
        original.Diseno.PiePagina = "Pie anterior";
        var propuesto = Contenido(("CABECERA", [P("A", 1, "Encabezado.")]), ("VISTOS", [P("V", 1, "Visto el expediente.")]));
        propuesto.Diseno.PiePagina = "Pie nuevo";

        var comparacion = Comparar(original, propuesto);

        Assert.Equal([("CABECERA", EstadoBloque.SinCambios), ("POR_TANTO", EstadoBloque.Eliminado), ("VISTOS", EstadoBloque.Nuevo)],
            comparacion.Bloques.Select(b => (b.Codigo, b.Estado)));
        Assert.Equal(new CampoDiseno("Pie de página", "Pie anterior", "Pie nuevo"), Assert.Single(comparacion.Diseno));
        Assert.Contains(comparacion.Ajustes, a => a.Etiqueta == "BLOQUE ELIMINADO: POR TANTO");
        Assert.Contains(comparacion.Ajustes, a => a.Etiqueta == "NUEVO BLOQUE: VISTOS");
    }
}

/// <summary>Bloques configurables por tipo documental (decisión D2) y comparación por la API.</summary>
public sealed class BloquesConfigurablesPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public BloquesConfigurablesPruebas()
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

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, object? cuerpo, string usuario = "usuario.prueba")
    {
        var mensaje = new HttpRequestMessage(metodo, ruta) { Headers = { { "X-Usuario-Prueba", usuario } } };
        if (cuerpo is not null)
            mensaje.Content = JsonContent.Create(cuerpo, options: Json);
        return _cliente.SendAsync(mensaje);
    }

    /// <summary>El tipo TDO-002 pasa a usar Cabecera, Vistos (obligatorio), Considerando y Firmas.</summary>
    private async Task ConfigurarDictamen()
    {
        var tipo = (await _cliente.GetFromJsonAsync<TipoDocumento>("/api/tipos-documento/TDO-002", Json))!;
        tipo.Bloques =
        [
            new BloqueDeTipo { BloqueId = "CABECERA", Orden = 1 },
            new BloqueDeTipo { BloqueId = "VISTOS", Orden = 2, Obligatorio = true },
            new BloqueDeTipo { BloqueId = "CONSIDERANDO", Orden = 3 },
            new BloqueDeTipo { BloqueId = "FIRMAS", Orden = 4 },
        ];
        (await Enviar(HttpMethod.Put, "/api/tipos-documento/TDO-002", tipo)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Solo_el_administrador_modifica_los_catalogos()
    {
        var nuevo = new BloqueDocumental { Id = "OBSERVACIONES", Nombre = "Observaciones", ContextoCampos = "considerando" };

        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, "/api/bloques", nuevo, "a.perez")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Delete, "/api/rubros/RUB-003", null, "j.ramos")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Enviar(HttpMethod.Post, "/api/bloques", nuevo, "admin")).StatusCode);
        // Consultar sigue abierto.
        Assert.Contains((await _cliente.GetFromJsonAsync<List<BloqueDocumental>>("/api/bloques", Json))!, b => b.Id == "OBSERVACIONES");
    }

    [Fact]
    public async Task Valida_los_bloques_del_catalogo_y_de_cada_tipo()
    {
        var malo = new BloqueDocumental { Id = "minusculas", Nombre = "X", ContextoCampos = "tabla" };
        var respuesta = await Enviar(HttpMethod.Post, "/api/bloques", malo);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("mayúsculas", texto);
        Assert.Contains("contexto", texto);

        var tipo = (await _cliente.GetFromJsonAsync<TipoDocumento>("/api/tipos-documento/TDO-002", Json))!;
        tipo.Bloques = [new BloqueDeTipo { BloqueId = "INEXISTENTE", Orden = 1 }, new BloqueDeTipo { BloqueId = "VISTOS", Orden = 2 }, new BloqueDeTipo { BloqueId = "VISTOS", Orden = 3 }];
        var tipoMalo = await Enviar(HttpMethod.Put, "/api/tipos-documento/TDO-002", tipo);
        Assert.Equal(HttpStatusCode.BadRequest, tipoMalo.StatusCode);
        var detalle = await tipoMalo.Content.ReadAsStringAsync();
        Assert.Contains("INEXISTENTE", detalle);
        Assert.Contains("no puede repetirse", detalle);

        // Un bloque en uso por un tipo no se elimina.
        await ConfigurarDictamen();
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(HttpMethod.Delete, "/api/bloques/VISTOS", null)).StatusCode);
    }

    [Fact]
    public async Task El_tipo_define_los_bloques_del_editor_y_del_documento()
    {
        await ConfigurarDictamen();
        var bloques = (await _cliente.GetFromJsonAsync<List<BloqueConfigurado>>("/api/tipos-documento/TDO-002/bloques", Json))!;
        Assert.Equal(["CABECERA", "VISTOS", "CONSIDERANDO", "FIRMAS"], bloques.Select(b => b.Codigo));

        // PLA-002 es TDO-002: su propuesta usa esos bloques; VISTOS es obligatorio y todavía está vacío.
        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-002")).Single().Id;
        var edicion = (await _cliente.GetFromJsonAsync<PlantillaEdicion>($"/api/propuestas/{propuesta}/edicion", Json))!;
        Assert.Equal(["CABECERA", "VISTOS", "CONSIDERANDO", "FIRMAS"], edicion.Bloques.Select(b => b.Codigo).Take(4));
        Assert.Contains("Vistos", edicion.ObligatoriosVacios);

        // VISTOS admite los campos del considerando; un bloque que el tipo no usa se rechaza.
        var visto = new DatosParrafo("VISTOS", 0, Doc("El expediente de la solicitud."), false, null, 0);
        (await _cliente.PostAsJsonAsync($"/api/propuestas/{propuesta}/parrafos", visto, Json)).EnsureSuccessStatusCode();
        var ajeno = await _cliente.PostAsJsonAsync($"/api/propuestas/{propuesta}/parrafos", visto with { Bloque = "RESUELVE" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, ajeno.StatusCode);
        Assert.Contains("no forma parte de este tipo", await ajeno.Content.ReadAsStringAsync());
        edicion = (await _cliente.GetFromJsonAsync<PlantillaEdicion>($"/api/propuestas/{propuesta}/edicion", Json))!;
        Assert.DoesNotContain("Vistos", edicion.ObligatoriosVacios);

        // El documento sigue el orden del tipo y pone el título del bloque.
        using var alcance = _fabrica.Services.CreateScope();
        var guardada = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(propuesta))!;
        var configurados = await alcance.ServiceProvider.GetRequiredService<CatalogoBloques>().DeContenidoAsync("TDO-002", guardada.ContenidoPropuesto);
        var (plantilla, bloquesDoc) = ConstructorContenido.ADocumento(guardada.ContenidoPropuesto, guardada.Id, guardada.Nombre,
            guardada.RubroId, guardada.SubRubroId, guardada.TipoDocumentoId, guardada.ClaseDocumentoId, configurados);
        var documento = await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync(plantilla, bloquesDoc, null);
        var vistos = documento.Secciones.Single(s => s.Codigo == "VISTOS");
        Assert.Equal("VISTOS:", vistos.Bloque.Titulo);
        Assert.True(documento.Secciones.ToList().IndexOf(vistos) < documento.Secciones.ToList().FindIndex(s => s.Codigo == "CONSIDERANDO"));
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/propuestas/{propuesta}/pdf")).StatusCode);
    }

    [Fact]
    public async Task Una_plantilla_nueva_empieza_con_los_bloques_de_su_tipo()
    {
        await ConfigurarDictamen();
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var respuesta = await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidado}/propuestas/crear",
            new SolicitudCrearPlantilla("RUB-001", "SUB-002", "TDO-002", "CDO-001", null), Json);
        var creada = Assert.Single((await respuesta.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!);

        Assert.Equal(["CABECERA", "VISTOS", "CONSIDERANDO", "FIRMAS"], creada.ContenidoPropuesto.Bloques.Select(b => b.Codigo));
    }

    [Fact]
    public async Task La_comparacion_muestra_las_diferencias_palabra_por_palabra()
    {
        var agregada = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).Single();
        var propuesta = agregada.Id;
        var parrafo = (await _cliente.GetFromJsonAsync<List<ParrafoPropuesta>>($"/api/propuestas/{propuesta}/parrafos?bloque=CONSIDERANDO", Json))![1];
        var original = string.Concat(parrafo.Contenido.Contenido![0].Contenido!.Select(n => n.Texto));
        (await _cliente.PutAsJsonAsync($"/api/propuestas/{propuesta}/parrafos/{parrafo.Id}",
            new DatosParrafo("CONSIDERANDO", parrafo.Orden, Doc(original + " Texto agregado."), parrafo.MostrarSoloSiCumple,
                parrafo.Condicion, parrafo.Version, parrafo.Activo), Json)).EnsureSuccessStatusCode();

        // EN REGISTRO, el analista no ve diferencias: las revisa el Oficial Jurídico cuando está VERIFICADO.
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync($"/api/propuestas/{propuesta}/comparacion")).StatusCode);
        (await _cliente.PostAsync($"/api/consolidados/{agregada.ConsolidadoId}/enviar", null)).EnsureSuccessStatusCode();
        var respuesta = await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/comparacion", null, "j.ramos");
        var comparacion = (await respuesta.Content.ReadFromJsonAsync<ComparacionPropuesta>(Json))!;

        var considerando = comparacion.Bloques.Single(b => b.Codigo == "CONSIDERANDO");
        Assert.Equal(EstadoBloque.Modificado, considerando.Estado);
        var cambiado = considerando.Parrafos.Single(p => p.ParrafoId == parrafo.Id);
        Assert.Equal(EstadoParrafo.Modificado, cambiado.Estado);
        Assert.Contains(cambiado.Propuesto!, s => s.Tipo == TipoSegmento.Agregado && s.Texto.Contains("Texto agregado."));
        Assert.All(comparacion.Bloques.Where(b => b.Codigo != "CONSIDERANDO"), b => Assert.Equal(EstadoBloque.SinCambios, b.Estado));
        Assert.Equal(["CONSIDERANDO 2 modificado"], comparacion.Ajustes.Select(a => a.Etiqueta));
    }
}
