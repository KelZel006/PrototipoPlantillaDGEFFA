using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// Fase 7: auditoría, concurrencia y permisos de todo el flujo, sobre los datos de prueba (a.perez prepara, j.ramos
/// revisa, admin administra).
/// </summary>
public sealed class AuditoriaPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public AuditoriaPruebas()
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

    private async Task<T> Leer<T>(HttpResponseMessage respuesta)
    {
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    /// <summary>a.perez arma un consolidado con MODIFICAR PLA-001 (un considerando nuevo) y, si se pide, lo envía.</summary>
    private async Task<(string Consolidado, string Propuesta)> Preparar(bool enviar)
    {
        var consolidado = (await Leer<DocumentoConsolidado>(await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez"))).Id;
        var propuesta = (await Leer<List<Propuesta>>(await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/propuestas/modificar", "a.perez",
            new { plantillaId = "PLA-001" }))).Single().Id;
        await Leer<ParrafoPropuesta>(await Enviar(HttpMethod.Post, $"/api/propuestas/{propuesta}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 0, Doc("Que se audita este cambio."), false, null, 0)));
        if (enviar)
            await Leer<DocumentoConsolidado>(await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez"));
        return (consolidado, propuesta);
    }

    // ---------- Trazabilidad e historial ----------

    [Fact]
    public async Task Cada_version_publicada_se_rastrea_hasta_su_documento_y_sus_responsables()
    {
        var (consolidado, propuesta) = await Preparar(enviar: true);
        await Leer<Devolucion>(await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "j.ramos",
            new SolicitudDevolucion("Revisar la redacción del considerando.", [])));
        await Leer<DocumentoConsolidado>(await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez"));
        var publicada = (await Leer<ResultadoAprobacion>(await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos")))
            .Publicadas.Single();

        var traza = await Leer<Trazabilidad>(await Enviar(HttpMethod.Get, $"/api/versiones-plantilla/{publicada.Id}/trazabilidad", "admin"));

        // Qué texto estaba vigente antes, qué se publicó, de qué documento y quiénes intervinieron.
        Assert.NotNull(traza.Anterior);
        Assert.Equal(Vigencia.NoVigente, traza.Anterior!.Vigencia);
        Assert.Equal((consolidado, "a.perez", "a.perez", "j.ramos"), (traza.ConsolidadoId, traza.Analista, traza.EnviadoPor, traza.Aprobador));
        Assert.Equal([1, 2], traza.Presentaciones.Select(p => p.Numero));
        Assert.Equal("Revisar la redacción del considerando.", Assert.Single(traza.Devoluciones).Justificacion);
        Assert.Equal(["NUEVO CONSIDERANDO (posición 4)"], traza.Ajustes.Select(a => a.Etiqueta));
        Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Get, $"/api/versiones-plantilla/{publicada.Id}/trazabilidad", "j.ramos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, $"/api/versiones-plantilla/{publicada.Id}/trazabilidad", "a.perez")).StatusCode);

        // El historial técnico conserva cada paso con su antes y su después, y solo lo consulta el administrador.
        var historial = await Leer<List<RegistroHistorial>>(await Enviar(HttpMethod.Get, $"/api/historial?entidadId={propuesta}", "admin"));
        Assert.Contains(historial, r => r.Accion == AccionHistorial.Creacion && r.Usuario == "a.perez");
        Assert.Contains(historial, r => r.Accion == AccionHistorial.Actualizacion && r.Nuevo is not null);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, "/api/historial", "a.perez")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, "/api/historial", "j.ramos")).StatusCode);
    }

    [Theory]
    [InlineData("DELETE", "/api/versiones-plantilla/VER-X")]
    [InlineData("PUT", "/api/versiones-plantilla/VER-X")]
    [InlineData("DELETE", "/api/consolidados/CON-X")]
    [InlineData("DELETE", "/api/historial/HIS-X")]
    [InlineData("POST", "/api/historial")]
    public async Task Versiones_documentos_e_historial_no_se_borran_ni_se_reescriben(string metodo, string ruta)
    {
        // Regla 15 y sección 10: no hay ninguna operación para borrar o reescribir lo histórico.
        var respuesta = await Enviar(new HttpMethod(metodo), ruta, "admin", metodo == "DELETE" ? null : new { });
        Assert.Contains(respuesta.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
    }

    [Fact]
    public async Task Con_dos_versiones_vigentes_no_se_publica_y_se_informa_la_inconsistencia()
    {
        var (consolidado, _) = await Preparar(enviar: true);
        using (var alcance = _fabrica.Services.CreateScope())
        {
            // Datos dañados a mano: PLA-001 queda con dos versiones vigentes (regla 14).
            var repositorio = alcance.ServiceProvider.GetRequiredService<IRepositorio<VersionPlantilla>>();
            var vigente = (await repositorio.ListarAsync()).Single(v => v.PlantillaId == "PLA-001" && v.Vigencia == Vigencia.Vigente);
            await repositorio.CrearAsync(new VersionPlantilla
            {
                PlantillaId = "PLA-001", NumeroVersion = 9, Vigencia = Vigencia.Vigente, Contenido = vigente.Contenido, Huella = vigente.Huella,
            });
        }

        var respuesta = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("versiones vigentes", await respuesta.Content.ReadAsStringAsync());
    }

    // ---------- Concurrencia ----------

    [Fact]
    public async Task Dos_ediciones_simultaneas_del_mismo_parrafo_no_se_pisan()
    {
        var (_, propuesta) = await Preparar(enviar: false);
        var parrafo = (await Leer<List<ParrafoPropuesta>>(await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/parrafos?bloque=CONSIDERANDO", "a.perez")))[0];

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Enviar(HttpMethod.Put, $"/api/propuestas/{propuesta}/parrafos/{parrafo.Id}", "a.perez",
            new DatosParrafo("CONSIDERANDO", parrafo.Orden, Doc($"Edición {i}"), false, null, parrafo.Version))));

        // Solo una edición gana; las demás traían la versión anterior y se rechazan.
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var final = (await Leer<List<ParrafoPropuesta>>(await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/parrafos?bloque=CONSIDERANDO", "a.perez")))[0];
        Assert.Equal(parrafo.Version + 1, final.Version);
    }

    [Fact]
    public async Task Aprobaciones_simultaneas_publican_una_sola_vez()
    {
        var (consolidado, _) = await Preparar(enviar: true);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/aprobar", "j.ramos")));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(4, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        using var alcance = _fabrica.Services.CreateScope();
        var versiones = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<VersionPlantilla>>().ListarAsync())
            .Where(v => v.PlantillaId == "PLA-001").ToList();
        Assert.Equal(2, versiones.Count);
        Assert.Single(versiones, v => v.Vigencia == Vigencia.Vigente);
    }

    [Fact]
    public async Task Dos_documentos_no_pueden_tomar_la_misma_plantilla_al_mismo_tiempo()
    {
        var documentos = new List<string>();
        for (var i = 0; i < 4; i++)
            documentos.Add((await Leer<DocumentoConsolidado>(await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez"))).Id);

        var respuestas = await Task.WhenAll(documentos.Select(d =>
            Enviar(HttpMethod.Post, $"/api/consolidados/{d}/propuestas/modificar", "a.perez", new { plantillaId = "PLA-001" })));

        // Decisión D4, también con peticiones simultáneas.
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(3, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    // ---------- Permisos en el servidor (regla 18) ----------

    public static TheoryData<string, string, string, HttpStatusCode> MatrizPermisos => new()
    {
        // Método, ruta ({c} = consolidado enviado, {p} = su propuesta), usuario, respuesta esperada.
        { "POST", "/api/consolidados", "j.ramos", HttpStatusCode.Forbidden },
        { "POST", "/api/consolidados", "admin", HttpStatusCode.Forbidden },
        { "POST", "/api/consolidados/{c}/enviar", "j.ramos", HttpStatusCode.Forbidden },
        { "POST", "/api/consolidados/{c}/devolver", "a.perez", HttpStatusCode.Forbidden },
        { "POST", "/api/consolidados/{c}/devolver", "admin", HttpStatusCode.Forbidden },
        { "POST", "/api/consolidados/{c}/aprobar", "a.perez", HttpStatusCode.Forbidden },
        { "POST", "/api/consolidados/{c}/aprobar", "admin", HttpStatusCode.Forbidden },
        { "GET", "/api/consolidados/{c}", "a.perez", HttpStatusCode.NotFound },   // VERIFICADO: fuera de su bandeja (D6).
        { "GET", "/api/consolidados/{c}", "m.lopez", HttpStatusCode.NotFound },   // De otro analista (D10).
        { "GET", "/api/consolidados/{c}", "j.ramos", HttpStatusCode.OK },
        { "GET", "/api/consolidados/{c}", "admin", HttpStatusCode.OK },
        { "GET", "/api/propuestas/{p}/comparacion", "a.perez", HttpStatusCode.Forbidden },
        { "GET", "/api/propuestas/{p}/comparacion", "admin", HttpStatusCode.Forbidden },
        { "GET", "/api/propuestas/{p}/comparacion", "j.ramos", HttpStatusCode.OK },
        { "POST", "/api/propuestas/{p}/retirar", "j.ramos", HttpStatusCode.Forbidden },
        { "PUT", "/api/propuestas/{p}/diseno", "j.ramos", HttpStatusCode.Forbidden },
        { "POST", "/api/bloques", "a.perez", HttpStatusCode.Forbidden },
        { "POST", "/api/campos", "j.ramos", HttpStatusCode.Forbidden },
        { "PUT", "/api/plantillas/PLA-001", "admin", HttpStatusCode.Conflict },   // Edición directa cerrada (D11), para todos.
        { "GET", "/api/historial", "a.perez", HttpStatusCode.Forbidden },
        { "GET", "/api/consolidados", "nadie", HttpStatusCode.Unauthorized },
        { "GET", "/api/consolidados", "inactivo", HttpStatusCode.Unauthorized },
    };

    [Theory]
    [MemberData(nameof(MatrizPermisos))]
    public async Task Cada_rol_solo_puede_lo_que_le_corresponde(string metodo, string ruta, string usuario, HttpStatusCode esperado)
    {
        var (consolidado, propuesta) = await Preparar(enviar: true);
        object? cuerpo = metodo switch
        {
            "GET" => null,
            _ when ruta.EndsWith("/devolver") => new SolicitudDevolucion("Justificación de prueba.", []),
            _ when ruta.EndsWith("/retirar") => new SolicitudRetiro("Motivo de prueba"),
            _ when ruta.EndsWith("/diseno") => new DatosDiseno(AlineacionLogo.Centro, null, 1.2),
            _ => new { },
        };

        var respuesta = await Enviar(new HttpMethod(metodo), ruta.Replace("{c}", consolidado).Replace("{p}", propuesta), usuario, cuerpo);

        Assert.Equal(esperado, respuesta.StatusCode);
    }
}
