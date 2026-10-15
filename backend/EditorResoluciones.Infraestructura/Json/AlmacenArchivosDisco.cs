using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Infraestructura.Json;

/// <summary>
/// Guarda el contenido de los archivos en la carpeta <c>archivos/</c> de los datos. El nombre en disco es el
/// identificador generado por el servidor; nunca el nombre que envía el usuario.
/// </summary>
public sealed partial class AlmacenArchivosDisco(AlmacenJson almacen) : IAlmacenArchivos
{
    [GeneratedRegex("^[A-Z]{3}-[A-Z0-9]{1,32}$")]
    private static partial Regex IdentificadorValido();

    private string Carpeta => Directory.CreateDirectory(Path.Combine(almacen.Directorio, "archivos")).FullName;

    public async Task GuardarAsync(string id, byte[] contenido, CancellationToken ct = default)
    {
        var ruta = Ruta(id);
        var temporal = ruta + ".tmp";
        await File.WriteAllBytesAsync(temporal, contenido, ct);
        File.Move(temporal, ruta, overwrite: true);
    }

    public async Task<byte[]?> LeerAsync(string id, CancellationToken ct = default)
    {
        var ruta = Ruta(id);
        return File.Exists(ruta) ? await File.ReadAllBytesAsync(ruta, ct) : null;
    }

    public Task EliminarAsync(string id, CancellationToken ct = default)
    {
        var ruta = Ruta(id);
        if (File.Exists(ruta))
            File.Delete(ruta);
        return Task.CompletedTask;
    }

    private string Ruta(string id)
    {
        if (!IdentificadorValido().IsMatch(id))
            throw new AlmacenamientoException("El identificador del archivo no es válido.");
        var carpeta = Carpeta;
        var ruta = Path.GetFullPath(Path.Combine(carpeta, id + ".bin"));
        if (!ruta.StartsWith(carpeta + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new AlmacenamientoException("La ruta del archivo no es válida.");
        return ruta;
    }
}
