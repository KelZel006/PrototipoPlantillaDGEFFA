using System.Security.Cryptography;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Servicios;

/// <summary>Contenido binario de los archivos cargados. En esta etapa se guardan en disco; luego puede ser otro almacén.</summary>
public interface IAlmacenArchivos
{
    Task GuardarAsync(string id, byte[] contenido, CancellationToken ct = default);

    /// <summary>Contenido del archivo, o null si no existe.</summary>
    Task<byte[]?> LeerAsync(string id, CancellationToken ct = default);

    Task EliminarAsync(string id, CancellationToken ct = default);
}

/// <summary>Logo del encabezado de una plantilla: carga, reemplazo y eliminación.</summary>
public sealed class ServicioLogos(
    IRepositorio<ArchivoRecurso> archivos,
    IAlmacenArchivos almacen,
    ServicioEntidades<Plantilla> plantillas,
    TimeProvider reloj)
{
    /// <summary>Tamaño máximo de un logo.</summary>
    public const int TamanoMaximo = 1024 * 1024;

    /// <summary>
    /// Guarda el logo y lo asigna a la plantilla. El tipo se comprueba por el contenido del archivo
    /// (firma PNG o JPEG), no por su nombre ni por lo que declara el navegador.
    /// </summary>
    public async Task<Plantilla> AsignarAsync(string plantillaId, string nombreOriginal, byte[] contenido, CancellationToken ct = default)
    {
        var plantilla = await plantillas.ObtenerAsync(plantillaId, ct);

        var resultado = new ResultadoValidacion();
        var tipo = TipoImagen(contenido);
        if (contenido.Length == 0)
            resultado.Agregar("archivo", "El archivo está vacío.");
        else if (contenido.Length > TamanoMaximo)
            resultado.Agregar("archivo", "El logo no puede superar 1 MB.");
        else if (tipo is null)
            resultado.Agregar("archivo", "El logo debe ser una imagen PNG o JPEG.");
        resultado.LanzarSiHayErrores();

        var recurso = await archivos.CrearAsync(new ArchivoRecurso
        {
            NombreOriginal = Path.GetFileName(nombreOriginal).Length > 150 ? Path.GetFileName(nombreOriginal)[..150] : Path.GetFileName(nombreOriginal),
            TipoContenido = tipo!,
            Tamano = contenido.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(contenido)),
            FechaCarga = reloj.GetUtcNow(),
        }, ct);
        await almacen.GuardarAsync(recurso.Id, contenido, ct);

        var anterior = plantilla.LogoId;
        plantilla.LogoId = recurso.Id;
        var actualizada = await plantillas.ActualizarAsync(plantilla.Id, plantilla, ct);

        if (!string.IsNullOrEmpty(anterior))
            await EliminarArchivoAsync(anterior, ct);
        return actualizada;
    }

    public async Task<Plantilla> QuitarAsync(string plantillaId, CancellationToken ct = default)
    {
        var plantilla = await plantillas.ObtenerAsync(plantillaId, ct);
        var anterior = plantilla.LogoId;
        if (string.IsNullOrEmpty(anterior))
            return plantilla;

        plantilla.LogoId = null;
        var actualizada = await plantillas.ActualizarAsync(plantilla.Id, plantilla, ct);
        await EliminarArchivoAsync(anterior, ct);
        return actualizada;
    }

    public async Task<(ArchivoRecurso Datos, byte[] Contenido)> LeerAsync(string archivoId, CancellationToken ct = default)
    {
        var datos = await archivos.ObtenerAsync(archivoId, ct) ?? throw new EntidadNoEncontradaException(archivos.Coleccion, archivoId);
        var contenido = await almacen.LeerAsync(archivoId, ct) ?? throw new EntidadNoEncontradaException(archivos.Coleccion, archivoId);
        return (datos, contenido);
    }

    /// <summary>"image/png" o "image/jpeg" según la firma del archivo; null si no es ninguno.</summary>
    public static string? TipoImagen(byte[] contenido) => contenido switch
    {
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        _ => null,
    };

    private async Task EliminarArchivoAsync(string archivoId, CancellationToken ct)
    {
        if (await archivos.ObtenerAsync(archivoId, ct) is not null)
            await archivos.EliminarAsync(archivoId, null, ct);
        await almacen.EliminarAsync(archivoId, ct);
    }
}
