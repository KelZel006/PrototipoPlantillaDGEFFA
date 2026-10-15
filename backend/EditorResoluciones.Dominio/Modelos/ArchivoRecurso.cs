using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>Datos de un archivo cargado (por ejemplo, un logo). El contenido se guarda aparte, en el almacén de archivos.</summary>
public class ArchivoRecurso : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public string NombreOriginal { get; set; } = "";
    public string TipoContenido { get; set; } = "";
    public long Tamano { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset FechaCarga { get; set; }
}
