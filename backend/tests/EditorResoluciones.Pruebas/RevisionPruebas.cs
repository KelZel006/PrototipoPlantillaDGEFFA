using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// Envío a verificación, revisión del Oficial Jurídico y devolución a registro (Fase 5), con los datos de prueba:
/// a.perez prepara el documento y j.ramos lo revisa.
/// </summary>
public sealed class RevisionPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public RevisionPruebas()
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

    private async Task<T> Leer<T>(string ruta, string usuario) =>
        (await (await Enviar(HttpMethod.Get, ruta, usuario)).Content.ReadFromJsonAsync<T>(Json))!;

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    /// <summary>a.perez crea un consolidado con la modificación de PLA-001 y un considerando nuevo.</summary>
    private async Task<(string Consolidado, string Propuesta)> Preparar()
    {
        var consolidado = (await (await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez")).Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!;
        var propuestas = await (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado.Id}/propuestas/modificar", "a.perez",
            new { plantillaId = "PLA-001" })).Content.ReadFromJsonAsync<List<Propuesta>>(Json);
        var propuesta = propuestas!.Single().Id;
        (await Enviar(HttpMethod.Post, $"/api/propuestas/{propuesta}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 0, Doc("Que se agrega un considerando."), false, null, 0))).EnsureSuccessStatusCode();
        return (consolidado.Id, propuesta);
    }

    private static SolicitudDevolucion Devolucion(string propuesta) => new(
        "El considerando nuevo no cita la norma aplicable.",
        [new DatosObservacion(propuesta, "CONSIDERANDO", "Falta la referencia legal.", "Citar el artículo correspondiente.")]);

    [Fact]
    public async Task Ciclo_completo_enviar_devolver_corregir_y_reenviar_con_su_historial()
    {
        var (consolidado, propuesta) = await Preparar();

        // Primera presentación: pasa a VERIFICADO y sale de la bandeja del analista.
        var enviado = await (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez"))
            .Content.ReadFromJsonAsync<DocumentoConsolidado>(Json);
        Assert.Equal((EstadoConsolidado.Verificado, 1, "a.perez"), (enviado!.Estado, enviado.NumeroPresentacion, enviado.EnviadoPor));
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"/api/consolidados/{consolidado}", "a.perez")).StatusCode);
        Assert.Contains(await Leer<List<ResumenConsolidado>>("/api/consolidados", "j.ramos"), c => c.Id == consolidado);
        // Mientras se revisa, el analista no puede cambiarlo.
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Post, $"/api/propuestas/{propuesta}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 0, Doc("Otro."), false, null, 0))).StatusCode);

        // El Oficial Jurídico compara y devuelve con su justificación.
        var comparacion = await Leer<ComparacionPropuesta>($"/api/propuestas/{propuesta}/comparacion", "j.ramos");
        Assert.Contains(comparacion.Ajustes, a => a.Etiqueta.StartsWith("NUEVO CONSIDERANDO"));
        (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "j.ramos", Devolucion(propuesta))).EnsureSuccessStatusCode();

        // Vuelve a la bandeja del analista, que ve la devolución y corrige.
        var devuelto = await Leer<DetalleConsolidado>($"/api/consolidados/{consolidado}", "a.perez");
        Assert.Equal(EstadoConsolidado.EnRegistro, devuelto.Consolidado.Estado);
        var dev = Assert.Single(devuelto.Devoluciones);
        Assert.Equal(("j.ramos", 1, "CONSIDERANDO"), (dev.Usuario, dev.NumeroPresentacion, dev.Observaciones.Single().BloqueCodigo));
        (await Enviar(HttpMethod.Post, $"/api/propuestas/{propuesta}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 0, Doc("Que, conforme al artículo 25, se agrega."), false, null, 0))).EnsureSuccessStatusCode();

        // Segunda presentación y segunda devolución: nada se sobrescribe.
        (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez")).EnsureSuccessStatusCode();
        (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "j.ramos",
            new SolicitudDevolucion("Todavía falta corregir la cabecera.", []))).EnsureSuccessStatusCode();

        var final = await Leer<DetalleConsolidado>($"/api/consolidados/{consolidado}", "a.perez");
        Assert.Equal([1, 2], final.Presentaciones.Select(p => p.Numero));
        Assert.Equal([1, 2], final.Devoluciones.Select(d => d.NumeroPresentacion));
        Assert.Equal("El considerando nuevo no cita la norma aplicable.", final.Devoluciones[0].Justificacion);
        // La primera presentación conserva lo enviado entonces (un considerando nuevo); la segunda, dos.
        int Nuevos(Presentacion p) => p.Propuestas.Single().ContenidoPropuesto.Bloques.Single(b => b.Codigo == "CONSIDERANDO").Parrafos.Count;
        Assert.Equal(Nuevos(final.Presentaciones[0]) + 1, Nuevos(final.Presentaciones[1]));
        Assert.All(final.Presentaciones, p => Assert.Equal(HuellaContenido.De(p.Propuestas), p.HuellaIntegridad));
        Assert.Equal(
            ["Primera presentación", "Primera devolución", "Segunda presentación", "Segunda devolución"],
            final.Movimientos.Where(m => m.Tipo is TipoMovimiento.EnvioVerificacion or TipoMovimiento.Devolucion)
                .Select(m => string.Join(' ', m.Detalle.Split(' ').Take(2)).TrimEnd(':')));
    }

    [Fact]
    public async Task Enviar_exige_plantillas_bloques_obligatorios_y_ser_el_analista_dueno()
    {
        var vacio = (await (await Enviar(HttpMethod.Post, "/api/consolidados", "a.perez")).Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!;
        var sinPlantillas = await Enviar(HttpMethod.Post, $"/api/consolidados/{vacio.Id}/enviar", "a.perez");
        Assert.Equal(HttpStatusCode.BadRequest, sinPlantillas.StatusCode);
        Assert.Contains("agregue al menos una", await sinPlantillas.Content.ReadAsStringAsync());

        var (consolidado, _) = await Preparar();
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "m.lopez")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "j.ramos")).StatusCode);

        // Un bloque obligatorio sin párrafos impide el envío.
        var tipo = await Leer<TipoDocumento>("/api/tipos-documento/TDO-001", "admin");
        tipo.Bloques = [new BloqueDeTipo { BloqueId = "CONSIDERANDO", Orden = 1 }, new BloqueDeTipo { BloqueId = "VISTOS", Orden = 2, Obligatorio = true }];
        (await Enviar(HttpMethod.Put, "/api/tipos-documento/TDO-001", "admin", tipo)).EnsureSuccessStatusCode();
        var obligatorio = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez");
        Assert.Equal(HttpStatusCode.BadRequest, obligatorio.StatusCode);
        Assert.Contains("Vistos", await obligatorio.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Devolver_exige_justificacion_y_observaciones_validas()
    {
        var (consolidado, propuesta) = await Preparar();
        // Solo el Oficial Jurídico devuelve, y solo lo que le llegó (VERIFICADO).
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "admin", Devolucion(propuesta))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "j.ramos", Devolucion(propuesta))).StatusCode);
        (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez")).EnsureSuccessStatusCode();

        var sinJustificacion = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "j.ramos", new SolicitudDevolucion("  ", []));
        Assert.Equal(HttpStatusCode.BadRequest, sinJustificacion.StatusCode);
        Assert.Contains("justificación es obligatoria", await sinJustificacion.Content.ReadAsStringAsync());

        var observacionMala = await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "j.ramos", new SolicitudDevolucion(
            "Justificación suficiente.", [new DatosObservacion("PRO-AJENA", null, "x", null), new DatosObservacion(propuesta, "INEXISTENTE", "", null)]));
        Assert.Equal(HttpStatusCode.BadRequest, observacionMala.StatusCode);
        var texto = await observacionMala.Content.ReadAsStringAsync();
        Assert.Contains("no está en este documento", texto);
        Assert.Contains("INEXISTENTE", texto);
        Assert.Contains("Describa el problema", texto);

        // El analista no devuelve; nada cambió con los intentos rechazados.
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/devolver", "a.perez", Devolucion(propuesta))).StatusCode);
        Assert.Equal(EstadoConsolidado.Verificado, (await Leer<DetalleConsolidado>($"/api/consolidados/{consolidado}", "j.ramos")).Consolidado.Estado);
    }

    [Fact]
    public void Quien_preparo_el_documento_no_lo_revisa_aunque_sea_Oficial_Juridico()
    {
        var oficial = new DocumentoConsolidado { AnalistaId = "a.perez", EnviadoPor = "a.perez" };

        Assert.Throws<AccesoDenegadoException>(() =>
            ServicioRevision.ExigirSeparacion(new Usuario { Id = "a.perez", Roles = [Rol.Analista, Rol.OficialJuridico] }, oficial));
        ServicioRevision.ExigirSeparacion(new Usuario { Id = "j.ramos", Roles = [Rol.OficialJuridico] }, oficial);
    }

    [Fact]
    public async Task Las_diferencias_solo_las_ve_el_Oficial_Juridico_con_el_documento_verificado()
    {
        var (consolidado, propuesta) = await Preparar();
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/comparacion", "a.perez")).StatusCode);
        // EN REGISTRO no está en la bandeja del Oficial Jurídico.
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/comparacion", "j.ramos")).StatusCode);

        (await Enviar(HttpMethod.Post, $"/api/consolidados/{consolidado}/enviar", "a.perez")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/comparacion", "j.ramos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, $"/api/propuestas/{propuesta}/comparacion", "admin")).StatusCode);
    }
}
