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

/// <summary>Transacciones de varias colecciones sobre JSON: todo o nada, reversión y recuperación del diario.</summary>
public sealed class UnidadDeTrabajoPruebas : IDisposable
{
    private readonly DirectorioTemporal _datos = new();

    public void Dispose() => _datos.Dispose();

    private static Rubro Rubro(string id) => new() { Id = id, Nombre = $"Rubro {id}" };
    private static Usuario Usuario(string id) => new() { Id = id, NombreCompleto = id, Roles = [Rol.Analista] };

    [Fact]
    public async Task Guarda_los_cambios_de_varias_colecciones_juntos()
    {
        var almacen = Rutas.CrearAlmacen(_datos.Ruta);
        var unidad = new UnidadDeTrabajoJson(almacen);

        await unidad.EjecutarAsync(async tx =>
        {
            await tx.CrearAsync(Rubro("R1"));
            await tx.CrearAsync(Usuario("u1"));
            return true;
        });

        Assert.Single(await almacen.LeerAsync<Rubro>("rubros"));
        Assert.Single(await almacen.LeerAsync<Usuario>("usuarios"));
        Assert.Empty(Directory.Exists(almacen.DirectorioTransacciones) ? Directory.GetFiles(almacen.DirectorioTransacciones) : []);
    }

    [Fact]
    public async Task Si_la_operacion_falla_no_se_guarda_nada()
    {
        var almacen = Rutas.CrearAlmacen(_datos.Ruta);
        var unidad = new UnidadDeTrabajoJson(almacen);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unidad.EjecutarAsync<bool>(async tx =>
        {
            await tx.CrearAsync(Rubro("R1"));
            await tx.CrearAsync(Usuario("u1"));
            throw new InvalidOperationException("Falla a mitad de la operación");
        }));

        Assert.Empty(await almacen.LeerAsync<Rubro>("rubros"));
        Assert.Empty(await almacen.LeerAsync<Usuario>("usuarios"));
    }

    [Fact]
    public async Task Una_version_desactualizada_cancela_toda_la_transaccion()
    {
        var almacen = Rutas.CrearAlmacen(_datos.Ruta);
        var unidad = new UnidadDeTrabajoJson(almacen);
        await unidad.EjecutarAsync(async tx => await tx.CrearAsync(Rubro("R1")));

        await Assert.ThrowsAsync<ConflictoException>(() => unidad.EjecutarAsync(async tx =>
        {
            await tx.CrearAsync(Usuario("u1"));
            return await tx.ActualizarAsync(new Rubro { Id = "R1", Nombre = "Cambio", Version = 7 });
        }));

        Assert.Equal("Rubro R1", Assert.Single(await almacen.LeerAsync<Rubro>("rubros")).Nombre);
        Assert.Empty(await almacen.LeerAsync<Usuario>("usuarios"));
    }

    [Fact]
    public async Task Si_falla_la_escritura_de_una_coleccion_se_revierte_lo_ya_escrito()
    {
        var almacen = Rutas.CrearAlmacen(_datos.Ruta);
        var unidad = new UnidadDeTrabajoJson(almacen);
        await unidad.EjecutarAsync(async tx => await tx.CrearAsync(Rubro("R0")));

        // Los rubros se escriben primero; al ir a escribir los usuarios, el disco «falla».
        almacen.AntesDeEscribirEnTransaccion = coleccion =>
            coleccion == "usuarios" ? throw new IOException("Disco lleno (simulado)") : Task.CompletedTask;

        var error = await Assert.ThrowsAsync<AlmacenamientoException>(() => unidad.EjecutarAsync(async tx =>
        {
            await tx.CrearAsync(Rubro("R1"));
            await tx.CrearAsync(Usuario("u1"));
            return true;
        }));

        Assert.Contains("No se aplicó ningún cambio", error.Message);
        Assert.Equal(["R0"], (await almacen.LeerAsync<Rubro>("rubros")).Select(r => r.Id));
        Assert.Empty(await almacen.LeerAsync<Usuario>("usuarios"));
        Assert.Empty(Directory.GetFiles(almacen.DirectorioTransacciones));
    }

    [Fact]
    public async Task Un_diario_pendiente_se_completa_al_iniciar()
    {
        // Simula que el proceso se detuvo después de preparar el diario y antes de escribir los archivos.
        var carpeta = Directory.CreateDirectory(Path.Combine(_datos.Ruta, "transacciones")).FullName;
        var diario = new
        {
            colecciones = new Dictionary<string, object>
            {
                ["rubros"] = new[] { Rubro("R9") },
                ["usuarios"] = new[] { Usuario("u9") },
            },
        };
        await File.WriteAllTextAsync(Path.Combine(carpeta, "20261016-pendiente.json"),
            JsonSerializer.Serialize(diario, OpcionesJson.Predeterminadas));

        var almacen = Rutas.CrearAlmacen(_datos.Ruta);

        Assert.Equal("R9", Assert.Single(await almacen.LeerAsync<Rubro>("rubros")).Id);
        Assert.Equal("u9", Assert.Single(await almacen.LeerAsync<Usuario>("usuarios")).Id);
        Assert.Empty(Directory.GetFiles(carpeta));
    }
}

/// <summary>Número PLAN-AAAANNNN: formato, año institucional, reinicio anual, límite y concurrencia.</summary>
public sealed class NumeracionPlanPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();

    public void Dispose() => _datos.Dispose();

    private static HttpRequestMessage Crear(string usuario = "a.perez") =>
        new(HttpMethod.Post, "/api/consolidados") { Headers = { { "X-Usuario-Prueba", usuario } } };

    private static async Task<DocumentoConsolidado> CrearAsync(HttpClient cliente)
    {
        var respuesta = await cliente.SendAsync(Crear());
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<DocumentoConsolidado>(Json))!;
    }

    [Theory]
    [InlineData(2026, 1, "PLAN-20260001")]
    [InlineData(2026, 42, "PLAN-20260042")]
    [InlineData(2027, 9999, "PLAN-20279999")]
    public void Formato(int anio, int secuencia, string esperado) => Assert.Equal(esperado, NumeroPlan.Formatear(anio, secuencia));

    [Fact]
    public void Fuera_de_rango_se_rechaza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumeroPlan.Formatear(2026, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumeroPlan.Formatear(2026, 10_000));
    }

    [Fact]
    public async Task Sigue_la_secuencia_del_anio_y_se_reinicia_en_el_anio_nuevo_de_la_zona_institucional()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));
        using var fabrica = new FabricaApi(_datos.Ruta, Rutas.DatosSemilla, reloj);
        using var cliente = fabrica.CreateClient();

        // Los datos semilla ya tienen PLAN-20260001 a PLAN-20260003.
        var primero = await CrearAsync(cliente);
        Assert.Equal(("PLAN-20260004", 2026, 4, EstadoConsolidado.EnRegistro, "a.perez"),
            (primero.Numero, primero.Anio, primero.Secuencia, primero.Estado, primero.AnalistaId));

        // 1/1/2027 05:00 UTC todavía es 31/12/2026 en Tegucigalpa (UTC−6).
        reloj.Ahora = new DateTimeOffset(2027, 1, 1, 5, 0, 0, TimeSpan.Zero);
        Assert.Equal("PLAN-20260005", (await CrearAsync(cliente)).Numero);

        reloj.Ahora = new DateTimeOffset(2027, 1, 1, 7, 0, 0, TimeSpan.Zero);
        Assert.Equal("PLAN-20270001", (await CrearAsync(cliente)).Numero);
        Assert.Equal("PLAN-20270002", (await CrearAsync(cliente)).Numero);
    }

    [Fact]
    public async Task Creaciones_simultaneas_obtienen_numeros_unicos_y_correlativos()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));
        using var fabrica = new FabricaApi(_datos.Ruta, Rutas.DatosSemilla, reloj);
        using var cliente = fabrica.CreateClient();

        var creados = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => CrearAsync(cliente)));

        Assert.Equal(Enumerable.Range(4, 25), creados.Select(c => c.Secuencia).Order());
        Assert.Equal(25, creados.Select(c => c.Numero).Distinct().Count());
        // Cada creación dejó su movimiento en la bitácora, en la misma transacción.
        using var alcance = fabrica.Services.CreateScope();
        var movimientos = await alcance.ServiceProvider.GetRequiredService<IRepositorio<Movimiento>>().ListarAsync();
        Assert.All(creados, c => Assert.Single(movimientos, m => m.ConsolidadoId == c.Id && m.Tipo == TipoMovimiento.Creacion));
    }

    [Fact]
    public async Task Se_rechaza_al_superar_9999_en_el_anio()
    {
        Directory.CreateDirectory(_datos.Ruta);
        await File.WriteAllTextAsync(_datos.Archivo("consolidados.json"), JsonSerializer.Serialize(new[]
        {
            new DocumentoConsolidado { Id = "CON-X", Version = 1, Numero = "PLAN-20269999", Anio = 2026, Secuencia = 9999, AnalistaId = "a.perez" },
        }, Json));
        var reloj = new RelojFijo(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));
        using var fabrica = new FabricaApi(_datos.Ruta, Rutas.DatosSemilla, reloj);
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.SendAsync(Crear());

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("9999", await respuesta.Content.ReadAsStringAsync());
    }
}

/// <summary>Permisos en el servidor y visibilidad por bandeja (decisiones D6 y D10), con los datos semilla.</summary>
public sealed class PermisosConsolidadoPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public PermisosConsolidadoPruebas()
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

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, string? usuario)
    {
        var mensaje = new HttpRequestMessage(metodo, ruta);
        if (usuario is not null)
            mensaje.Headers.Add("X-Usuario-Prueba", usuario);
        return _cliente.SendAsync(mensaje);
    }

    private async Task<List<ResumenConsolidado>> Listar(string usuario)
    {
        var respuesta = await Enviar(HttpMethod.Get, "/api/consolidados", usuario);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<ResumenConsolidado>>(Json))!;
    }

    [Theory]
    [InlineData("j.ramos", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.Forbidden)]
    [InlineData("a.perez", HttpStatusCode.Created)]
    [InlineData(null, HttpStatusCode.Created)] // Sin encabezado: usuario.prueba (Analista y Administrador).
    [InlineData("nadie", HttpStatusCode.Unauthorized)]
    [InlineData("inactivo", HttpStatusCode.Unauthorized)]
    [InlineData("A.Perez<script>", HttpStatusCode.Unauthorized)]
    public async Task Solo_un_analista_registrado_y_activo_crea_consolidados(string? usuario, HttpStatusCode esperado) =>
        Assert.Equal(esperado, (await Enviar(HttpMethod.Post, "/api/consolidados", usuario)).StatusCode);

    [Fact]
    public async Task Cada_bandeja_ve_solo_lo_que_le_corresponde()
    {
        // a.perez: PLAN-1 (aprobado) es suyo; PLAN-2 es suyo pero está VERIFICADO y salió de su bandeja.
        Assert.Equal(["PLAN-20260001"], (await Listar("a.perez")).Select(c => c.Numero));
        // m.lopez: solo su PLAN-3 (EN REGISTRO). No ve los de a.perez.
        Assert.Equal(["PLAN-20260003"], (await Listar("m.lopez")).Select(c => c.Numero));
        // Oficial Jurídico: VERIFICADOS y APROBADOS, no lo que está EN REGISTRO.
        Assert.Equal(["PLAN-20260002", "PLAN-20260001"], (await Listar("j.ramos")).Select(c => c.Numero));
        // Administrador: todos, en consulta.
        Assert.Equal(["PLAN-20260003", "PLAN-20260002", "PLAN-20260001"], (await Listar("admin")).Select(c => c.Numero));
    }

    [Theory]
    [InlineData("a.perez", "CON-0002")] // Suyo, pero VERIFICADO: solo lo ve el Oficial Jurídico.
    [InlineData("m.lopez", "CON-0001")] // De otro analista.
    [InlineData("j.ramos", "CON-0003")] // EN REGISTRO: todavía no le llega.
    public async Task Un_consolidado_ajeno_a_la_bandeja_responde_como_inexistente(string usuario, string id) =>
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"/api/consolidados/{id}", usuario)).StatusCode);

    [Fact]
    public async Task El_listado_muestra_cada_tipo_documental_una_sola_vez()
    {
        var plan2 = (await Listar("j.ramos")).Single(c => c.Numero == "PLAN-20260002");

        Assert.Equal(["Certificación", "Constancia de Obligados Tributarios", "Resolución"], plan2.TiposDocumento);
        Assert.Equal(3, plan2.TotalPlantillas);
        Assert.Equal(EstadoConsolidado.Verificado, plan2.Estado);
    }

    [Fact]
    public async Task El_detalle_conserva_presentaciones_devoluciones_y_bitacora()
    {
        var respuesta = await Enviar(HttpMethod.Get, "/api/consolidados/CON-0002", "j.ramos");
        var detalle = (await respuesta.Content.ReadFromJsonAsync<DetalleConsolidado>(Json))!;

        Assert.Equal([1, 2], detalle.Presentaciones.Select(p => p.Numero));
        var devolucion = Assert.Single(detalle.Devoluciones);
        Assert.Equal(1, devolucion.NumeroPresentacion);
        Assert.False(string.IsNullOrWhiteSpace(devolucion.Justificacion));
        Assert.Contains(devolucion.Observaciones, o => o is { PropuestaId: "PRO-0002", BloqueCodigo: "CONSIDERANDO" });
        Assert.Equal(
            [TipoMovimiento.Creacion, TipoMovimiento.PropuestaAgregada, TipoMovimiento.PropuestaAgregada, TipoMovimiento.EnvioVerificacion,
             TipoMovimiento.Devolucion, TipoMovimiento.PropuestaModificada, TipoMovimiento.EnvioVerificacion],
            detalle.Movimientos.Select(m => m.Tipo));
        // La Certificación va junto con su Resolución (decisión D3).
        Assert.Equal("PRO-0002", detalle.Propuestas.Single(p => p.TipoDocumentoId == "TDO-004").DerivadaDePropuestaId);
    }
}

/// <summary>Carga inicial (decisión D5) y coherencia de los datos semilla del Documento Consolidado.</summary>
public sealed class CargaInicialPruebas : IDisposable
{
    private readonly DirectorioTemporal _datos = new();

    public void Dispose() => _datos.Dispose();

    private async Task<T> Leer<T>(Func<IServiceProvider, Task<T>> lectura)
    {
        using var fabrica = new FabricaApi(_datos.Ruta, Rutas.DatosSemilla);
        using var alcance = fabrica.Services.CreateScope();
        return await lectura(alcance.ServiceProvider);
    }

    private static Task<IReadOnlyList<T>> Lista<T>(IServiceProvider s) where T : class, IEntidad =>
        s.GetRequiredService<IRepositorio<T>>().ListarAsync();

    [Fact]
    public async Task Cada_plantilla_queda_con_una_sola_version_vigente()
    {
        var (plantillas, versiones) = await Leer(async s => (await Lista<Plantilla>(s), await Lista<VersionPlantilla>(s)));

        Assert.All(plantillas, p => Assert.Single(versiones, v => v.PlantillaId == p.Id && v.Vigencia == Vigencia.Vigente));
        Assert.Empty(MigracionCargaInicial.Inconsistencias(plantillas, versiones));
        Assert.All(versiones, v => Assert.Equal(HuellaContenido.De(v.Contenido), v.Huella));

        // PLA-001 se migró con su contenido actual, sin consolidado de origen.
        var pla001 = versiones.Single(v => v.PlantillaId == "PLA-001");
        Assert.True(pla001.CargaInicial);
        Assert.Null(pla001.ConsolidadoId);
        Assert.Equal(1, pla001.NumeroVersion);

        // PLA-002 tiene historia: la versión 1 quedó NO VIGENTE y la 2, publicada por PLAN-20260001, es la vigente.
        var pla002 = versiones.Where(v => v.PlantillaId == "PLA-002").OrderBy(v => v.NumeroVersion).ToList();
        Assert.Equal([(1, Vigencia.NoVigente), (2, Vigencia.Vigente)], pla002.Select(v => (v.NumeroVersion, v.Vigencia)));
        Assert.Equal(pla002[0].Id, pla002[1].VersionAnteriorId);
        Assert.NotNull(pla002[0].FinVigencia);
        Assert.Equal("CON-0001", pla002[1].ConsolidadoId);
    }

    [Fact]
    public async Task Las_propuestas_y_presentaciones_semilla_quedan_completas()
    {
        var (versiones, propuestas, presentaciones) = await Leer(async s =>
            (await Lista<VersionPlantilla>(s), await Lista<Propuesta>(s), await Lista<Presentacion>(s)));

        foreach (var propuesta in propuestas.Where(p => p.Operacion == OperacionPropuesta.Modificar))
        {
            var origen = versiones.Single(v => v.Id == propuesta.VersionOrigenId);
            Assert.Equal(propuesta.PlantillaId, origen.PlantillaId);
            Assert.Equal(origen.Huella, propuesta.HuellaOrigen);
        }
        Assert.All(presentaciones, p => Assert.Equal(HuellaContenido.De(p.Propuestas), p.HuellaIntegridad));
    }

    [Fact]
    public async Task La_migracion_es_idempotente()
    {
        var antes = await Leer(async s => (await Lista<VersionPlantilla>(s)).Select(v => (v.Id, v.Version)).ToList());
        var historialAntes = await Leer(async s => (await Lista<RegistroHistorial>(s)).Count);

        // Segundo inicio de la API con los mismos datos: nada que completar.
        var despues = await Leer(async s => (await Lista<VersionPlantilla>(s)).Select(v => (v.Id, v.Version)).ToList());
        var historialDespues = await Leer(async s => (await Lista<RegistroHistorial>(s)).Count);

        Assert.Equal(antes, despues);
        Assert.Equal(historialAntes, historialDespues);
    }

    [Fact]
    public async Task Los_datos_semilla_cumplen_las_reglas_del_consolidado()
    {
        var (plantillas, consolidados, propuestas, devoluciones) = await Leer(async s =>
            (await Lista<Plantilla>(s), await Lista<DocumentoConsolidado>(s), await Lista<Propuesta>(s), await Lista<Devolucion>(s)));

        // Un consolidado en cada estado, con números únicos y bien formados.
        Assert.Equal([EstadoConsolidado.EnRegistro, EstadoConsolidado.Verificado, EstadoConsolidado.Aprobado],
            consolidados.Select(c => c.Estado).Distinct().Order());
        Assert.All(consolidados, c => Assert.Equal(NumeroPlan.Formatear(c.Anio, c.Secuencia), c.Numero));
        Assert.Equal(consolidados.Count, consolidados.Select(c => c.Numero).Distinct().Count());

        // Cada propuesta pertenece a un consolidado existente y cada plantilla aparece una vez por consolidado.
        Assert.All(propuestas, p => Assert.Contains(consolidados, c => c.Id == p.ConsolidadoId));
        Assert.All(propuestas.GroupBy(p => p.ConsolidadoId), g =>
            Assert.Equal(g.Count(), g.Select(p => (p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId)).Distinct().Count()));

        // D4: ninguna plantilla está en dos consolidados abiertos.
        var abiertas = propuestas.Where(p => consolidados.Single(c => c.Id == p.ConsolidadoId).Estado != EstadoConsolidado.Aprobado);
        Assert.All(abiertas.Where(p => p.PlantillaId is not null).GroupBy(p => p.PlantillaId), g => Assert.Single(g));

        // Una creación no puede chocar con una plantilla existente.
        Assert.All(propuestas.Where(p => p.Operacion == OperacionPropuesta.Crear), p => Assert.DoesNotContain(plantillas, x =>
            (x.RubroId, x.SubRubroId, x.TipoDocumentoId, x.ClaseDocumentoId) == (p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId)));

        // D3: cada Certificación derivada apunta a una Resolución del mismo consolidado y la misma combinación.
        foreach (var derivada in propuestas.Where(p => p.DerivadaDePropuestaId is not null))
        {
            var resolucion = propuestas.Single(p => p.Id == derivada.DerivadaDePropuestaId);
            Assert.Equal((resolucion.ConsolidadoId, resolucion.RubroId, resolucion.SubRubroId, resolucion.ClaseDocumentoId),
                (derivada.ConsolidadoId, derivada.RubroId, derivada.SubRubroId, derivada.ClaseDocumentoId));
        }

        Assert.All(devoluciones, d => Assert.False(string.IsNullOrWhiteSpace(d.Justificacion)));
    }
}
