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

/// <summary>
/// Archivos de logo: carga validada, lectura y borrado cuando ya nadie los usa. Un mismo archivo puede estar en la
/// plantilla anterior, en versiones publicadas, en propuestas y en lo enviado a verificación (por ejemplo, la
/// Certificación comparte el logo de su Resolución), así que solo se borra si ninguno lo usa.
/// </summary>
public sealed class ServicioLogos(
    IRepositorio<ArchivoRecurso> archivos,
    IAlmacenArchivos almacen,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<Propuesta> propuestas,
    IRepositorio<Presentacion> presentaciones,
    TimeProvider reloj)
{
    /// <summary>Tamaño máximo de un logo.</summary>
    public const int TamanoMaximo = 1024 * 1024;

    /// <summary>
    /// Guarda el archivo del logo. El tipo se comprueba por el contenido del archivo (firma PNG o JPEG), no por su
    /// nombre ni por lo que declara el navegador.
    /// </summary>
    public async Task<ArchivoRecurso> GuardarAsync(string nombreOriginal, byte[] contenido, CancellationToken ct = default)
    {
        var resultado = new ResultadoValidacion();
        var tipo = TipoImagen(contenido);
        if (contenido.Length == 0)
            resultado.Agregar("archivo", "El archivo está vacío.");
        else if (contenido.Length > TamanoMaximo)
            resultado.Agregar("archivo", "El logo no puede superar 1 MB.");
        else if (tipo is null)
            resultado.Agregar("archivo", "El logo debe ser una imagen PNG o JPEG.");
        resultado.LanzarSiHayErrores();

        var nombre = Path.GetFileName(nombreOriginal);
        var recurso = await archivos.CrearAsync(new ArchivoRecurso
        {
            NombreOriginal = nombre.Length > 150 ? nombre[..150] : nombre,
            TipoContenido = tipo!,
            Tamano = contenido.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(contenido)),
            FechaCarga = reloj.GetUtcNow(),
        }, ct);
        await almacen.GuardarAsync(recurso.Id, contenido, ct);
        return recurso;
    }

    /// <summary>Borra el archivo si ninguna plantilla, versión, propuesta ni presentación lo usa.</summary>
    public async Task EliminarSiNoSeUsaAsync(string? archivoId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(archivoId))
            return;
        var enUso =
            (await plantillas.ListarAsync(ct)).Any(p => p.LogoId == archivoId) ||
            (await versiones.ListarAsync(ct)).Any(v => v.Contenido.Diseno.LogoId == archivoId) ||
            (await propuestas.ListarAsync(ct)).Any(p => p.ContenidoPropuesto.Diseno.LogoId == archivoId) ||
            (await presentaciones.ListarAsync(ct)).Any(p => p.Propuestas.Any(e => e.ContenidoPropuesto.Diseno.LogoId == archivoId));
        if (enUso)
            return;
        if (await archivos.ObtenerAsync(archivoId, ct) is not null)
            await archivos.EliminarAsync(archivoId, null, ct);
        await almacen.EliminarAsync(archivoId, ct);
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
}
