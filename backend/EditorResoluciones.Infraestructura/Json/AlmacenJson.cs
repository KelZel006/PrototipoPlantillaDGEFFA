using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using EditorResoluciones.Dominio.Comun;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EditorResoluciones.Infraestructura.Json;

public sealed class OpcionesAlmacenJson
{
    /// <summary>Carpeta donde se leen y escriben los datos en ejecución.</summary>
    public string Directorio { get; set; } = "Data";

    /// <summary>Carpeta con los datos de prueba iniciales. Se copian cuando falta un archivo en <see cref="Directorio"/>.</summary>
    public string? DirectorioSemilla { get; set; } = "DatosSemilla";
}

/// <summary>
/// Lee y escribe colecciones completas en archivos JSON.
/// <list type="bullet">
/// <item>Un bloqueo por colección evita que dos escrituras simultáneas se pisen dentro del proceso.</item>
/// <item>La escritura es atómica: se escribe un temporal y se reemplaza el original, que queda como <c>.bak</c>.</item>
/// <item>Un archivo dañado nunca se sobrescribe: se copia a <c>cuarentena/</c> y se informa el error.</item>
/// <item>Las transacciones de varias colecciones (<see cref="UnidadDeTrabajoJson"/>) dejan un diario en
/// <c>transacciones/</c>; si el proceso se interrumpe al guardar, se completan al iniciar.</item>
/// </list>
/// </summary>
public sealed class AlmacenJson
{
    private const string ExtensionRespaldo = ".bak";
    private const string ExtensionTemporal = ".tmp";

    private readonly string _directorio;
    private readonly string? _directorioSemilla;
    private readonly ILogger<AlmacenJson> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _bloqueos = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (DateTime Modificado, string Destino)> _cuarentena = new(StringComparer.OrdinalIgnoreCase);

    public AlmacenJson(IOptions<OpcionesAlmacenJson> opciones, ILogger<AlmacenJson> logger)
    {
        _logger = logger;
        _directorio = Path.GetFullPath(opciones.Value.Directorio);
        _directorioSemilla = string.IsNullOrWhiteSpace(opciones.Value.DirectorioSemilla)
            ? null
            : Path.GetFullPath(opciones.Value.DirectorioSemilla);
        Directory.CreateDirectory(_directorio);
        RecuperarTransacciones();
    }

    public string Directorio => _directorio;

    /// <summary>Carpeta de los diarios de transacción pendientes.</summary>
    internal string DirectorioTransacciones => Path.Combine(_directorio, "transacciones");

    /// <summary>Permite una sola transacción de varias colecciones a la vez (evita bloqueos cruzados).</summary>
    internal SemaphoreSlim BloqueoTransacciones { get; } = new(1, 1);

    /// <summary>Solo para pruebas: se ejecuta antes de escribir cada colección de una transacción ya preparada.</summary>
    internal Func<string, Task>? AntesDeEscribirEnTransaccion { get; set; }

    /// <summary>Toma el bloqueo de la colección. Se libera con <see cref="Liberar"/>.</summary>
    internal async Task BloquearAsync(string coleccion, CancellationToken ct) => await Bloqueo(coleccion).WaitAsync(ct);

    internal void Liberar(string coleccion) => Bloqueo(coleccion).Release();

    public async Task<List<T>> LeerAsync<T>(string coleccion, CancellationToken ct = default)
    {
        var bloqueo = Bloqueo(coleccion);
        await bloqueo.WaitAsync(ct);
        try
        {
            return await LeerSinBloqueoAsync<T>(coleccion, ct);
        }
        finally
        {
            bloqueo.Release();
        }
    }

    /// <summary>
    /// Lee la colección, aplica la operación y guarda el resultado, todo dentro del bloqueo de la colección.
    /// Si la operación lanza una excepción, el archivo no se modifica.
    /// </summary>
    public async Task<TResultado> ModificarAsync<T, TResultado>(
        string coleccion,
        Func<List<T>, TResultado> operacion,
        CancellationToken ct = default)
    {
        var bloqueo = Bloqueo(coleccion);
        await bloqueo.WaitAsync(ct);
        try
        {
            var registros = await LeerSinBloqueoAsync<T>(coleccion, ct);
            var resultado = operacion(registros);
            await EscribirSinBloqueoAsync(coleccion, registros, ct);
            return resultado;
        }
        finally
        {
            bloqueo.Release();
        }
    }

    private SemaphoreSlim Bloqueo(string coleccion)
    {
        ColeccionesJson.PorNombreColeccion(coleccion); // Rechaza colecciones no registradas.
        return _bloqueos.GetOrAdd(coleccion, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>Ruta del archivo de una colección registrada; garantiza que quede dentro de la carpeta de datos.</summary>
    private string RutaArchivo(string coleccion)
    {
        var definicion = ColeccionesJson.PorNombreColeccion(coleccion);
        var ruta = Path.GetFullPath(Path.Combine(_directorio, definicion.Archivo));
        var raiz = _directorio.EndsWith(Path.DirectorySeparatorChar) ? _directorio : _directorio + Path.DirectorySeparatorChar;
        if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
            throw new AlmacenamientoException($"La ruta de la colección '{coleccion}' no es válida.");
        return ruta;
    }

    internal async Task<List<T>> LeerSinBloqueoAsync<T>(string coleccion, CancellationToken ct)
    {
        var ruta = RutaArchivo(coleccion);
        if (!File.Exists(ruta) && !CopiarSemilla(coleccion, ruta))
        {
            _logger.LogWarning("La colección {Coleccion} no tiene archivo ni semilla; se inicia vacía.", coleccion);
            return [];
        }

        string texto;
        try
        {
            texto = await File.ReadAllTextAsync(ruta, ct);
        }
        catch (IOException ex)
        {
            throw new AlmacenamientoException($"No se pudo leer el archivo de datos de '{coleccion}'.", ex);
        }

        try
        {
            var registros = string.IsNullOrWhiteSpace(texto)
                ? null
                : JsonSerializer.Deserialize<List<T>>(texto, OpcionesJson.Predeterminadas);
            return registros ?? throw new JsonException("El archivo está vacío o no contiene una lista.");
        }
        catch (JsonException ex)
        {
            var copia = PonerEnCuarentena(ruta, coleccion);
            _logger.LogError(ex, "Archivo de datos dañado en {Coleccion}; copia en {Copia}.", coleccion, copia);
            throw new AlmacenamientoException(
                $"El archivo de datos de '{coleccion}' está dañado y no se pudo leer. No se modificó; " +
                $"se guardó una copia en la carpeta de cuarentena. Restaure el respaldo '{Path.GetFileName(ruta)}{ExtensionRespaldo}' " +
                "o elimine el archivo para recuperar los datos de prueba iniciales.", ex);
        }
    }

    internal async Task EscribirSinBloqueoAsync<T>(string coleccion, List<T> registros, CancellationToken ct)
    {
        var ruta = RutaArchivo(coleccion);
        var temporal = ruta + ExtensionTemporal;
        try
        {
            await using (var flujo = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(flujo, registros, OpcionesJson.Predeterminadas, ct);
                await flujo.FlushAsync(ct);
            }

            await ReemplazarConReintentosAsync(temporal, ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryEliminar(temporal);
            throw new AlmacenamientoException($"No se pudieron guardar los datos de '{coleccion}'. Los datos anteriores se conservan.", ex);
        }
    }

    /// <summary>Reemplaza el archivo de forma atómica y conserva la versión anterior como respaldo.</summary>
    private static async Task ReemplazarConReintentosAsync(string temporal, string ruta)
    {
        const int intentos = 3;
        for (var intento = 1; ; intento++)
        {
            try
            {
                if (File.Exists(ruta))
                    File.Replace(temporal, ruta, ruta + ExtensionRespaldo, ignoreMetadataErrors: true);
                else
                    File.Move(temporal, ruta);
                return;
            }
            catch (IOException) when (intento < intentos)
            {
                // Un antivirus o un indexador pueden retener el archivo un instante.
                await Task.Delay(50 * intento);
            }
        }
    }

    /// <summary>
    /// Completa las transacciones cuyo diario quedó preparado pero no se terminó de aplicar (por ejemplo, si el proceso
    /// se detuvo mientras se guardaban los archivos). El diario tiene el contenido final de cada colección.
    /// </summary>
    private void RecuperarTransacciones()
    {
        if (!Directory.Exists(DirectorioTransacciones))
            return;
        foreach (var diario in Directory.GetFiles(DirectorioTransacciones, "*.json").Order(StringComparer.Ordinal))
        {
            JsonObject colecciones;
            try
            {
                colecciones = JsonNode.Parse(File.ReadAllText(diario))?["colecciones"]?.AsObject()
                    ?? throw new JsonException("El diario no tiene colecciones.");
            }
            catch (JsonException ex)
            {
                // El diario se escribe completo o no se escribe; uno ilegible no se aplica.
                _logger.LogError(ex, "Diario de transacción ilegible: {Diario}. No se aplicó.", diario);
                File.Move(diario, diario + ".ilegible", overwrite: true);
                continue;
            }

            foreach (var (coleccion, registros) in colecciones)
            {
                var ruta = RutaArchivo(coleccion);
                var temporal = ruta + ExtensionTemporal;
                File.WriteAllText(temporal, registros?.ToJsonString(OpcionesJson.Predeterminadas) ?? "[]");
                ReemplazarConReintentosAsync(temporal, ruta).GetAwaiter().GetResult();
            }
            File.Delete(diario);
            _logger.LogWarning("Se completó la transacción pendiente {Diario}.", Path.GetFileName(diario));
        }
    }

    private bool CopiarSemilla(string coleccion, string ruta)
    {
        if (_directorioSemilla is null)
            return false;
        var semilla = Path.Combine(_directorioSemilla, ColeccionesJson.PorNombreColeccion(coleccion).Archivo);
        if (!File.Exists(semilla))
            return false;
        File.Copy(semilla, ruta, overwrite: false);
        _logger.LogInformation("Se copiaron los datos de prueba iniciales de {Coleccion}.", coleccion);
        return true;
    }

    private string PonerEnCuarentena(string ruta, string coleccion)
    {
        // Cada lectura de un archivo dañado falla igual; solo se copia una vez por cada contenido distinto.
        var modificado = File.GetLastWriteTimeUtc(ruta);
        if (_cuarentena.TryGetValue(ruta, out var copiaPrevia) && copiaPrevia.Modificado == modificado)
            return copiaPrevia.Destino;

        var carpeta = Path.Combine(_directorio, "cuarentena");
        Directory.CreateDirectory(carpeta);
        var destino = Path.Combine(carpeta, $"{coleccion}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
        File.Copy(ruta, destino, overwrite: false);
        _cuarentena[ruta] = (modificado, destino);
        return destino;
    }

    private static void TryEliminar(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
        catch (IOException)
        {
            // El temporal huérfano se sobrescribe en la siguiente escritura.
        }
    }
}
