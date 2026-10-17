using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Seguridad;

/// <summary>
/// Comprueba en el servidor quién ejecuta la operación y qué roles tiene. El frontend oculta las acciones, pero la
/// decisión siempre se toma aquí.
/// </summary>
public sealed class ServicioAutorizacion(IUsuarioActual actual, IRepositorio<Usuario> usuarios)
{
    private Usuario? _usuario;

    /// <summary>Usuario de la operación. Si no está registrado o está inactivo, se rechaza (401).</summary>
    public async Task<Usuario> UsuarioAsync(CancellationToken ct = default)
    {
        if (_usuario is not null)
            return _usuario;
        var usuario = await usuarios.ObtenerAsync(actual.Nombre, ct);
        if (usuario is not { Activo: true })
            throw new UsuarioNoReconocidoException(actual.Nombre);
        return _usuario = usuario;
    }

    /// <summary>Usuario de la operación, si tiene el rol indicado; si no, se rechaza (403).</summary>
    public async Task<Usuario> ExigirAsync(Rol rol, CancellationToken ct = default)
    {
        var usuario = await UsuarioAsync(ct);
        if (!usuario.Tiene(rol))
            throw new AccesoDenegadoException($"Esta acción requiere el rol {NombreRol(rol)}.");
        return usuario;
    }

    public static string NombreRol(Rol rol) => rol switch
    {
        Rol.Analista => "Analista",
        Rol.OficialJuridico => "Oficial Jurídico",
        Rol.Administrador => "Administrador",
        _ => rol.ToString(),
    };
}
