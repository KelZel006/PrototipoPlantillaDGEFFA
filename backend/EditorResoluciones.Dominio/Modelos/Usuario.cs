using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum Rol
{
    Analista,
    OficialJuridico,
    Administrador
}

/// <summary>
/// Usuario de prueba con sus roles. Solo sirve para simular el flujo en el prototipo: no hay contraseñas ni
/// autenticación. En producción, los usuarios y roles vendrán del proveedor de identidad institucional.
/// </summary>
public class Usuario : IEntidad
{
    /// <summary>Identificador de usuario, por ejemplo <c>a.perez</c>.</summary>
    public string Id { get; set; } = "";

    public int Version { get; set; }
    public string NombreCompleto { get; set; } = "";
    public List<Rol> Roles { get; set; } = [];
    public bool Activo { get; set; } = true;

    public bool Tiene(Rol rol) => Roles.Contains(rol);
}
