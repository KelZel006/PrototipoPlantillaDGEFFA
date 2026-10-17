namespace EditorResoluciones.Dominio.Comun;

/// <summary>El registro solicitado no existe.</summary>
public sealed class EntidadNoEncontradaException(string coleccion, string id)
    : Exception($"No existe el registro '{id}' en {coleccion}.")
{
    public string Coleccion { get; } = coleccion;
    public string EntidadId { get; } = id;
}

/// <summary>La operación choca con el estado actual de los datos (versión desactualizada o identificador repetido).</summary>
public sealed class ConflictoException(string mensaje) : Exception(mensaje);

/// <summary>Los datos recibidos no cumplen las reglas de validación del servidor.</summary>
public sealed class ValidacionException(IReadOnlyDictionary<string, string[]> errores)
    : Exception("Los datos enviados no son válidos.")
{
    public IReadOnlyDictionary<string, string[]> Errores { get; } = errores;
}

/// <summary>El almacenamiento no pudo leer o escribir los datos (por ejemplo, un archivo JSON dañado).</summary>
public sealed class AlmacenamientoException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);

/// <summary>El usuario no tiene el rol o el permiso que exige la operación (403).</summary>
public sealed class AccesoDenegadoException(string mensaje) : Exception(mensaje);

/// <summary>El usuario de prueba indicado no existe o está inactivo (401).</summary>
public sealed class UsuarioNoReconocidoException(string usuario)
    : Exception($"El usuario '{usuario}' no está registrado o está inactivo.");
