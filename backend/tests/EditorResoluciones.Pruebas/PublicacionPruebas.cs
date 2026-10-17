using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Infraestructura.Json;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// Aprobación y publicación transaccional (Fase 6): a.perez prepara y envía, j.ramos aprueba. PLA-001 se modifica y se
/// crea una plantilla nueva (RUB-001 / SUB-002 / TDO-003 / Original).
/// </summary>
public sealed class PublicacionPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public PublicacionPruebas()
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

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, string usuario, object? cuerpo = null)
    {
        var mensaje = new HttpRequestMessage(metodo, ruta) { Headers = { { "X-Usuario-Prueba", usuario } } };
        if (cuerpo is not null)
            mensaje.Content = JsonContent.Create(cuerpo, options: Json);
        return _cliente.SendAsync(mensaje);
    }

    private async Task<T> Leer<T>(string ruta, string usuario = "j.ramos") =>
        (await (await Enviar(HttpMethod.Get, ruta, usuario)).Content.ReadFromJsonAsync<T>(Json))!;

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    private async Task<List<VersionPlantilla>> Versiones()
    {
        using var alcance = _fabrica.Services.CreateScope();
        return [.. await alcance.ServiceProvider.GetRequiredService<IRepositorio<VersionPlantilla>>().ListarAsync()];
    }

    /// <summary>Consolidado con MODIFICAR PLA-001 (con un considerando nuevo) y CREAR una plantilla, ya enviado.</summary>
    private async Task<(string Consolidado, string Modificar, string Crear)> PrepararYEnviar()
    {
        var consolidado = (await (await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez")).Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!.Id;
        var modificar = (await (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/propuestas/modificar", "a.perez",
            new { plantillaId = "PLA-001" })).Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single().Id;
        (await Enviar(HttpMethod.Post, $"/api/propuestas/{modificar}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 0, Doc("Que se publica este considerando."), false, null, 0))).EnsureSuccessStatusCode();
        var crear = (await (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/propuestas/crear", "a.perez",
            new SolicitudCrearPlantilla("RUB-001", "SUB-002", "TDO-003", "CDO-001", "Plantilla nueva publicada"))).Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single().Id;
        (await Enviar(HttpMethod.Post, $"/api/propuestas/{crear}/parrafos", "a.perez",
            new DatosParrafo("RESUELVE", 0, Doc("Resolver lo que corresponda."), false, null, 0))).EnsureSuccessStatusCode();
        (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez")).EnsureSuccessStatusCode();
        return (consolidado, modificar, crear);
    }

    [Fact]
    public async Task Aprobar_publica_todo_y_actualiza_la_vigencia()
    {
        var (consolidado, _, _) = await PrepararYEnviar();
        var antes = (await Versiones()).Single(v => v.PlantillaId == "PLA-001" && v.Vigencia == Vigencia.Vigente);

        var respuesta = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoAprobacion>(Json))!;
        Assert.Equal((EstadoConsolidado.Aprobado, "j.ramos"), (resultado.Consolidado.Estado, resultado.Consolidado.AprobadorId));
        Assert.Equal(2, resultado.Publicadas.Count);

        var versiones = await Versiones();
        // MODIFICAR: la versión anterior queda NO VIGENTE (no se borra) y la nueva VIGENTE, ligada a la anterior y al PLAN.
        var pla001 = versiones.Where(v => v.PlantillaId == "PLA-001").OrderBy(v => v.NumeroVersion).ToList();
        Assert.Equal([(1, Vigencia.NoVigente), (2, Vigencia.Vigente)], pla001.Select(v => (v.NumeroVersion, v.Vigencia)));
        Assert.Equal(antes.Id, pla001[0].Id);
        Assert.NotNull(pla001[0].FinVigencia);
        Assert.Equal(antes.Contenido.Bloques.Sum(b => b.Parrafos.Count), pla001[0].Contenido.Bloques.Sum(b => b.Parrafos.Count));
        Assert.Equal((antes.Id, consolidado, "j.ramos"), (pla001[1].VersionAnteriorId, pla001[1].ConsolidadoId, pla001[1].PublicadaPor));
        Assert.Contains(pla001[1].Contenido.Bloques.Single(b => b.Codigo == "CONSIDERANDO").Parrafos,
            p => p.Contenido.Contenido![0].Contenido![0].Texto == "Que se publica este considerando.");

        // CREAR: la plantilla existe recién ahora, con su versión 1 VIGENTE; nada más cambió de vigencia.
        var plantillas = await Leer<List<Plantilla>>("/api/plantillas?rubroId=RUB-001&subRubroId=SUB-002");
        var nueva = Assert.Single(plantillas);
        Assert.Equal("Plantilla nueva publicada", nueva.Nombre);
        var deNueva = Assert.Single(versiones, v => v.PlantillaId == nueva.Id);
        Assert.Equal((1, Vigencia.Vigente, (string?)null), (deNueva.NumeroVersion, deNueva.Vigencia, deNueva.VersionAnteriorId));
        Assert.Equal(1, versiones.Count(v => v.Vigencia == Vigencia.NoVigente));

        // Bitácora, bandejas y consolidado de plantillas.
        var detalle = await Leer<DetalleConsolidado>($"/api/consolidados/{consolidado}", "a.perez");
        Assert.Contains(detalle.Movimientos, m => m.Tipo == TipoMovimiento.Aprobacion);
        Assert.Contains(detalle.Movimientos, m => m.Tipo == TipoMovimiento.CambioVigencia && m.Detalle.Contains("NO VIGENTE"));
        Assert.Contains(detalle.Movimientos, m => m.Tipo == TipoMovimiento.Publicacion && m.Detalle.Contains("plantilla nueva"));
        var historia = (await Leer<List<PlantillaConVersiones>>("/api/consolidado-plantillas", "a.perez")).Single(p => p.PlantillaId == "PLA-001");
        Assert.Equal([2, 1], historia.Versiones.Select(v => v.NumeroVersion));
        Assert.Equal(detalle.Consolidado.Numero, historia.Versiones[0].ConsolidadoNumero);

        // Un documento aprobado ya no se modifica; un cambio nuevo parte de la versión publicada.
        Assert.Equal(HttpStatusCode.Conflict, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos")).StatusCode);
        var otro = (await (await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez")).Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!.Id;
        var siguiente = (await (await Enviar(HttpMethod.Post, $"/api/consolidados/{otro}/propuestas/modificar", "a.perez",
            new { plantillaId = "PLA-001" })).Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single();
        Assert.Equal(pla001[1].Id, siguiente.VersionOrigenId);
    }

    [Fact]
    public async Task Si_la_version_vigente_cambio_no_se_publica_nada_y_queda_registrado()
    {
        var (consolidado, _, _) = await PrepararYEnviar();
        // Otra publicación cambió la vigente de PLA-001 por fuera del documento.
        using (var alcance = _fabrica.Services.CreateScope())
        {
            var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorio<VersionPlantilla>>();
            var vigente = (await repositorio.ListarAsync()).Single(v => v.PlantillaId == "PLA-001" && v.Vigencia == Vigencia.Vigente);
            vigente.Huella = "OTRA";
            await repositorio.ActualizarAsync(vigente);
        }
        var antes = (await Versiones()).Select(v => (v.Id, v.Version, v.Vigencia)).ToList();

        var respuesta = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("cambió desde que se preparó", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(antes, (await Versiones()).Select(v => (v.Id, v.Version, v.Vigencia)));
        Assert.Empty(await Leer<List<Plantilla>>("/api/plantillas?rubroId=RUB-001&subRubroId=SUB-002"));
        var detalle = await Leer<DetalleConsolidado>($"/api/consolidados/{consolidado}");
        Assert.Equal(EstadoConsolidado.Verificado, detalle.Consolidado.Estado);
        Assert.Contains(detalle.Movimientos, m => m.Tipo == TipoMovimiento.ErrorPublicacion && m.Detalle.Contains("No se publicó"));
    }

    [Fact]
    public async Task Si_el_contenido_cambio_despues_del_envio_no_se_publica()
    {
        var (consolidado, modificar, _) = await PrepararYEnviar();
        using (var alcance = _fabrica.Services.CreateScope())
        {
            var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>();
            var propuesta = (await repositorio.ObtenerAsync(modificar))!;
            propuesta.ContenidoPropuesto.Diseno.PiePagina = "Cambio no revisado";
            await repositorio.ActualizarAsync(propuesta);
        }

        var respuesta = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("cambió desde que se envió", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_fallo_al_guardar_no_deja_cambios_parciales()
    {
        var (consolidado, _, _) = await PrepararYEnviar();
        var antes = (await Versiones()).Select(v => (v.Id, v.Version, v.Vigencia)).ToList();
        var almacen = _fabrica.Services.GetRequiredService<AlmacenJson>();
        // Las demás colecciones se escriben; al llegar a las versiones, el disco «falla».
        almacen.AntesDeEscribirEnTransaccion = coleccion =>
            coleccion == "versionesPlantilla" ? throw new IOException("Disco lleno (simulado)") : Task.CompletedTask;

        var respuesta = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos");
        almacen.AntesDeEscribirEnTransaccion = null;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal(antes, (await Versiones()).Select(v => (v.Id, v.Version, v.Vigencia)));
        Assert.Empty(await Leer<List<Plantilla>>("/api/plantillas?rubroId=RUB-001&subRubroId=SUB-002"));
        var detalle = await Leer<DetalleConsolidado>($"/api/consolidados/{consolidado}");
        Assert.Equal(EstadoConsolidado.Verificado, detalle.Consolidado.Estado);
        Assert.Contains(detalle.Movimientos, m => m.Tipo == TipoMovimiento.ErrorPublicacion);
        // Corregido el problema, se aprueba normalmente.
        Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos")).StatusCode);
    }

    [Theory]
    [InlineData("a.perez", HttpStatusCode.Forbidden)]  // El analista no aprueba.
    [InlineData("admin", HttpStatusCode.Forbidden)]    // El administrador tampoco.
    [InlineData("m.lopez", HttpStatusCode.Forbidden)]
    public async Task Solo_el_Oficial_Juridico_aprueba(string usuario, HttpStatusCode esperado)
    {
        var (consolidado, _, _) = await PrepararYEnviar();
        Assert.Equal(esperado, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", usuario)).StatusCode);
    }

    [Fact]
    public async Task No_se_aprueba_lo_que_no_esta_verificado()
    {
        var consolidado = (await (await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez")).Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!.Id;
        // EN REGISTRO no llega a la bandeja del Oficial Jurídico.
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos")).StatusCode);
    }
}

/// <summary>Aprobación del documento de demostración PLAN-20260002 de los datos semilla (Resolución, Certificación y Constancia).</summary>
public sealed class PublicacionSemillaPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public PublicacionSemillaPruebas()
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

    [Fact]
    public async Task El_documento_de_demostracion_se_aprueba_con_sus_tres_plantillas()
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, "/api/consolidados/CON-0002/aprobar") { Headers = { { "X-Usuario-Prueba", "j.ramos" } } };
        var respuesta = await _cliente.SendAsync(mensaje);

        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ResultadoAprobacion>(Json))!;
        Assert.Equal(3, resultado.Publicadas.Count);
        Assert.Contains(resultado.Publicadas, v => v.PlantillaId == "PLA-001" && v.NumeroVersion == 2);
        Assert.Contains(resultado.Publicadas, v => v.PlantillaId == "PLA-004" && v.NumeroVersion == 2);
        Assert.Contains(resultado.Publicadas, v => v.NumeroVersion == 1);
    }
}
