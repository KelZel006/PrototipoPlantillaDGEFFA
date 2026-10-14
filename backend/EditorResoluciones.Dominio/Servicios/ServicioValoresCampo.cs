using System.Globalization;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>
/// Valores distintos que tiene un campo en los datos de prueba. Sirven de sugerencias al escribir
/// el valor de una condición (por ejemplo, los estados de solicitud existentes).
/// </summary>
public sealed class ServicioValoresCampo(
    IRepositorio<Campo> campos,
    IRepositorio<Solicitud> solicitudes,
    IRepositorio<DetalleBien> bienes)
{
    private const int MaximoValores = 50;

    public async Task<IReadOnlyList<string>> ValoresAsync(string campoId, CancellationToken ct = default)
    {
        var campo = await campos.ObtenerAsync(campoId, ct) ?? throw new EntidadNoEncontradaException(campos.Coleccion, campoId);
        var propiedad = OrigenesDatos.Propiedad(campo.Origen.Coleccion, campo.Origen.Propiedad);
        if (propiedad is null)
            return [];

        IEnumerable<object> registros = campo.Origen.Coleccion switch
        {
            OrigenesDatos.Solicitudes => await solicitudes.ListarAsync(ct),
            OrigenesDatos.DetallesBienes => await bienes.ListarAsync(ct),
            _ => [],
        };

        return registros
            .Select(r => Formatear(propiedad.GetValue(r)))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.CurrentCulture)
            .Take(MaximoValores)
            .ToList();
    }

    /// <summary>Formato que acepta una condición: fechas AAAA-MM-DD, números con punto, booleanos true/false.</summary>
    private static string? Formatear(object? valor) => valor switch
    {
        null => null,
        string texto when string.IsNullOrWhiteSpace(texto) => null,
        string texto => texto,
        DateOnly fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool logico => logico ? "true" : "false",
        IFormattable numero => numero.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString(),
    };
}
