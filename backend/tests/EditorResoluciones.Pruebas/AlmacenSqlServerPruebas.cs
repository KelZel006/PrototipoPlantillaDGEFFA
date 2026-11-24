using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Consolidados;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>
/// Solo se ejecuta si existe la variable de entorno <c>PRUEBAS_SQLSERVER</c> con la conexión a un SQL Server (sin base),
/// por ejemplo <c>Server=localhost;Integrated Security=True;TrustServerCertificate=True</c>.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string Variable = "PRUEBAS_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Defina {Variable} con la conexión a un SQL Server para ejecutar las pruebas del almacenamiento en SQL Server.";
    }
}

/// <summary>
/// La API sobre SQL Server: cada prueba crea una base temporal con <c>01-SEFH-esquema.sql</c> y <c>02-SEFH-datos.sql</c>, la usa
/// y la borra. Cubre lo que el almacenamiento JSON no puede probar: las tablas hijas, la concurrencia en el motor, las
/// transacciones del motor, las tablas derivadas y las reglas que protege la base.
/// </summary>
public sealed class AlmacenSqlServerPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly string? _servidor = Environment.GetEnvironmentVariable(SqlServerFactAttribute.Variable);
    private readonly string _base = $"PlantillasPrueba_{Guid.NewGuid():N}"[..34];
    private readonly FabricaApiSql? _fabrica;
    private readonly HttpClient? _cliente;

    public AlmacenSqlServerPruebas()
    {
        if (string.IsNullOrWhiteSpace(_servidor))
            return;
        Ejecutar(_servidor, $"CREATE DATABASE [{_base}] COLLATE Modern_Spanish_CI_AI");
        foreach (var script in new[] { "01-SEFH-esquema.sql", "02-SEFH-datos.sql" })
        {
            var lotes = Regex.Split(File.ReadAllText(Path.Combine(Rutas.ScriptsSefh, script)), @"^\s*GO\s*$", RegexOptions.Multiline);
            foreach (var lote in lotes.Where(l => !string.IsNullOrWhiteSpace(l)))
                Ejecutar(CadenaBase, lote);
        }
        _fabrica = new FabricaApiSql(CadenaBase);
        _cliente = _fabrica.CreateClient();
    }

    public void Dispose()
    {
        _cliente?.Dispose();
        _fabrica?.Dispose();
        if (string.IsNullOrWhiteSpace(_servidor))
            return;
        SqlConnection.ClearAllPools();
        Ejecutar(_servidor, $"ALTER DATABASE [{_base}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_base}]");
    }

    private string CadenaBase => new SqlConnectionStringBuilder(_servidor) { InitialCatalog = _base }.ConnectionString;

    private static void Ejecutar(string cadena, string sql)
    {
        using var conexion = new SqlConnection(cadena);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion) { CommandTimeout = 300 };
        comando.ExecuteNonQuery();
    }

    private T Consultar<T>(string sql)
    {
        using var conexion = new SqlConnection(CadenaBase);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        return (T)comando.ExecuteScalar()!;
    }

    private Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, string usuario, object? cuerpo = null)
    {
        var mensaje = new HttpRequestMessage(metodo, ruta) { Headers = { { "X-Usuario-Prueba", usuario } } };
        if (cuerpo is not null)
            mensaje.Content = JsonContent.Create(cuerpo, options: Json);
        return _cliente!.SendAsync(mensaje);
    }

    private static async Task<T> Leer<T>(Task<HttpResponseMessage> peticion)
    {
        var respuesta = await peticion;
        Assert.True(respuesta.IsSuccessStatusCode, $"{(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static NodoContenido Doc(string texto) => new()
    {
        Tipo = "doc",
        Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [new NodoContenido { Tipo = "text", Texto = texto }] }],
    };

    [SqlServerFact]
    public async Task Ciclo_completo_del_documento_se_guarda_en_las_tablas_SEFH()
    {
        // a.perez crea el documento PLAN-20260008 con una plantilla nueva (sin listas opcionales) y modifica PLA-006.
        var documento = await Leer<DocumentoConsolidado>(Enviar(HttpMethod.Post, "/api/consolidados", "a.perez"));
        Assert.Equal("PLAN-20260008", documento.Numero);
        var nueva = Assert.Single(await Leer<List<Propuesta>>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/propuestas/crear", "a.perez",
            new SolicitudCrearPlantilla("1800", "1802", null, null, null, null))));
        var modificada = Assert.Single(await Leer<List<Propuesta>>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/propuestas/modificar", "a.perez",
            new { plantillaId = "PLA-006" })));
        Assert.Equal(2, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_BLOQUEOS_PLANTILLA WHERE ID_CONS_SEQ = '{documento.Id}'"));
        Assert.Equal(8, Consultar<int>("SELECT Ultimo FROM dbo.SEFH_SECUENCIAS_PLAN WHERE Anio = 2026"));

        await Leer<ParrafoPropuesta>(Enviar(HttpMethod.Post, $"/api/propuestas/{nueva.Id}/parrafos", "a.perez",
            new DatosParrafo("RESUELVE", 0, Doc("Se admite la aclaratoria."), false, null, 0)));
        await Leer<ParrafoPropuesta>(Enviar(HttpMethod.Post, $"/api/propuestas/{modificada.Id}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 0, Doc("Que el beneficio ISV se acredita con la constancia."), false, null, 0)));

        // El logo se guarda en SEFH_ARCHIVOS y se lee igual.
        using var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Png);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formulario.Add(archivo, "archivo", "logo.png");
        var conLogo = await Leer<PlantillaEdicion>(_cliente!.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/propuestas/{nueva.Id}/logo")
        {
            Headers = { { "X-Usuario-Prueba", "a.perez" } },
            Content = formulario,
        }));
        Assert.Equal(Png.Length, Consultar<int>($"SELECT CAST(DATALENGTH(Contenido) AS int) FROM dbo.SEFH_ARCHIVOS WHERE ID_ARCH_SEQ = '{conLogo.LogoId}'"));
        Assert.Equal(Png, await (await Enviar(HttpMethod.Get, $"/api/archivos/{conLogo.LogoId}", "a.perez")).Content.ReadAsByteArrayAsync());

        // Envío, devolución con observación (tabla hija), corrección, reenvío y aprobación.
        await Leer<DocumentoConsolidado>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/enviar", "a.perez"));
        await Leer<DocumentoConsolidado>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/devolver", "j.ramos",
            new SolicitudDevolucion("Falta citar la norma.", [new DatosObservacion(modificada.Id, "CONSIDERANDO", "No cita la norma.", "Citar el artículo.")])));
        Assert.Equal("No cita la norma.", Consultar<string>(
            $"SELECT o.Problema FROM dbo.SEFH_OBSERVACIONES o JOIN dbo.SEFH_DEVOLUCIONES d ON d.ID_DEV_SEQ = o.ID_DEV_SEQ WHERE d.ID_CONS_SEQ = '{documento.Id}'"));
        await Leer<ParrafoPropuesta>(Enviar(HttpMethod.Post, $"/api/propuestas/{modificada.Id}/parrafos", "a.perez",
            new DatosParrafo("CONSIDERANDO", 1, Doc("Conforme al artículo 25."), false, null, 0)));
        await Leer<DocumentoConsolidado>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/enviar", "a.perez"));
        var detalle = await Leer<DetalleConsolidado>(Enviar(HttpMethod.Get, $"/api/consolidados/{documento.Id}", "j.ramos"));
        Assert.Equal("No cita la norma.", Assert.Single(Assert.Single(detalle.Devoluciones).Observaciones).Problema);
        await Leer<ResultadoAprobacion>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/aprobar", "j.ramos"));

        // Lo aprobado quedó publicado y ya no bloquea las plantillas.
        Assert.Equal("Aprobado", Consultar<string>($"SELECT ESTADO_DOCUMENTO FROM dbo.SEFH_PLANTILLAS_DOC_CAB WHERE ID_CONS_SEQ = '{documento.Id}'"));
        Assert.Equal(0, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_BLOQUEOS_PLANTILLA WHERE ID_CONS_SEQ = '{documento.Id}'"));
        Assert.Equal(2, Consultar<int>("SELECT COUNT(*) FROM dbo.SEFH_PLANTILLAS_CONSOLIDADO WHERE ID_PLA_SEQ = 'PLA-006'"));
        Assert.Equal(1, Consultar<int>("SELECT COUNT(*) FROM dbo.SEFH_PLANTILLAS WHERE ID_SUBRUBRO_SEQ = '1802' AND ID_TIPDOC_SEQ IS NULL AND ID_BEN_SEQ IS NULL"));
        Assert.Equal(2, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_PRESENTACIONES WHERE ID_CONS_SEQ = '{documento.Id}'"));

        // Auditoría: quién creó el documento, quién lo modificó por última vez (al aprobarlo) y su estado de registro.
        Assert.Equal("a.perez", Consultar<string>($"SELECT USU_CRE FROM dbo.SEFH_PLANTILLAS_DOC_CAB WHERE ID_CONS_SEQ = '{documento.Id}'"));
        Assert.Equal("j.ramos", Consultar<string>($"SELECT USU_MOD FROM dbo.SEFH_PLANTILLAS_DOC_CAB WHERE ID_CONS_SEQ = '{documento.Id}'"));
        Assert.Equal(1, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_PLANTILLAS_DOC_CAB WHERE ID_CONS_SEQ = '{documento.Id}' AND FEC_MOD IS NOT NULL AND ESTADO = 'ACTIVO'"));
    }

    [SqlServerFact]
    public async Task La_Resolucion_crea_su_Certificacion_y_le_copia_cada_cambio()
    {
        var documento = await Leer<DocumentoConsolidado>(Enviar(HttpMethod.Post, "/api/consolidados", "a.perez"));
        var agregadas = await Leer<List<Propuesta>>(Enviar(HttpMethod.Post, $"/api/consolidados/{documento.Id}/propuestas/crear", "a.perez",
            new SolicitudCrearPlantilla("1600", "1602", "TDO-001", "CDO-001", "BEN-002", null)));
        var resolucion = agregadas.Single(p => p.TipoDocumentoId == "TDO-001");
        var certificacion = agregadas.Single(p => p.TipoDocumentoId == "TDO-004");
        Assert.Equal(resolucion.Id, Consultar<string>($"SELECT DerivadaDePropuestaId FROM dbo.SEFH_PLANTILLA_DOC WHERE ID_PLADOC_SEQ = '{certificacion.Id}'"));

        await Leer<ParrafoPropuesta>(Enviar(HttpMethod.Post, $"/api/propuestas/{resolucion.Id}/parrafos", "a.perez",
            new DatosParrafo("RESUELVE", 0, Doc("RESOLUCIÓN: Notificar."), false, null, 0)));
        var copia = await Leer<List<ParrafoPropuesta>>(Enviar(HttpMethod.Get, $"/api/propuestas/{certificacion.Id}/parrafos?bloque=RESUELVE", "a.perez"));
        Assert.Contains(copia, p => JsonSerializer.Serialize(p.Contenido, Json).Contains("CERTIFICACIÓN"));
    }

    [SqlServerFact]
    public async Task Los_catalogos_guardan_sus_listas_y_rechazan_una_version_desactualizada()
    {
        var tipo = await Leer<TipoDocumento>(Enviar(HttpMethod.Post, "/api/tipos-documento", "admin", new TipoDocumento
        {
            Nombre = "Auto de prueba",
            Bloques = [new BloqueDeTipo { BloqueId = "CABECERA", Orden = 1, Obligatorio = true }, new BloqueDeTipo { BloqueId = "FIRMAS", Orden = 2 }],
        }));
        Assert.Equal(2, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_TIPO_DOCUMENTO_BLOQUES WHERE ID_TIPDOC_SEQ = '{tipo.Id}'"));

        // Se reemplazan sus bloques; con la versión anterior, el motor rechaza la escritura.
        var anterior = tipo.Version;
        tipo.Bloques = [new BloqueDeTipo { BloqueId = "RESUELVE", Orden = 1, Obligatorio = true }];
        var actualizado = await Leer<TipoDocumento>(Enviar(HttpMethod.Put, $"/api/tipos-documento/{tipo.Id}", "admin", tipo));
        Assert.Equal(anterior + 1, actualizado.Version);
        Assert.Equal("RESUELVE", Consultar<string>($"SELECT ID_BLODOC_SEQ FROM dbo.SEFH_TIPO_DOCUMENTO_BLOQUES WHERE ID_TIPDOC_SEQ = '{tipo.Id}'"));
        tipo.Version = anterior;
        Assert.Equal(HttpStatusCode.Conflict, (await Enviar(HttpMethod.Put, $"/api/tipos-documento/{tipo.Id}", "admin", tipo)).StatusCode);

        // Al eliminarlo se van también sus bloques.
        Assert.True((await Enviar(HttpMethod.Delete, $"/api/tipos-documento/{tipo.Id}?version={actualizado.Version}", "admin")).IsSuccessStatusCode);
        Assert.Equal(0, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_TIPO_DOCUMENTO_BLOQUES WHERE ID_TIPDOC_SEQ = '{tipo.Id}'"));
        Assert.Equal(0, Consultar<int>($"SELECT COUNT(*) FROM dbo.SEFH_TIPOS_DOCUMENTO WHERE ID_TIPDOC_SEQ = '{tipo.Id}'"));
    }

    [SqlServerFact]
    public async Task Una_transaccion_que_falla_no_deja_nada_guardado()
    {
        using var alcance = _fabrica!.Services.CreateScope();
        var unidad = alcance.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>();
        var antes = Consultar<int>("SELECT COUNT(*) FROM dbo.SEFH_MOVIMIENTOS");
        var versionAntes = Consultar<int>("SELECT Version FROM dbo.SEFH_PLANTILLAS_DOC_CAB WHERE ID_CONS_SEQ = 'CON-0003'");

        await Assert.ThrowsAsync<InvalidOperationException>(() => unidad.EjecutarAsync<bool>(async tx =>
        {
            await tx.CrearAsync(new Movimiento { ConsolidadoId = "CON-0003", Tipo = TipoMovimiento.Creacion, Usuario = "a.perez", Fecha = DateTimeOffset.UtcNow, Detalle = "Prueba." });
            var consolidado = (await tx.ListarAsync<DocumentoConsolidado>()).Single(c => c.Id == "CON-0003");
            consolidado.FechaActualizacion = DateTimeOffset.UtcNow;
            await tx.ActualizarAsync(consolidado);
            throw new InvalidOperationException("Falla a mitad de la operación.");
        }));

        Assert.Equal(antes, Consultar<int>("SELECT COUNT(*) FROM dbo.SEFH_MOVIMIENTOS"));
        Assert.Equal(versionAntes, Consultar<int>("SELECT Version FROM dbo.SEFH_PLANTILLAS_DOC_CAB WHERE ID_CONS_SEQ = 'CON-0003'"));
    }

    [SqlServerFact]
    public async Task Las_reglas_del_motor_llegan_como_conflicto()
    {
        using var alcance = _fabrica!.Services.CreateScope();
        var historial = alcance.ServiceProvider.GetRequiredService<IRepositorio<RegistroHistorial>>();
        var registro = (await historial.ListarAsync())[0];
        registro.Usuario = "alguien";

        var error = await Assert.ThrowsAsync<ConflictoException>(() => historial.ActualizarAsync(registro));
        Assert.Contains("El historial no se modifica", error.Message);
    }
}

/// <summary>API en memoria sobre una base SQL Server.</summary>
public sealed class FabricaApiSql(string cadenaConexion) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Almacenamiento:Proveedor", "SqlServer");
        builder.UseSetting("Almacenamiento:CadenaConexion", cadenaConexion);
    }
}
