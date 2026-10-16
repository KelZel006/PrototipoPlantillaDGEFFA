using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Infraestructura.Pdf;
using Microsoft.Extensions.DependencyInjection;

namespace EditorResoluciones.Pruebas;

/// <summary>Texto corrido de la Certificación: todo seguido, solo las tablas conservan su forma.</summary>
public class TextoCorridoPruebas
{
    private static NodoContenido Texto(string texto, bool negrita = false) => new()
    {
        Tipo = "text",
        Texto = texto,
        Marcas = negrita ? [new MarcaContenido { Tipo = "bold" }] : null,
    };

    private static NodoContenido Parrafo(params NodoContenido[] contenido) => new() { Tipo = "paragraph", Contenido = [.. contenido] };

    private static NodoContenido Tabla() => new()
    {
        Tipo = "table",
        Contenido = [new NodoContenido { Tipo = "tableRow", Contenido = [new NodoContenido { Tipo = "tableCell", Contenido = [Parrafo(Texto("celda"))] }] }],
    };

    private static ParrafoDocumento Bloque(params NodoContenido[] bloques) =>
        new("PAR", 1, new NodoContenido { Tipo = "doc", Contenido = [.. bloques] });

    private static SeccionDocumento Seccion(Seccion seccion, params ParrafoDocumento[] parrafos) => new(seccion, parrafos, []);

    private static string Plano(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Plano));

    [Fact]
    public void Une_todo_el_texto_en_un_solo_parrafo_con_los_titulos_en_negrita()
    {
        var bloques = TextoCorrido.Unir(
        [
            Seccion(Dominio.Modelos.Seccion.Cabecera,
                Bloque(Parrafo(Texto("RESOLUCIÓN No. 1")), Parrafo(Texto("Documento de prueba."))),
                Bloque(Parrafo(Texto("VISTO: "), Texto("la solicitud"), new NodoContenido { Tipo = "hardBreak" }, Texto("presentada.")))),
            Seccion(Dominio.Modelos.Seccion.Considerando, Bloque(Parrafo(Texto("CONSIDERANDO(1): ", true), Texto("Que…")))),
            Seccion(Dominio.Modelos.Seccion.PorTanto, Bloque(Parrafo(Texto("Esta Dirección.")))),
            Seccion(Dominio.Modelos.Seccion.Resuelve, Bloque(Parrafo(Texto("PRIMERO: ", true), Texto("Autorizar.")), Parrafo())),
        ]);

        var parrafo = Assert.Single(bloques);
        Assert.Equal("paragraph", parrafo.Tipo);
        Assert.Equal(
            "RESOLUCIÓN No. 1 Documento de prueba. VISTO: la solicitud presentada. CONSIDERANDO(1): Que… POR TANTO: Esta Dirección. RESUELVE: PRIMERO: Autorizar.",
            Plano(parrafo));
        Assert.Contains(parrafo.Contenido!, n => n.Texto == "RESUELVE:" && n.Marcas!.Any(m => m.Tipo == "bold"));
    }

    [Fact]
    public void Las_tablas_cortan_el_texto_y_conservan_su_forma()
    {
        var tabla = Tabla();
        var bloques = TextoCorrido.Unir(
        [
            Seccion(Dominio.Modelos.Seccion.Considerando,
                Bloque(Parrafo(Texto("Que los bienes son:")), tabla, Parrafo()),
                Bloque(Parrafo(Texto("Que el monto…")))),
        ]);

        Assert.Collection(bloques,
            b => Assert.Equal("Que los bienes son:", Plano(b)),
            b => Assert.Same(tabla, b),
            b => Assert.Equal("Que el monto…", Plano(b)));
    }

    [Fact]
    public async Task La_Certificacion_genera_el_documento_corrido()
    {
        using var datos = new DirectorioTemporal();
        await using var fabrica = new FabricaApi(datos.Ruta, Rutas.DatosSemilla);
        using var alcance = fabrica.Services.CreateScope();
        var servicio = alcance.ServiceProvider.GetRequiredService<ServicioDocumento>();

        var resolucion = await servicio.GenerarAsync("PLA-001", "SOL-001");
        var certificacion = await servicio.GenerarAsync("PLA-004", "SOL-001");

        Assert.False(resolucion.Diseno.TextoCorrido);
        Assert.True(certificacion.Diseno.TextoCorrido);
        var pdf = new GeneradorPdfMigraDoc().Generar(certificacion);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }
}
