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
/// Texto condicionado dentro de un párrafo: un fragmento que solo aparece en el documento si se cumple su condición, con
/// las mismas reglas que la condición del párrafo completo.
/// </summary>
public sealed class CondicionTextoPruebas : IAsyncLifetime, IDisposable
{
    private static readonly JsonSerializerOptions Json = OpcionesJson.Predeterminadas;
    private string _propuesta = "";
    private readonly DirectorioTemporal _datos = new();
    private readonly FabricaApi _fabrica;
    private readonly HttpClient _cliente;

    public CondicionTextoPruebas()
    {
        _fabrica = new FabricaApi(_datos.Ruta);
        _cliente = _fabrica.CreateClient();
    }

    public async Task InitializeAsync() => _propuesta = (await Consolidados.ModificarAsync(_cliente, "PLA-001")).Single().Id;

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _cliente.Dispose();
        _fabrica.Dispose();
        _datos.Dispose();
    }

    private static NodoContenido TextoNodo(string texto, GrupoCondiciones? condicion = null) => new()
    {
        Tipo = "text",
        Texto = texto,
        Marcas = condicion is null ? null : [Marca(condicion)],
    };

    private static MarcaContenido Marca(GrupoCondiciones condicion) => new()
    {
        Tipo = "condicion",
        Atributos = new Dictionary<string, JsonElement> { ["condicion"] = JsonSerializer.SerializeToElement(condicion, Json) },
    };

    private static GrupoCondiciones SiBeneficiario(string valor) => new()
    {
        OperadorLogico = OperadorLogico.Y,
        Reglas = [new ReglaCondicion { CampoId = "beneficiario", Operador = OperadorComparacion.IgualA, Valor = valor }],
    };

    private static string Texto(NodoContenido nodo) =>
        nodo.Tipo == "text" ? nodo.Texto ?? "" : string.Concat((nodo.Contenido ?? []).Select(Texto));

    private Task<HttpResponseMessage> CrearPorTanto(params NodoContenido[] enLinea) =>
        _cliente.PostAsJsonAsync($"/api/propuestas/{_propuesta}/parrafos",
            new DatosParrafo(CodigosBloque.PorTanto, 0,
                new NodoContenido { Tipo = "doc", Contenido = [new NodoContenido { Tipo = "paragraph", Contenido = [.. enLinea] }] },
                false, null, 0), Json);

    private async Task<string> PorTanto(string? solicitud)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var propuesta = (await alcance.ServiceProvider.GetRequiredService<IRepositorio<Propuesta>>().ObtenerAsync(_propuesta))!;
        var (plantilla, bloques) = ConstructorContenido.ADocumento(propuesta.ContenidoPropuesto, propuesta.Id, propuesta.Nombre,
            propuesta.RubroId, propuesta.SubRubroId, propuesta.TipoDocumentoId, propuesta.ClaseDocumentoId);
        var documento = await alcance.ServiceProvider.GetRequiredService<ServicioDocumento>().GenerarAsync(plantilla, bloques, solicitud);
        return Texto(documento.Secciones.Single(s => s.Codigo == CodigosBloque.PorTanto).Parrafos.Single().Contenido);
    }

    [Fact]
    public async Task El_texto_condicionado_aparece_solo_si_se_cumple_su_condicion()
    {
        // SOL-001 es del «Beneficiario Ficticio Uno (prueba)»; SOL-002, de otro beneficiario.
        (await CrearPorTanto(
            TextoNodo("Se aprueba "),
            TextoNodo("con carácter prioritario", SiBeneficiario("Beneficiario Ficticio Uno (prueba)")),
            TextoNodo(" la solicitud."))).EnsureSuccessStatusCode();

        Assert.Equal("Se aprueba con carácter prioritario la solicitud.", await PorTanto("SOL-001"));
        // Al omitirlo no queda un espacio doble.
        Assert.Equal("Se aprueba la solicitud.", await PorTanto("SOL-002"));
        // Sin solicitud se muestra, como los párrafos condicionados.
        Assert.Equal("Se aprueba con carácter prioritario la solicitud.", await PorTanto(null));
    }

    [Fact]
    public async Task Un_campo_dentro_del_texto_condicionado_se_sustituye()
    {
        var campo = new NodoContenido
        {
            Tipo = "campo",
            Atributos = new Dictionary<string, JsonElement> { ["campoId"] = JsonSerializer.SerializeToElement("beneficiario") },
            Marcas = [Marca(SiBeneficiario("Beneficiario Ficticio Uno (prueba)"))],
        };
        (await CrearPorTanto(TextoNodo("A favor de "), campo, TextoNodo("."))).EnsureSuccessStatusCode();

        Assert.Equal("A favor de Beneficiario Ficticio Uno (prueba).", await PorTanto("SOL-001"));
        Assert.Equal("A favor de .", await PorTanto("SOL-002"));
    }

    [Fact]
    public async Task Una_condicion_de_texto_invalida_o_vacia_se_rechaza()
    {
        var invalida = new GrupoCondiciones
        {
            OperadorLogico = OperadorLogico.Y,
            Reglas = [new ReglaCondicion { CampoId = "monto_exoneracion", Operador = OperadorComparacion.Contiene, Valor = "1" }],
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearPorTanto(TextoNodo("Texto "), TextoNodo("condicionado", invalida))).StatusCode);

        var vacia = new GrupoCondiciones { OperadorLogico = OperadorLogico.Y };
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearPorTanto(TextoNodo("Texto "), TextoNodo("condicionado", vacia))).StatusCode);
    }
}
