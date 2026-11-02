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

/// <summary>
/// Marca «Título», alerta al aprobador cuando se modifica el campo Operador, y unidades (Exoneraciones, Dispensas,
/// Registro Exonerados, Notas de Crédito y Devoluciones).
/// </summary>
public sealed class UnidadesTitulosAlertasPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public UnidadesTitulosAlertasPruebas()
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

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, string? usuario = null, object? cuerpo = null)
    {
        var mensaje = new HttpRequestMessage(metodo, ruta);
        if (usuario is not null)
            mensaje.Headers.Add("X-Usuario-Prueba", usuario);
        if (cuerpo is not null)
            mensaje.Content = JsonContent.Create(cuerpo, options: Json);
        return _cliente.SendAsync(mensaje);
    }

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    private async Task<ParrafoPropuesta> Parrafo(string propuesta, string id) =>
        (await _cliente.GetFromJsonAsync<List<ParrafoPropuesta>>($"/api/propuestas/{propuesta}/parrafos", Json))!.Single(p => p.Id == id);

    private async Task<HttpResponseMessage> Actualizar(string propuesta, ParrafoPropuesta p, NodoContenido? contenido = null, bool? esTitulo = null, bool? unir = null) =>
        await _cliente.PutAsJsonAsync($"/api/propuestas/{propuesta}/parrafos/{p.Id}",
            new DatosParrafo(p.Bloque, p.Orden, contenido ?? p.Contenido, p.MostrarSoloSiCumple, p.Condicion, p.Version, p.Activo,
                unir ?? p.UnirConAnterior, esTitulo ?? p.EsTitulo), Json);

    private async Task<SeccionDocumento> Considerandos(string propuestaId, string? solicitud)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var propuesta = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(propuestaId))!;
        var (plantilla, bloques) = ConstructorContenido.ADocumento(propuesta.ContenidoPropuesto, propuesta.Id, propuesta.Nombre,
            propuesta.RubroId, propuesta.SubRubroId, propuesta.TipoDocumentoId, propuesta.ClaseDocumentoId);
        var documento = await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync(plantilla, bloques, solicitud);
        return documento.Secciones.Single(s => s.Codigo == CodigosBloque.Considerando);
    }

    // ---------- Título ----------

    [Fact]
    public async Task Un_titulo_va_centrado_y_en_negrita_y_no_consume_numeracion()
    {
        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).Single().Id;
        (await Actualizar(propuesta, await Parrafo(propuesta, "PAR-003"), esTitulo: true)).EnsureSuccessStatusCode();
        Assert.True((await Parrafo(propuesta, "PAR-003")).EsTitulo);

        var considerandos = (await Considerandos(propuesta, "SOL-001")).Parrafos;

        Assert.Equal(["PAR-002", "PAR-003", "PAR-004"], considerandos.Select(p => p.Id));
        Assert.StartsWith("CONSIDERANDO(1): ", Texto(considerandos[0].Contenido));
        var titulo = considerandos[1];
        Assert.True(titulo.EsTitulo);
        Assert.DoesNotContain("CONSIDERANDO", Texto(titulo.Contenido));
        var parrafo = titulo.Contenido.Contenido![0];
        Assert.Equal("center", parrafo.Atributos!["textAlign"].GetString());
        Assert.All(parrafo.Contenido!.Where(n => n.Tipo == "text"), n => Assert.Contains(n.Marcas!, m => m.Tipo == "bold"));
        Assert.StartsWith("CONSIDERANDO(2): ", Texto(considerandos[2].Contenido));

        var ajustes = await _cliente.GetStringAsync($"/api/propuestas/{propuesta}/ajustes");
        Assert.Contains("CONSIDERANDO 2: se marcó como título", ajustes);
    }

    [Fact]
    public async Task Un_titulo_no_se_une_con_el_parrafo_anterior()
    {
        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).Single().Id;

        var respuesta = await Actualizar(propuesta, await Parrafo(propuesta, "PAR-003"), esTitulo: true, unir: true);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ---------- Alerta del campo Operador ----------

    [Fact]
    public async Task Al_aprobador_se_le_avisa_que_se_modifico_un_parrafo_con_el_campo_Operador()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001", consolidado)).Single().Id;
        // PAR-002 usa {Operador}; PAR-003 no lo usa.
        var par002 = await Parrafo(propuesta, "PAR-002");
        var contenido = FusionDerivadaPrueba.ConTextoAlFinal(par002.Contenido, " Texto agregado.");
        (await Actualizar(propuesta, par002, contenido)).EnsureSuccessStatusCode();
        var par003 = await Parrafo(propuesta, "PAR-003");
        (await Actualizar(propuesta, par003, FusionDerivadaPrueba.ConTextoAlFinal(par003.Contenido, " Otro."))).EnsureSuccessStatusCode();
        (await _cliente.PostAsync($"/api/consolidados/{consolidado}/enviar", null)).EnsureSuccessStatusCode();

        var paraAprobador = await (await Enviar(HttpMethod.Get, $"/api/consolidados/{consolidado}", "j.ramos")).Content.ReadFromJsonAsync<DetalleConsolidado>(Json);
        var alerta = Assert.Single(paraAprobador!.Alertas);
        Assert.Equal("operador", alerta.CampoId);
        Assert.Equal("Operador", alerta.Campo);
        Assert.Equal("CONSIDERANDO 1", alerta.Ubicacion);
        Assert.Contains("cambió el texto alrededor de {Operador}", alerta.Detalle);

        // El analista no recibe la alerta.
        var paraAnalista = await _cliente.GetFromJsonAsync<DetalleConsolidado>($"/api/consolidados/{consolidado}", Json);
        Assert.Empty(paraAnalista!.Alertas);
    }

    [Fact]
    public async Task Quitar_el_campo_Operador_o_eliminar_su_parrafo_tambien_se_avisa()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001", consolidado)).Single().Id;
        (await _cliente.DeleteAsync($"/api/propuestas/{propuesta}/parrafos/PAR-002?version=1")).EnsureSuccessStatusCode();
        (await _cliente.PostAsync($"/api/consolidados/{consolidado}/enviar", null)).EnsureSuccessStatusCode();

        var detalle = await (await Enviar(HttpMethod.Get, $"/api/consolidados/{consolidado}", "j.ramos")).Content.ReadFromJsonAsync<DetalleConsolidado>(Json);

        Assert.Contains("se eliminó un párrafo con {Operador}", Assert.Single(detalle!.Alertas).Detalle);
    }

    [Fact]
    public async Task Una_plantilla_nueva_no_genera_aviso_del_campo_Operador()
    {
        // CREAR clonando PLA-001, cuyo texto usa {Operador}: todos sus párrafos son nuevos, pero no es una modificación.
        var consolidado = await Consolidados.CrearAsync(_cliente);
        (await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidado}/propuestas/crear",
            new SolicitudCrearPlantilla("RUB-001", "SUB-001", "TDO-001", "CDO-002", null, "PLA-001"), Json)).EnsureSuccessStatusCode();
        (await _cliente.PostAsync($"/api/consolidados/{consolidado}/enviar", null)).EnsureSuccessStatusCode();

        var detalle = await (await Enviar(HttpMethod.Get, $"/api/consolidados/{consolidado}", "j.ramos")).Content.ReadFromJsonAsync<DetalleConsolidado>(Json);

        Assert.Empty(detalle!.Alertas);
    }

    [Fact]
    public async Task El_listado_trae_cada_plantilla_con_los_nombres_de_su_identidad()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        await Consolidados.ModificarAsync(_cliente, "PLA-001", consolidado);

        var resumen = (await _cliente.GetFromJsonAsync<List<ResumenConsolidado>>("/api/consolidados", Json))!.Single(c => c.Id == consolidado);

        var plantilla = Assert.Single(resumen.Plantillas);
        Assert.Equal("PLA-001", (await _cliente.GetFromJsonAsync<Propuesta>($"/api/propuestas/{plantilla.PropuestaId}", Json))!.PlantillaId);
        Assert.Equal(OperacionPropuesta.Modificar, plantilla.Operacion);
        Assert.False(string.IsNullOrEmpty(plantilla.Rubro));
        Assert.NotEqual(plantilla.RubroId, plantilla.Rubro);
        Assert.Equal("Resolución de aprobación (prueba)", plantilla.TipoDocumento);
        Assert.Equal("Original", plantilla.ClaseDocumento);
    }

    [Fact]
    public async Task El_Nombre_Plantilla_se_escribe_al_agregar_la_plantilla_y_no_cambia_el_nombre_tecnico()
    {
        // MODIFICAR: con el nombre que escribe el analista (sin espacios sobrantes).
        var consolidadoId = await Consolidados.CrearAsync(_cliente);
        var respuesta = await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidadoId}/propuestas/modificar",
            new SolicitudModificarPlantilla("PLA-001", "  Exoneración a docentes 2026  "), Json);
        respuesta.EnsureSuccessStatusCode();
        var modificacion = (await respuesta.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.First();
        var edicion = await _cliente.GetFromJsonAsync<PlantillaEdicion>($"/api/propuestas/{modificacion.Id}/edicion", Json);
        Assert.Equal("Exoneración a docentes 2026", edicion!.NombreReferencia);
        Assert.NotEqual(edicion.NombreReferencia, edicion.Nombre);

        // CREAR: también desde el diálogo.
        var crear = await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidadoId}/propuestas/crear",
            new SolicitudCrearPlantilla("RUB-001", "SUB-002", "TDO-003", "CDO-001", null, null, "Dictamen ONG"), Json);
        crear.EnsureSuccessStatusCode();
        Assert.Equal("Dictamen ONG", (await crear.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.First().NombreReferencia);

        // Más de 200 caracteres se rechaza; ya no hay forma de cambiarlo en el editor.
        var otro = await Consolidados.CrearAsync(_cliente);
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.PostAsJsonAsync($"/api/consolidados/{otro}/propuestas/modificar",
            new SolicitudModificarPlantilla("PLA-002", new string('x', 201)), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.PutAsJsonAsync($"/api/propuestas/{modificacion.Id}/nombre-referencia",
            new { nombreReferencia = "Otro" }, Json)).StatusCode);
    }

    [Fact]
    public async Task El_analista_elimina_su_documento_EN_REGISTRO_de_forma_logica_y_libera_sus_plantillas()
    {
        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).First();
        var id = propuesta.ConsolidadoId;

        // El Oficial Jurídico no elimina documentos.
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, $"/api/consolidados/{id}/eliminar", "j.ramos", null)).StatusCode);
        // Otro analista tampoco: no es suyo.
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Post, $"/api/consolidados/{id}/eliminar", "m.lopez", null)).StatusCode);

        var respuesta = await _cliente.PostAsync($"/api/consolidados/{id}/eliminar", null);
        respuesta.EnsureSuccessStatusCode();
        Assert.Equal(EstadoConsolidado.Eliminado, (await respuesta.Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!.Estado);

        // No se borra: el documento y su propuesta siguen guardados, con el movimiento de la eliminación.
        using var alcance = _fabrica.Services.CreateScope();
        var guardado = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<DocumentoConsolidado>>().ObtenerAsync(id))!;
        Assert.Equal(EstadoConsolidado.Eliminado, guardado.Estado);
        Assert.NotNull(await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(propuesta.Id));
        Assert.Contains((await alcance.ServiceProvider.GetRequiredService<IRepositorio<Movimiento>>().ListarAsync()),
            m => m.ConsolidadoId == id && m.Tipo == TipoMovimiento.Eliminacion && m.EstadoDestino == EstadoConsolidado.Eliminado);

        // Sale de la bandeja del analista; el administrador lo sigue viendo, en consulta, como ELIMINADO.
        Assert.False(ServicioConsolidados.PuedeVer(new Usuario { Id = guardado.AnalistaId, Roles = [Rol.Analista] }, guardado));
        Assert.False(ServicioConsolidados.PuedeVer(new Usuario { Id = "j.ramos", Roles = [Rol.OficialJuridico] }, guardado));
        var bandeja = await _cliente.GetFromJsonAsync<List<ResumenConsolidado>>("/api/consolidados", Json);
        Assert.Equal(EstadoConsolidado.Eliminado, Assert.Single(bandeja!, c => c.Id == id).Estado);
        // La plantilla queda libre para otro documento.
        Assert.Single(await Consolidados.ModificarAsync(_cliente, "PLA-001"), p => p.PlantillaId == "PLA-001");

        // Ya eliminado, no se vuelve a eliminar.
        Assert.Equal(HttpStatusCode.Conflict, (await _cliente.PostAsync($"/api/consolidados/{id}/eliminar", null)).StatusCode);
    }

    // ---------- Unidades ----------

    [Fact]
    public async Task Las_unidades_se_listan_en_el_orden_del_menu()
    {
        var unidades = await _cliente.GetFromJsonAsync<List<Unidad>>("/api/unidades", Json);

        Assert.Equal(["Exoneraciones", "Dispensas", "Registro Exonerados", "Notas de Crédito y Devoluciones"], unidades!.Select(u => u.Nombre));
    }

    [Fact]
    public async Task Los_datos_anteriores_quedan_en_Exoneraciones_y_un_documento_sin_unidad_tambien()
    {
        var vigentes = await _cliente.GetFromJsonAsync<List<PlantillaVigente>>("/api/plantillas-vigentes", Json);
        Assert.All(vigentes!, p => Assert.Equal("exoneraciones", p.UnidadId));

        var consolidado = await _cliente.GetFromJsonAsync<DetalleConsolidado>($"/api/consolidados/{await Consolidados.CrearAsync(_cliente)}", Json);
        Assert.Equal("exoneraciones", consolidado!.Consolidado.UnidadId);
    }

    [Fact]
    public async Task Cada_unidad_tiene_sus_propias_plantillas()
    {
        var respuesta = await Enviar(HttpMethod.Post, "/api/consolidados", cuerpo: new SolicitudCrearConsolidado("dispensas"));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var dispensas = (await respuesta.Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!;
        Assert.Equal("dispensas", dispensas.UnidadId);

        // PLA-001 es de Exoneraciones: no se modifica desde un documento de Dispensas.
        var modificar = await _cliente.PostAsJsonAsync($"/api/consolidados/{dispensas.Id}/propuestas/modificar", new { plantillaId = "PLA-001" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, modificar.StatusCode);

        // La misma combinación de PLA-001 se puede crear en Dispensas: es otra plantilla.
        var crear = await _cliente.PostAsJsonAsync($"/api/consolidados/{dispensas.Id}/propuestas/crear",
            new SolicitudCrearPlantilla("RUB-001", "SUB-001", "TDO-001", "CDO-001", null), Json);
        Assert.Equal(HttpStatusCode.OK, crear.StatusCode);
        Assert.Equal("dispensas", (await crear.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Single().UnidadId);

        // En Exoneraciones esa combinación ya existe.
        var exoneraciones = await Consolidados.CrearAsync(_cliente);
        var repetida = await _cliente.PostAsJsonAsync($"/api/consolidados/{exoneraciones}/propuestas/crear",
            new SolicitudCrearPlantilla("RUB-001", "SUB-001", "TDO-001", "CDO-001", null), Json);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task Una_unidad_inexistente_se_rechaza()
    {
        var respuesta = await Enviar(HttpMethod.Post, "/api/consolidados", cuerpo: new SolicitudCrearConsolidado("no-existe"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}

/// <summary>Ayuda de las pruebas: agrega texto al final del primer párrafo de un contenido.</summary>
internal static class FusionDerivadaPrueba
{
    public static NodoContenido ConTextoAlFinal(NodoContenido documento, string texto)
    {
        var copia = JsonSerializer.Deserialize<NodoContenido>(JsonSerializer.Serialize(documento))!;
        var primero = copia.Contenido!.First(b => b.Tipo == "paragraph");
        primero.Contenido = [.. primero.Contenido ?? [], new NodoContenido { Tipo = "text", Texto = texto }];
        return copia;
    }
}
