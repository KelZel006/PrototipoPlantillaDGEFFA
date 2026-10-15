using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>Párrafo incluido en el documento, con los campos ya sustituidos por sus valores.</summary>
public sealed record ParrafoDocumento(string Id, int Posicion, NodoContenido Contenido);

/// <summary>Párrafo que no se incluyó y el motivo, para el resumen de la generación.</summary>
public sealed record ParrafoOmitido(string Id, int Posicion, string Resumen, string Motivo);

public sealed record SeccionDocumento(
    Seccion Seccion,
    IReadOnlyList<ParrafoDocumento> Parrafos,
    IReadOnlyList<ParrafoOmitido> Omitidos);

/// <summary>
/// Documento listo para presentar: secciones en el orden Cabecera, Considerando y Resuelve,
/// solo con los párrafos que corresponde mostrar.
/// </summary>
public sealed record DocumentoResuelto(
    Plantilla Plantilla,
    Solicitud? Solicitud,
    DateTimeOffset Fecha,
    IReadOnlyList<SeccionDocumento> Secciones,
    IReadOnlyList<string> Advertencias,
    DisenoDocumento Diseno);

/// <summary>Presentación del documento: logo del encabezado, texto del pie de página e interlineado.</summary>
public sealed record DisenoDocumento(byte[]? Logo, AlineacionLogo AlineacionLogo, string? PiePagina, double Interlineado);

/// <summary>Marcas internas del documento resuelto (no se guardan; solo indican cómo presentar un campo).</summary>
public static class MarcasDocumento
{
    /// <summary>Campo sustituido por su valor.</summary>
    public const string CampoResuelto = "campoResuelto";

    /// <summary>Campo sin dato en la solicitud de prueba.</summary>
    public const string CampoSinDato = "campoSinDato";

    /// <summary>Campo mostrado como "{Etiqueta}" porque no se eligió solicitud.</summary>
    public const string CampoPlantilla = "campoPlantilla";
}

/// <summary>Convierte el documento resuelto en un archivo PDF.</summary>
public interface IGeneradorPdf
{
    byte[] Generar(DocumentoResuelto documento);
}
