using System.Text.Json;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>
/// Lista cerrada de colecciones y propiedades de las que un campo dinámico puede obtener su valor.
/// Los usuarios eligen entre estas opciones; nunca escriben consultas ni código.
/// </summary>
public static class OrigenesDatos
{
    public const string Solicitudes = "solicitudes";
    public const string DetallesBienes = "detallesBienes";

    private static readonly HashSet<string> PropiedadesExcluidas = ["version"];

    public static IReadOnlyDictionary<string, Type> Colecciones { get; } = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [Solicitudes] = typeof(Solicitud),
        [DetallesBienes] = typeof(DetalleBien),
    };

    /// <summary>Propiedades disponibles de una colección, en camelCase.</summary>
    public static IReadOnlyList<string> Propiedades(string coleccion) =>
        Colecciones.TryGetValue(coleccion, out var tipo)
            ? tipo.GetProperties()
                .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
                .Where(nombre => !PropiedadesExcluidas.Contains(nombre))
                .ToList()
            : [];

    public static bool PropiedadExiste(string coleccion, string propiedad) =>
        Propiedades(coleccion).Contains(propiedad, StringComparer.Ordinal);
}
