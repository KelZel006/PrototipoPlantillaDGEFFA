using System.Text.Json;
using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

public enum AccionHistorial
{
    Creacion,
    Actualizacion,
    Eliminacion
}

/// <summary>Cambio registrado sobre una colección. Se agrega y no se modifica.</summary>
public class RegistroHistorial : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public string Usuario { get; set; } = "";
    public string Coleccion { get; set; } = "";
    public string EntidadId { get; set; } = "";
    public AccionHistorial Accion { get; set; }
    public int VersionEntidad { get; set; }
    public JsonElement? Anterior { get; set; }
    public JsonElement? Nuevo { get; set; }
}
