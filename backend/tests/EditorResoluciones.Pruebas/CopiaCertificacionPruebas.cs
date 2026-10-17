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
/// La Resolución lleva consigo su Certificación dentro del Documento Consolidado (decisión D3), y una plantilla no puede
/// estar en dos consolidados abiertos (D4). Se usan los datos semilla sin sus consolidados de demostración, para que
/// la Resolución PLA-001 y su Certificación PLA-004 estén libres.
/// </summary>
public sealed class CopiaCertificacionPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public CopiaCertificacionPruebas()
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

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    private async Task<List<ParrafoPropuesta>> Parrafos(string propuestaId, string? bloque = null) =>
        (await _cliente.GetFromJsonAsync<List<ParrafoPropuesta>>(
            $"/api/propuestas/{propuestaId}/parrafos{(bloque is null ? "" : $"?bloque={bloque}")}", Json))!;

    private async Task<ParrafoPropuesta> CrearParrafo(string propuestaId, string bloque, string texto)
    {
        var respuesta = await _cliente.PostAsJsonAsync($"/api/propuestas/{propuestaId}/parrafos",
            new DatosParrafo(bloque, 0, Doc(texto), false, null, 0), Json);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ParrafoPropuesta>(Json))!;
    }

    private async Task Actualizar(string propuestaId, ParrafoPropuesta parrafo, string? texto = null, bool? activo = null) =>
        (await _cliente.PutAsJsonAsync($"/api/propuestas/{propuestaId}/parrafos/{parrafo.Id}",
            new DatosParrafo(parrafo.Bloque, parrafo.Orden, texto is null ? parrafo.Contenido : Doc(texto), parrafo.MostrarSoloSiCumple,
                parrafo.Condicion, parrafo.Version, activo ?? parrafo.Activo), Json)).EnsureSuccessStatusCode();

    /// <summary>Propuesta MODIFICAR de PLA-001 y su Certificación derivada (PLA-004).</summary>
    private async Task<(string Consolidado, Propuesta Resolucion, Propuesta Certificacion)> ModificarResolucion()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var agregadas = await Consolidados.ModificarAsync(_cliente, "PLA-001", consolidado);
        Assert.Equal(2, agregadas.Count);
        var resolucion = agregadas.Single(p => p.PlantillaId == "PLA-001");
        var certificacion = agregadas.Single(p => p.PlantillaId == "PLA-004");
        Assert.Equal(resolucion.Id, certificacion.DerivadaDePropuestaId);
        Assert.Equal(OperacionPropuesta.Modificar, certificacion.Operacion);
        return (consolidado, resolucion, certificacion);
    }

    [Fact]
    public async Task Los_cambios_de_la_Resolucion_pasan_a_su_Certificacion_sin_tocar_las_vigentes()
    {
        var (_, resolucion, certificacion) = await ModificarResolucion();
        var vigenteAntes = await _cliente.GetStringAsync("/api/versiones-plantilla?vigencia=Vigente");

        // Crear: el nuevo considerando aparece en la Certificación, con «resolución» cambiada.
        var nuevo = await CrearParrafo(resolucion.Id, CodigosBloque.Considerando, "Que la presente resolución se notifica.");
        var copia = (await Parrafos(certificacion.Id, CodigosBloque.Considerando)).Single(p => Texto(p.Contenido).Contains("se notifica"));
        Assert.Equal("Que la presente certificación se notifica.", Texto(copia.Contenido));

        // Actualizar.
        await Actualizar(resolucion.Id, nuevo, "Que la resolución cambió.");
        Assert.Equal("Que la certificación cambió.", Texto((await Parrafos(certificacion.Id)).Single(p => p.Id == copia.Id).Contenido));

        // Un párrafo existente: PAR-002 de la Resolución corresponde a PAR-031 de la Certificación.
        var par002 = (await Parrafos(resolucion.Id)).Single(p => p.Id == "PAR-002");
        await Actualizar(resolucion.Id, par002, "Texto nuevo del primer considerando.");
        Assert.Equal("Texto nuevo del primer considerando.", Texto((await Parrafos(certificacion.Id)).Single(p => p.Id == "PAR-031").Contenido));

        // Duplicar y reordenar: la Certificación sigue el mismo orden relativo.
        (await _cliente.PostAsync($"/api/propuestas/{resolucion.Id}/parrafos/PAR-003/duplicar", null)).EnsureSuccessStatusCode();
        var considerandos = await Parrafos(resolucion.Id, CodigosBloque.Considerando);
        considerandos.Reverse();
        (await _cliente.PostAsJsonAsync($"/api/propuestas/{resolucion.Id}/parrafos/reordenar", new SolicitudReordenPropuesta(CodigosBloque.Considerando,
            considerandos.Select(p => new ElementoOrden(p.Id, p.Version)).ToList()), Json)).EnsureSuccessStatusCode();
        var enResolucion = (await Parrafos(resolucion.Id, CodigosBloque.Considerando)).Select(p => p.Id).ToList();
        var enCertificacion = await Parrafos(certificacion.Id, CodigosBloque.Considerando);
        var origenes = (await _cliente.GetFromJsonAsync<Propuesta>($"/api/propuestas/{certificacion.Id}", Json))!.ContenidoPropuesto.Bloques
            .Single(b => b.Codigo == CodigosBloque.Considerando).Parrafos.ToDictionary(p => p.Id, p => p.OrigenParrafoId);
        Assert.Equal(enResolucion, enCertificacion.Select(p => origenes[p.Id]));
        Assert.Equal(Enumerable.Range(1, enCertificacion.Count), enCertificacion.Select(p => p.Orden));

        // Eliminar.
        var actual = (await Parrafos(resolucion.Id)).Single(p => p.Id == nuevo.Id);
        (await _cliente.DeleteAsync($"/api/propuestas/{resolucion.Id}/parrafos/{nuevo.Id}?version={actual.Version}")).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await Parrafos(certificacion.Id), p => p.Id == copia.Id);

        // Nada de esto cambió las versiones vigentes (regla 6).
        Assert.Equal(vigenteAntes, await _cliente.GetStringAsync("/api/versiones-plantilla?vigencia=Vigente"));
    }

    [Fact]
    public async Task Lo_editado_en_la_Certificacion_se_conserva_hasta_que_la_Resolucion_cambia_lo_mismo()
    {
        var (_, resolucion, certificacion) = await ModificarResolucion();
        var editada = (await Parrafos(certificacion.Id)).Single(p => p.Id == "PAR-031");
        var otra = (await Parrafos(certificacion.Id)).Single(p => p.Id == "PAR-032");

        // Se edita directamente la Certificación (texto y estado) y se agrega un párrafo propio.
        await Actualizar(certificacion.Id, editada, "Texto propio de la certificación", activo: false);
        var propio = await CrearParrafo(certificacion.Id, CodigosBloque.Considerando, "Párrafo solo de la certificación");

        // Un cambio en otro párrafo de la Resolución llega sin tocar lo editado ni lo propio.
        await Actualizar(resolucion.Id, (await Parrafos(resolucion.Id)).Single(p => p.Id == "PAR-003"), "Otro texto de la resolución");
        var parrafos = await Parrafos(certificacion.Id);
        Assert.Equal("Otro texto de la certificación", Texto(parrafos.Single(p => p.Id == otra.Id).Contenido));
        Assert.Equal("Texto propio de la certificación", Texto(parrafos.Single(p => p.Id == editada.Id).Contenido));
        Assert.False(parrafos.Single(p => p.Id == editada.Id).Activo);
        Assert.Contains(parrafos, p => p.Id == propio.Id);

        // Cuando la Resolución cambia ese mismo texto, gana la Resolución; el estado (no cambió allá) se conserva.
        await Actualizar(resolucion.Id, (await Parrafos(resolucion.Id)).Single(p => p.Id == "PAR-002"), "La resolución manda");
        parrafos = await Parrafos(certificacion.Id);
        Assert.Equal("La certificación manda", Texto(parrafos.Single(p => p.Id == editada.Id).Contenido));
        Assert.False(parrafos.Single(p => p.Id == editada.Id).Activo);
        Assert.Contains(parrafos, p => p.Id == propio.Id);
        // Los cambios de la Certificación no vuelven a la Resolución.
        Assert.DoesNotContain(await Parrafos(resolucion.Id), p => Texto(p.Contenido).Contains("solo de la certificación"));
    }

    [Fact]
    public async Task El_diseno_de_la_Resolucion_pasa_solo_en_lo_que_cambia()
    {
        var (_, resolucion, certificacion) = await ModificarResolucion();
        async Task Diseno(string id, string? pie, double espacio) =>
            (await _cliente.PutAsJsonAsync($"/api/propuestas/{id}/diseno", new DatosDiseno(AlineacionLogo.Centro, pie, espacio), Json)).EnsureSuccessStatusCode();
        async Task<PlantillaEdicion> Edicion(string id) => (await _cliente.GetFromJsonAsync<PlantillaEdicion>($"/api/propuestas/{id}/edicion", Json))!;

        await Diseno(resolucion.Id, "Pie de la resolución", 2.5);
        Assert.Equal(("Pie de la resolución", 2.5), ((await Edicion(certificacion.Id)).PiePagina, (await Edicion(certificacion.Id)).EspacioFirmasCm));

        // La Certificación cambia su pie; luego la Resolución cambia solo el espacio entre firmas.
        await Diseno(certificacion.Id, "Pie propio", 2.5);
        await Diseno(resolucion.Id, "Pie de la resolución", 3);
        Assert.Equal(("Pie propio", 3.0), ((await Edicion(certificacion.Id)).PiePagina, (await Edicion(certificacion.Id)).EspacioFirmasCm));
    }

    [Fact]
    public async Task Crear_una_Resolucion_crea_su_Certificacion_derivada()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var respuesta = await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidado}/propuestas/crear",
            new SolicitudCrearPlantilla("1600", "1602", "TDO-001", "CDO-001", null), Json);
        respuesta.EnsureSuccessStatusCode();
        var agregadas = (await respuesta.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!;

        var resolucion = agregadas.Single(p => p.TipoDocumentoId == "TDO-001");
        var certificacion = agregadas.Single(p => p.TipoDocumentoId == "TDO-004");
        Assert.All(agregadas, p => Assert.Equal(OperacionPropuesta.Crear, p.Operacion));
        Assert.Equal(resolucion.Id, certificacion.DerivadaDePropuestaId);
        Assert.StartsWith("Certificación (Original) – ", certificacion.Nombre);

        await CrearParrafo(resolucion.Id, CodigosBloque.Resuelve, "RESOLUCIÓN No. 1: Notificar la presente resolución.");
        var copia = Assert.Single(await Parrafos(certificacion.Id, CodigosBloque.Resuelve));
        Assert.Equal("CERTIFICACIÓN No. 1: Notificar la presente certificación.", Texto(copia.Contenido));
        // Ninguna plantilla existe todavía: se crean al aprobar.
        Assert.Empty(await _cliente.GetFromJsonAsync<List<Plantilla>>("/api/plantillas?rubroId=1600&subRubroId=1602", Json) ?? []);
    }

    [Fact]
    public async Task La_Certificacion_que_ya_estaba_en_el_consolidado_se_liga_a_su_Resolucion()
    {
        var consolidado = await Consolidados.CrearAsync(_cliente);
        var sola = Assert.Single(await Consolidados.ModificarAsync(_cliente, "PLA-004", consolidado));
        Assert.Null(sola.DerivadaDePropuestaId);

        var agregadas = await Consolidados.ModificarAsync(_cliente, "PLA-001", consolidado);

        Assert.Equal(2, agregadas.Count);
        var ligada = agregadas.Single(p => p.Id == sola.Id);
        Assert.Equal(agregadas.Single(p => p.PlantillaId == "PLA-001").Id, ligada.DerivadaDePropuestaId);
    }

    [Fact]
    public async Task Una_plantilla_no_puede_estar_en_dos_consolidados_abiertos()
    {
        var (consolidado, resolucion, certificacion) = await ModificarResolucion();

        // En el mismo consolidado: todos sus ajustes van en la misma propuesta.
        var repetida = await _cliente.PostAsJsonAsync($"/api/consolidados/{consolidado}/propuestas/modificar", new { plantillaId = "PLA-001" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Contains("misma propuesta", await repetida.Content.ReadAsStringAsync());

        // En otro consolidado: se rechaza indicando dónde está. Tampoco se puede tomar su Certificación.
        var otro = await Consolidados.CrearAsync(_cliente);
        var numero = (await _cliente.GetFromJsonAsync<DetalleConsolidado>($"/api/consolidados/{consolidado}", Json))!.Consolidado.Numero;
        foreach (var plantilla in new[] { "PLA-001", "PLA-004" })
        {
            var respuesta = await _cliente.PostAsJsonAsync($"/api/consolidados/{otro}/propuestas/modificar", new { plantillaId = plantilla }, Json);
            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            Assert.Contains(numero, await respuesta.Content.ReadAsStringAsync());
        }
        var vigentes = (await _cliente.GetFromJsonAsync<List<PlantillaVigente>>("/api/plantillas-vigentes", Json))!;
        Assert.Equal(numero, vigentes.Single(v => v.PlantillaId == "PLA-001").EnConsolidado);
        Assert.Null(vigentes.Single(v => v.PlantillaId == "PLA-002").EnConsolidado);

        // La Certificación sola no se retira; al retirar la Resolución se retiran las dos y quedan libres.
        var sola = await _cliente.PostAsJsonAsync($"/api/propuestas/{certificacion.Id}/retirar", new SolicitudRetiro("Ya no se necesita"), Json);
        Assert.Equal(HttpStatusCode.Conflict, sola.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _cliente.PostAsJsonAsync($"/api/propuestas/{resolucion.Id}/retirar", new SolicitudRetiro(""), Json)).StatusCode);
        var retiro = await _cliente.PostAsJsonAsync($"/api/propuestas/{resolucion.Id}/retirar", new SolicitudRetiro("Se hará en otro documento"), Json);
        Assert.Equal(2, (await retiro.Content.ReadFromJsonAsync<List<Propuesta>>(Json))!.Count(p => p.Retirada));
        Assert.Equal(2, (await Consolidados.ModificarAsync(_cliente, "PLA-001", otro)).Count);

        // La bitácora conserva el retiro.
        var detalle = (await _cliente.GetFromJsonAsync<DetalleConsolidado>($"/api/consolidados/{consolidado}", Json))!;
        Assert.Contains(detalle.Movimientos, m => m.Tipo == TipoMovimiento.PropuestaRetirada && m.Detalle.Contains("otro documento"));
        Assert.Empty(detalle.TiposIncluidos);
    }

    [Fact]
    public async Task El_resumen_muestra_cada_tipo_una_vez_y_los_ajustes_de_cada_plantilla()
    {
        var (consolidado, resolucion, _) = await ModificarResolucion();
        await CrearParrafo(resolucion.Id, CodigosBloque.Considerando, "Que se agrega un considerando.");
        await Actualizar(resolucion.Id, (await Parrafos(resolucion.Id)).Single(p => p.Id == "PAR-008"), "Texto nuevo del por tanto.");
        (await _cliente.PutAsJsonAsync($"/api/propuestas/{resolucion.Id}/diseno", new DatosDiseno(AlineacionLogo.Centro, "Pie nuevo", 1.2), Json)).EnsureSuccessStatusCode();

        var detalle = (await _cliente.GetFromJsonAsync<DetalleConsolidado>($"/api/consolidados/{consolidado}", Json))!;

        Assert.Equal(["Certificación", "Resolución"], detalle.TiposIncluidos.Select(t => t.TipoDocumento));
        var tipoResolucion = detalle.TiposIncluidos.Single(t => t.TipoDocumento == "Resolución");
        Assert.Equal(("Modificar", 1, 3), (tipoResolucion.Operacion, tipoResolucion.Plantillas, tipoResolucion.Ajustes));
        Assert.Equal(["NUEVO CONSIDERANDO (posición 5)", "POR TANTO 1 modificado", "Pie de página modificado"],
            detalle.Ajustes[resolucion.Id].Select(a => a.Etiqueta));
    }

    [Fact]
    public async Task La_carga_inicial_conserva_el_vinculo_de_la_Certificacion_con_su_Resolucion()
    {
        using var alcance = _fabrica.Services.CreateScope();
        var versiones = await alcance.ServiceProvider.GetRequiredService<IRepositorio<VersionPlantilla>>().ListarAsync();
        var certificacion = versiones.Single(v => v.PlantillaId == "PLA-004" && v.Vigencia == Vigencia.Vigente);

        var cabecera = certificacion.Contenido.Bloques.Single(b => b.Codigo == CodigosBloque.Cabecera).Parrafos.Single();
        Assert.Equal("PAR-001", cabecera.OrigenParrafoId);
    }
}

/// <summary>Quién puede editar una propuesta: solo el analista dueño, y solo mientras el consolidado está EN REGISTRO.</summary>
public sealed class EdicionPropuestaPermisosPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public EdicionPropuestaPermisosPruebas()
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

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, string usuario, object? cuerpo = null)
    {
        var mensaje = new HttpRequestMessage(metodo, ruta) { Headers = { { "X-Usuario-Prueba", usuario } } };
        if (cuerpo is not null)
            mensaje.Content = JsonContent.Create(cuerpo, options: Json);
        return _cliente.SendAsync(mensaje);
    }

    private static DatosParrafo Nuevo() => new(CodigosBloque.Considerando, 0, new NodoContenido
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = "x" }] }],
    }, false, null, 0);

    [Fact]
    public async Task El_dueno_edita_su_propuesta_en_registro()
    {
        // PRO-0005 es la Resolución de PLAN-20260003, de m.lopez, EN REGISTRO. Su Certificación es PRO-0006 (Crear).
        Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Post, "/api/propuestas/PRO-0005/parrafos", "m.lopez", Nuevo())).StatusCode);
        var edicion = await (await Enviar(HttpMethod.Get, "/api/propuestas/PRO-0005/edicion", "m.lopez")).Content.ReadFromJsonAsync<PlantillaEdicion>(Json);
        Assert.True(edicion!.Editable);
        Assert.Equal("PLAN-20260003", edicion.ConsolidadoNumero);
    }

    [Theory]
    [InlineData("a.perez", "PRO-0005", HttpStatusCode.NotFound)]   // Otro analista: ni siquiera lo ve.
    [InlineData("j.ramos", "PRO-0005", HttpStatusCode.NotFound)]   // EN REGISTRO: todavía no llega al Oficial Jurídico.
    [InlineData("admin", "PRO-0005", HttpStatusCode.Forbidden)]    // Lo ve, pero no es analista.
    [InlineData("j.ramos", "PRO-0002", HttpStatusCode.Forbidden)]  // VERIFICADO: el Oficial Jurídico revisa, no edita.
    [InlineData("a.perez", "PRO-0001", HttpStatusCode.Conflict)]   // Su consolidado ya está APROBADO.
    public async Task Nadie_mas_edita_ni_se_edita_fuera_de_registro(string usuario, string propuesta, HttpStatusCode esperado) =>
        Assert.Equal(esperado, (await Enviar(HttpMethod.Post, $"/api/propuestas/{propuesta}/parrafos", usuario, Nuevo())).StatusCode);

    [Fact]
    public async Task El_Oficial_Juridico_consulta_la_propuesta_verificada_sin_poder_editarla()
    {
        var respuesta = await Enviar(HttpMethod.Get, "/api/propuestas/PRO-0002/edicion", "j.ramos");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False((await respuesta.Content.ReadFromJsonAsync<PlantillaEdicion>(Json))!.Editable);
        Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Get, "/api/propuestas/PRO-0002/pdf", "j.ramos")).StatusCode);
    }
}

/// <summary>Cambio de «resolución» por «certificación» en el texto copiado.</summary>
public class CambioPalabraPruebas
{
    private static readonly Dominio.Servicios.CambioPalabra Cambio = new("Resolución", "Certificación");

    [Theory]
    [InlineData("RESOLUCIÓN No. 15", "CERTIFICACIÓN No. 15")]
    [InlineData("RESOLUCION No. 15", "CERTIFICACIÓN No. 15")]
    [InlineData("Resolución de prueba", "Certificación de prueba")]
    [InlineData("la presente resolución.", "la presente certificación.")]
    [InlineData("la presente resolucion,", "la presente certificación,")]
    [InlineData("las resoluciones anteriores", "las certificaciones anteriores")]
    [InlineData("RESOLUCIONES", "CERTIFICACIONES")]
    [InlineData("(Resolución)", "(Certificación)")]
    public void Cambia_la_palabra_con_tilde_y_respetando_mayusculas(string texto, string esperado) =>
        Assert.Equal(esperado, Cambio.Aplicar(texto));

    [Theory]
    [InlineData("resolutivo")]
    [InlineData("irresolución")]
    [InlineData("Considerando")]
    public void Solo_cambia_palabras_completas(string texto) => Assert.Equal(texto, Cambio.Aplicar(texto));
}
