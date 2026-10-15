using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Infraestructura.Json;

/// <summary>Colección lógica, el archivo fijo donde se guarda y el prefijo de sus identificadores.</summary>
public sealed record DefinicionColeccion(string Nombre, string Archivo, string Prefijo, Type TipoEntidad);

/// <summary>
/// Lista cerrada de colecciones. Los nombres de archivo nunca provienen del cliente:
/// una colección que no esté aquí no se puede leer ni escribir.
/// </summary>
public static class ColeccionesJson
{
    private static readonly DefinicionColeccion[] Definiciones =
    [
        new("rubros", "rubros.json", "RUB", typeof(Rubro)),
        new("subRubros", "subRubros.json", "SUB", typeof(SubRubro)),
        new("tiposDocumento", "tiposDocumento.json", "TDO", typeof(TipoDocumento)),
        new("plantillas", "plantillas.json", "PLA", typeof(Plantilla)),
        new("parrafos", "parrafos.json", "PAR", typeof(Parrafo)),
        new("campos", "campos.json", "CAM", typeof(Campo)),
        new("solicitudes", "solicitudes.json", "SOL", typeof(Solicitud)),
        new("detallesBienes", "detallesBienes.json", "BIE", typeof(DetalleBien)),
        new("historial", "historial.json", "HIS", typeof(RegistroHistorial)),
        new("archivos", "archivos.json", "ARC", typeof(ArchivoRecurso)),
    ];

    private static readonly Dictionary<string, DefinicionColeccion> PorNombre =
        Definiciones.ToDictionary(d => d.Nombre, StringComparer.Ordinal);

    private static readonly Dictionary<Type, DefinicionColeccion> PorTipo =
        Definiciones.ToDictionary(d => d.TipoEntidad);

    public static IReadOnlyList<DefinicionColeccion> Todas => Definiciones;

    public static DefinicionColeccion De<T>() =>
        PorTipo.TryGetValue(typeof(T), out var definicion)
            ? definicion
            : throw new InvalidOperationException($"El tipo {typeof(T).Name} no tiene una colección JSON registrada.");

    public static DefinicionColeccion PorNombreColeccion(string nombre) =>
        PorNombre.TryGetValue(nombre, out var definicion)
            ? definicion
            : throw new ArgumentException($"La colección '{nombre}' no está permitida.", nameof(nombre));
}
