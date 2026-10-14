namespace EditorResoluciones.Dominio.Comun;

/// <summary>
/// Registro persistible con identificador estable y número de versión para el control de concurrencia optimista.
/// </summary>
public interface IEntidad
{
    string Id { get; set; }

    /// <summary>Se incrementa en cada actualización; una escritura con versión desactualizada se rechaza.</summary>
    int Version { get; set; }
}

/// <summary>Registro que guarda la fecha de su última modificación (la asigna el servidor).</summary>
public interface IConMarcaTiempo
{
    DateTimeOffset FechaModificacion { get; set; }
}

/// <summary>Usuario que ejecuta la operación. En esta etapa es un usuario de prueba configurable.</summary>
public interface IUsuarioActual
{
    string Nombre { get; }
}
