using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Validacion;
using EditorResoluciones.Infraestructura.Json;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>Comprueba que los datos de prueba iniciales sean coherentes y cubran los escenarios necesarios.</summary>
public class DatosSemillaPruebas
{
    private static AlmacenJson Almacen(DirectorioTemporal datos) => Rutas.CrearAlmacen(datos.Ruta, Rutas.DatosSemilla);

    [Fact]
    public async Task Todas_las_colecciones_se_leen_y_sus_identificadores_son_unicos()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Almacen(datos);

        foreach (var definicion in ColeccionesJson.Todas)
        {
            Assert.True(File.Exists(Path.Combine(Rutas.DatosSemilla, definicion.Archivo)), $"Falta la semilla {definicion.Archivo}");
            var registros = await almacen.LeerAsync<System.Text.Json.JsonElement>(definicion.Nombre);
            var ids = registros.Select(r => r.GetProperty("id").GetString()).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }

    [Fact]
    public async Task Hay_solicitudes_con_cero_uno_y_varios_bienes()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Almacen(datos);
        var solicitudes = await almacen.LeerAsync<Solicitud>("solicitudes");
        var bienes = await almacen.LeerAsync<DetalleBien>("detallesBienes");

        var conteos = solicitudes.Select(s => bienes.Count(b => b.SolicitudId == s.Id)).ToList();

        Assert.Contains(0, conteos);
        Assert.Contains(1, conteos);
        Assert.Contains(conteos, c => c > 1);
        Assert.All(solicitudes, s => Assert.True(s.EsDatoPrueba));
        Assert.All(bienes, b => Assert.True(b.EsDatoPrueba));
    }

    [Fact]
    public async Task Hay_una_solicitud_con_datos_faltantes_para_probar_condiciones()
    {
        using var datos = new DirectorioTemporal();
        var solicitudes = await Almacen(datos).LeerAsync<Solicitud>("solicitudes");

        Assert.Contains(solicitudes, s => s.FechaResolucion is null && s.MontoExoneracion is null && s.Municipio is null);
    }

    [Fact]
    public async Task Los_campos_insertados_en_cada_parrafo_estan_declarados()
    {
        using var datos = new DirectorioTemporal();
        var parrafos = await Almacen(datos).LeerAsync<Parrafo>("parrafos");

        foreach (var parrafo in parrafos)
        {
            var insertados = Nodos(parrafo.Contenido)
                .Where(n => n.Tipo == "campo")
                .Select(n => n.Atributos!["campoId"].GetString())
                .ToHashSet();
            Assert.True(insertados.SetEquals(parrafo.CamposUsados), $"camposUsados no coincide con el contenido en {parrafo.Id}");
        }
    }

    [Fact]
    public async Task Cada_registro_semilla_cumple_las_reglas_del_servidor()
    {
        using var datos = new DirectorioTemporal();
        await using var fabrica = new FabricaApi(datos.Ruta);
        using var alcance = fabrica.Services.CreateScope();

        await ValidarColeccion<Rubro>(alcance.ServiceProvider);
        await ValidarColeccion<SubRubro>(alcance.ServiceProvider);
        await ValidarColeccion<TipoDocumento>(alcance.ServiceProvider);
        await ValidarColeccion<Plantilla>(alcance.ServiceProvider);
        await ValidarColeccion<Campo>(alcance.ServiceProvider);
        await ValidarColeccion<Parrafo>(alcance.ServiceProvider);
        await ValidarColeccion<Solicitud>(alcance.ServiceProvider);
        await ValidarColeccion<DetalleBien>(alcance.ServiceProvider);
    }

    private static async Task ValidarColeccion<T>(IServiceProvider servicios) where T : class, IEntidad
    {
        var registros = await servicios.GetRequiredService<IRepositorio<T>>().ListarAsync();
        Assert.NotEmpty(registros);
        foreach (var registro in registros)
        {
            var resultado = new ResultadoValidacion();
            foreach (var validador in servicios.GetServices<IValidadorEntidad<T>>())
                await validador.ValidarGuardadoAsync(registro, resultado, CancellationToken.None);
            Assert.True(resultado.EsValido,
                $"{typeof(T).Name} {registro.Id}: {string.Join("; ", resultado.Errores.SelectMany(e => e.Value))}");
        }
    }

    private static IEnumerable<NodoContenido> Nodos(NodoContenido nodo) =>
        new[] { nodo }.Concat((nodo.Contenido ?? []).SelectMany(Nodos));
}
