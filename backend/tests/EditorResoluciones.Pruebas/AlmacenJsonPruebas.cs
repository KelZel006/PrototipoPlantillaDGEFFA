using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Infraestructura.Json;

namespace EditorResoluciones.Pruebas;

public class AlmacenJsonPruebas
{
    [Fact]
    public async Task Archivo_inexistente_sin_semilla_devuelve_lista_vacia()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Rutas.CrearAlmacen(datos.Ruta);

        var rubros = await almacen.LeerAsync<Rubro>("rubros");

        Assert.Empty(rubros);
    }

    [Fact]
    public async Task Archivo_inexistente_con_semilla_copia_los_datos_iniciales()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Rutas.CrearAlmacen(datos.Ruta, Rutas.DatosSemilla);

        var rubros = await almacen.LeerAsync<Rubro>("rubros");

        Assert.Equal(3, rubros.Count);
        Assert.True(File.Exists(datos.Archivo("rubros.json")));
    }

    [Fact]
    public async Task Escritura_conserva_respaldo_y_no_deja_temporales()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Rutas.CrearAlmacen(datos.Ruta);
        var repositorio = new RepositorioJson<Rubro>(almacen);

        await repositorio.CrearAsync(new Rubro { Nombre = "Primero" });
        await repositorio.CrearAsync(new Rubro { Nombre = "Segundo" });

        var respaldo = await File.ReadAllTextAsync(datos.Archivo("rubros.json.bak"));
        Assert.Contains("Primero", respaldo);
        Assert.DoesNotContain("Segundo", respaldo);
        Assert.False(File.Exists(datos.Archivo("rubros.json.tmp")));
    }

    [Fact]
    public async Task Archivo_danado_no_se_sobrescribe_y_se_copia_a_cuarentena_una_vez()
    {
        using var datos = new DirectorioTemporal();
        const string contenidoDanado = "[ { \"id\": \"RUB-1\", ";
        await File.WriteAllTextAsync(datos.Archivo("rubros.json"), contenidoDanado);
        var almacen = Rutas.CrearAlmacen(datos.Ruta);
        var repositorio = new RepositorioJson<Rubro>(almacen);

        var lectura = await Assert.ThrowsAsync<AlmacenamientoException>(() => repositorio.ListarAsync());
        await Assert.ThrowsAsync<AlmacenamientoException>(() => repositorio.ListarAsync());
        await Assert.ThrowsAsync<AlmacenamientoException>(() => repositorio.CrearAsync(new Rubro { Nombre = "Nuevo" }));

        Assert.Contains("dañado", lectura.Message);
        Assert.Equal(contenidoDanado, await File.ReadAllTextAsync(datos.Archivo("rubros.json")));
        Assert.Single(Directory.GetFiles(Path.Combine(datos.Ruta, "cuarentena")));
    }

    [Fact]
    public async Task Archivo_vacio_se_considera_danado()
    {
        using var datos = new DirectorioTemporal();
        await File.WriteAllTextAsync(datos.Archivo("rubros.json"), "   ");
        var almacen = Rutas.CrearAlmacen(datos.Ruta);

        await Assert.ThrowsAsync<AlmacenamientoException>(() => almacen.LeerAsync<Rubro>("rubros"));
    }

    [Fact]
    public async Task Escrituras_simultaneas_no_pierden_registros()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Rutas.CrearAlmacen(datos.Ruta);
        var repositorio = new RepositorioJson<Rubro>(almacen);

        await Task.WhenAll(Enumerable.Range(1, 50)
            .Select(i => Task.Run(() => repositorio.CrearAsync(new Rubro { Nombre = $"Rubro {i}" }))));

        var guardados = await Rutas.CrearAlmacen(datos.Ruta).LeerAsync<Rubro>("rubros");
        Assert.Equal(50, guardados.Count);
        Assert.Equal(50, guardados.Select(r => r.Id).Distinct().Count());
    }

    [Fact]
    public async Task Operacion_fallida_no_modifica_el_archivo()
    {
        using var datos = new DirectorioTemporal();
        var almacen = Rutas.CrearAlmacen(datos.Ruta);
        var repositorio = new RepositorioJson<Rubro>(almacen);
        var rubro = await repositorio.CrearAsync(new Rubro { Nombre = "Original" });
        var antes = await File.ReadAllTextAsync(datos.Archivo("rubros.json"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            almacen.ModificarAsync<Rubro, bool>("rubros", lista =>
            {
                lista.Clear();
                throw new InvalidOperationException("falla simulada");
            }));

        Assert.Equal(antes, await File.ReadAllTextAsync(datos.Archivo("rubros.json")));
        Assert.NotNull(await repositorio.ObtenerAsync(rubro.Id));
    }

    [Theory]
    [InlineData("../appsettings")]
    [InlineData("rubros.json")]
    [InlineData("desconocida")]
    public async Task Colecciones_no_registradas_se_rechazan(string coleccion)
    {
        using var datos = new DirectorioTemporal();
        var almacen = Rutas.CrearAlmacen(datos.Ruta);

        await Assert.ThrowsAsync<ArgumentException>(() => almacen.LeerAsync<Rubro>(coleccion));
    }
}
