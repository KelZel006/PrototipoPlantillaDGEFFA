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
/// Formato del Dictamen: Encabezado / Visto, Fundamento de Hecho / Bases Legales, Fundamento Legal, Dictamina y Firma,
/// sin títulos ni numeración automáticos (los redacta el analista).
/// Un Dictamen hecho con el formato anterior pasa su texto a los bloques nuevos al modificarlo.
/// </summary>
public sealed class DictamenPruebas : IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public DictamenPruebas()
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

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    [Fact]
    public async Task El_Dictamen_tiene_sus_cinco_bloques_en_orden()
    {
        var bloques = await _cliente.GetFromJsonAsync<List<BloqueConfigurado>>("/api/tipos-documento/TDO-002/bloques", Json);

        Assert.Equal(["Encabezado / Visto", "Fundamento de Hecho / Bases Legales", "Fundamento Legal", "Dictamina", "Firmas"],
            bloques!.Select(b => b.Nombre));
        // Sin títulos ni numeración automáticos: cada parte la redacta el analista.
        Assert.All(bloques, b => Assert.Null(b.Titulo));
        Assert.All(bloques, b => Assert.Equal(NumeracionBloque.Ninguna, b.Numeracion));
        Assert.True(bloques[0].Obligatorio);
        Assert.True(bloques[4].Obligatorio);
    }

    [Fact]
    public async Task Al_modificar_un_Dictamen_del_formato_anterior_su_texto_pasa_a_los_bloques_nuevos()
    {
        var vigente = (await _cliente.GetFromJsonAsync<List<VersionPlantilla>>("/api/versiones-plantilla?vigencia=Vigente", Json))!
            .Single(v => v.PlantillaId == "PLA-002");
        int Parrafos(string codigo) => vigente.Contenido.Bloques.Single(b => b.Codigo == codigo).Parrafos.Count;

        var propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-002")).Single();

        var bloques = propuesta.ContenidoPropuesto.Bloques;
        Assert.Equal(["ENCABEZADO_VISTO", "FUNDAMENTO_HECHO", "FUNDAMENTO_LEGAL", "DICTAMINA", "FIRMAS"], bloques.Select(b => b.Codigo));
        Assert.Equal(Parrafos("CABECERA"), bloques[0].Parrafos.Count);
        Assert.Equal(Parrafos("CONSIDERANDO"), bloques[1].Parrafos.Count);
        Assert.Equal(Parrafos("POR_TANTO"), bloques[2].Parrafos.Count);
        Assert.Equal(Parrafos("RESUELVE"), bloques[3].Parrafos.Count);
        Assert.Equal(Parrafos("FIRMAS"), bloques[4].Parrafos.Count);
        // La versión vigente no cambia.
        var otraVez = (await _cliente.GetFromJsonAsync<List<VersionPlantilla>>("/api/versiones-plantilla?vigencia=Vigente", Json))!
            .Single(v => v.PlantillaId == "PLA-002");
        Assert.Equal(vigente.Huella, otraVez.Huella);

        // En el documento no se agregan títulos ni numeración: el texto sale tal como lo escribió el analista.
        using var alcance = _fabrica.Services.CreateScope();
        var guardada = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(propuesta.Id))!;
        var configurados = await alcance.ServiceProvider.GetRequiredService<CatalogoBloques>().DeTipoAsync("TDO-002");
        var (plantilla, paraDocumento) = ConstructorContenido.ADocumento(guardada.ContenidoPropuesto, guardada.Id, guardada.Nombre,
            guardada.RubroId, guardada.SubRubroId, guardada.TipoDocumentoId, guardada.ClaseDocumentoId, configurados);
        var documento = await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync(plantilla, paraDocumento, null);
        var dictamina = documento.Secciones.Single(s => s.Codigo == "DICTAMINA");
        Assert.Null(dictamina.Bloque.Titulo);
        Assert.All(dictamina.Parrafos, p => Assert.DoesNotMatch(@"^(PRIMERO|SEGUNDO|DICTAMINA)", Texto(p.Contenido)));
        Assert.StartsWith("Dictaminar favorablemente", Texto(dictamina.Parrafos[0].Contenido));
    }

    [Fact]
    public void Un_bloque_que_el_tipo_todavia_usa_no_se_traslada()
    {
        var contenido = new ContenidoPlantilla
        {
            Bloques = [new BloqueContenido { Codigo = CodigosBloque.Resuelve, Parrafos = [new ParrafoContenido { Id = "P1", Orden = 1 }] }],
        };
        // Un tipo que usa Resuelve y también Dictamina: Resuelve se queda donde está.
        var resolucion = CatalogoBloques.Predeterminados
            .Append(new BloqueConfigurado("DICTAMINA", "Dictamina", "DICTAMINA:", NumeracionBloque.Ordinal, false, "resuelve", false, 6, [CodigosBloque.Resuelve]))
            .ToList();

        Assert.Equal(0, ConstructorContenido.AdaptarABloques(contenido, resolucion));
        Assert.Equal("P1", contenido.Bloques.Single(b => b.Codigo == CodigosBloque.Resuelve).Parrafos.Single().Id);
    }
}
