using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Infraestructura.Json;

namespace EditorResoluciones.Pruebas;

public class RepositorioJsonPruebas
{
    [Fact]
    public async Task Crear_genera_identificador_con_prefijo_y_version_1()
    {
        using var datos = new DirectorioTemporal();
        var repositorio = new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta));

        var rubro = await repositorio.CrearAsync(new Rubro { Nombre = "Rubro de prueba", Version = 7 });

        Assert.StartsWith("RUB-", rubro.Id);
        Assert.Equal(1, rubro.Version);
    }

    [Fact]
    public async Task Crear_con_identificador_repetido_genera_conflicto()
    {
        using var datos = new DirectorioTemporal();
        var repositorio = new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta));
        await repositorio.CrearAsync(new Rubro { Id = "RUB-X", Nombre = "Uno" });

        await Assert.ThrowsAsync<ConflictoException>(() => repositorio.CrearAsync(new Rubro { Id = "RUB-X", Nombre = "Dos" }));
    }

    [Fact]
    public async Task Actualizar_incrementa_version_y_rechaza_versiones_desactualizadas()
    {
        using var datos = new DirectorioTemporal();
        var repositorio = new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta));
        var creado = await repositorio.CrearAsync(new Rubro { Nombre = "Original" });

        var actualizado = await repositorio.ActualizarAsync(new Rubro { Id = creado.Id, Version = 1, Nombre = "Cambio A" });
        Assert.Equal(2, actualizado.Version);

        // Una segunda edición basada en la versión 1 no debe pisar "Cambio A".
        await Assert.ThrowsAsync<ConflictoException>(() =>
            repositorio.ActualizarAsync(new Rubro { Id = creado.Id, Version = 1, Nombre = "Cambio B" }));
        Assert.Equal("Cambio A", (await repositorio.ObtenerAsync(creado.Id))!.Nombre);
    }

    [Fact]
    public async Task Eliminar_valida_existencia_y_version()
    {
        using var datos = new DirectorioTemporal();
        var repositorio = new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta));
        var creado = await repositorio.CrearAsync(new Rubro { Nombre = "Para eliminar" });

        await Assert.ThrowsAsync<EntidadNoEncontradaException>(() => repositorio.EliminarAsync("RUB-NOEXISTE", null));
        await Assert.ThrowsAsync<ConflictoException>(() => repositorio.EliminarAsync(creado.Id, 5));

        await repositorio.EliminarAsync(creado.Id, 1);
        Assert.Null(await repositorio.ObtenerAsync(creado.Id));
    }

    [Fact]
    public async Task Lote_con_un_conflicto_no_aplica_ningun_cambio()
    {
        using var datos = new DirectorioTemporal();
        var repositorio = new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta));
        var a = await repositorio.CrearAsync(new Rubro { Nombre = "A" });
        var b = await repositorio.CrearAsync(new Rubro { Nombre = "B" });
        await repositorio.ActualizarAsync(new Rubro { Id = b.Id, Version = 1, Nombre = "B2" });

        // b viene con versión 1 pero ya está en la 2: no se crea C ni se cambia A.
        await Assert.ThrowsAsync<ConflictoException>(() => repositorio.AplicarLoteAsync(
            [new Rubro { Nombre = "C" }],
            [new Rubro { Id = a.Id, Version = 1, Nombre = "A2" }, new Rubro { Id = b.Id, Version = 1, Nombre = "B3" }]));

        var guardados = await repositorio.ListarAsync();
        Assert.Equal(["A", "B2"], guardados.Select(r => r.Nombre));
    }

    [Fact]
    public async Task Lote_valido_crea_y_actualiza_a_la_vez()
    {
        using var datos = new DirectorioTemporal();
        var repositorio = new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta));
        var a = await repositorio.CrearAsync(new Rubro { Nombre = "A" });

        var lote = await repositorio.AplicarLoteAsync(
            [new Rubro { Nombre = "Nuevo" }],
            [new Rubro { Id = a.Id, Version = 1, Nombre = "A2" }]);

        Assert.StartsWith("RUB-", lote.Creados[0].Id);
        Assert.Equal(2, lote.Actualizados[0].Version);
        Assert.Equal(["A2", "Nuevo"], (await repositorio.ListarAsync()).Select(r => r.Nombre));
    }

    [Fact]
    public async Task Los_datos_persisten_al_crear_un_almacen_nuevo()
    {
        using var datos = new DirectorioTemporal();
        var creado = await new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta))
            .CrearAsync(new Rubro { Nombre = "Persistente con tildes y eñes" });

        // Un almacén nuevo equivale a reiniciar la aplicación.
        var leido = await new RepositorioJson<Rubro>(Rutas.CrearAlmacen(datos.Ruta)).ObtenerAsync(creado.Id);

        Assert.Equal("Persistente con tildes y eñes", leido!.Nombre);
        Assert.Contains("eñes", await File.ReadAllTextAsync(datos.Archivo("rubros.json")));
    }
}
