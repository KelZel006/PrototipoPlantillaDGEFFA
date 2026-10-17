using EditorResoluciones.Dominio.Comun;

namespace EditorResoluciones.Dominio.Modelos;

/// <summary>Vigencia de una versión publicada. No confundir con el estado del Documento Consolidado.</summary>
public enum Vigencia
{
    Vigente,
    NoVigente
}

/// <summary>
/// Versión publicada de una plantilla. Es inmutable: solo cambian su vigencia y su fin de vigencia.
/// Nunca se elimina. Para cada plantilla hay como máximo una versión vigente.
/// </summary>
public class VersionPlantilla : IEntidad
{
    public string Id { get; set; } = "";
    public int Version { get; set; }

    public string PlantillaId { get; set; } = "";

    /// <summary>1, 2, 3… dentro de la plantilla.</summary>
    public int NumeroVersion { get; set; }

    public Vigencia Vigencia { get; set; }

    /// <summary>Diseño, bloques y párrafos tal como quedaron publicados.</summary>
    public ContenidoPlantilla Contenido { get; set; } = new();

    /// <summary>SHA-256 del contenido (<see cref="Consolidados.HuellaContenido"/>). Permite detectar cambios.</summary>
    public string Huella { get; set; } = "";

    /// <summary>Versión que esta reemplazó; null en la primera.</summary>
    public string? VersionAnteriorId { get; set; }

    /// <summary>Documento Consolidado que la publicó; null en la carga inicial.</summary>
    public string? ConsolidadoId { get; set; }

    /// <summary>Verdadero si proviene de los datos que existían antes del Documento Consolidado (decisión D5).</summary>
    public bool CargaInicial { get; set; }

    public string PublicadaPor { get; set; } = "";
    public DateTimeOffset FechaPublicacion { get; set; }
    public DateTimeOffset InicioVigencia { get; set; }

    /// <summary>Momento en que pasó a no vigente; null mientras está vigente.</summary>
    public DateTimeOffset? FinVigencia { get; set; }
}

/// <summary>
/// Contenido completo de una plantilla: diseño y bloques con sus párrafos. Tienen esta misma forma la versión vigente,
/// la propuesta y lo enviado a verificación, para compararlas, generar el PDF y publicarlas con el mismo código.
/// </summary>
public class ContenidoPlantilla
{
    public DisenoPlantilla Diseno { get; set; } = new();
    public List<BloqueContenido> Bloques { get; set; } = [];
}

/// <summary>Datos de diseño del documento. En la comparación se tratan como el bloque <c>DISENO</c>.</summary>
public class DisenoPlantilla
{
    public string? LogoId { get; set; }
    public AlineacionLogo AlineacionLogo { get; set; } = AlineacionLogo.Centro;
    public string? PiePagina { get; set; }
    public double EspacioFirmasCm { get; set; } = 1.2;
}

/// <summary>Bloque del documento (hoy, una de las cinco secciones) identificado por un código estable.</summary>
public class BloqueContenido
{
    /// <summary>Código estable del bloque (<see cref="CodigosBloque"/>); la comparación empareja por este código.</summary>
    public string Codigo { get; set; } = "";

    /// <summary>Falso si el bloque se excluyó del documento.</summary>
    public bool Incluido { get; set; } = true;

    public List<ParrafoContenido> Parrafos { get; set; } = [];
}

/// <summary>Párrafo dentro de un bloque. Conserva el identificador del párrafo de origen para comparar sin depender de la posición.</summary>
public class ParrafoContenido
{
    public string Id { get; set; } = "";

    /// <summary>
    /// Versión del párrafo dentro de la propuesta: cada cambio la incrementa y una edición con versión desactualizada se
    /// rechaza (por ejemplo, desde otra pestaña). No forma parte de la comparación.
    /// </summary>
    public int Version { get; set; } = 1;

    public int Orden { get; set; }
    public NodoContenido Contenido { get; set; } = new();
    public bool Activo { get; set; } = true;
    public bool MostrarSoloSiCumple { get; set; }
    public GrupoCondiciones? Condicion { get; set; }
    public List<string> CamposUsados { get; set; } = [];

    /// <summary>En una Certificación, el párrafo de la Resolución del que se copió (decisión D3).</summary>
    public string? OrigenParrafoId { get; set; }
}

/// <summary>
/// Códigos de los bloques. En esta etapa son las cinco secciones actuales (decisión D2); el catálogo configurable por
/// tipo documental llega en la Fase 4.
/// </summary>
public static class CodigosBloque
{
    public const string Cabecera = "CABECERA";
    public const string Considerando = "CONSIDERANDO";
    public const string PorTanto = "POR_TANTO";
    public const string Resuelve = "RESUELVE";
    public const string Firmas = "FIRMAS";

    /// <summary>Pseudobloque con el logo, el pie de página y el espacio entre firmas.</summary>
    public const string Diseno = "DISENO";

    public static string De(Seccion seccion) => seccion switch
    {
        Seccion.Cabecera => Cabecera,
        Seccion.Considerando => Considerando,
        Seccion.PorTanto => PorTanto,
        Seccion.Resuelve => Resuelve,
        Seccion.Firmas => Firmas,
        _ => throw new ArgumentOutOfRangeException(nameof(seccion)),
    };

    /// <summary>Sección del editor y del PDF que corresponde al bloque; null si el código no existe.</summary>
    public static Seccion? ASeccion(string codigo) => codigo switch
    {
        Cabecera => Seccion.Cabecera,
        Considerando => Seccion.Considerando,
        PorTanto => Seccion.PorTanto,
        Resuelve => Seccion.Resuelve,
        Firmas => Seccion.Firmas,
        _ => null,
    };

    /// <summary>Nombre para mostrar en los ajustes: «POR TANTO», «FUNDAMENTACION LEGAL», «CONSIDERANDO»…</summary>
    public static string Nombre(string codigo) => codigo switch
    {
        Diseno => "DISEÑO",
        _ => codigo.Replace('_', ' '),
    };
}
