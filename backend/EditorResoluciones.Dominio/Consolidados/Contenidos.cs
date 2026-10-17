using System.Security.Cryptography;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Documento;
using EditorResoluciones.Dominio.Modelos;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Huella SHA-256 de un contenido o de un conjunto de propuestas, para detectar cualquier cambio.</summary>
public static class HuellaContenido
{
    private static readonly JsonSerializerOptions Compacto = new(OpcionesJson.Predeterminadas) { WriteIndented = false };

    public static string De(ContenidoPlantilla contenido) => Calcular(contenido);

    /// <summary>
    /// Huella de lo enviado a verificación: operación, identidad, versión original y contenido de cada propuesta, en
    /// orden de identificador. No incluye datos que cambian solos, como la versión de registro o las fechas.
    /// </summary>
    public static string De(IEnumerable<Propuesta> propuestas) => Calcular(propuestas
        .OrderBy(p => p.Id, StringComparer.Ordinal)
        .Select(p => new
        {
            p.Id, p.Operacion, p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId, p.Nombre,
            p.PlantillaId, p.VersionOrigenId, p.HuellaOrigen, p.ContenidoPropuesto, p.DerivadaDePropuestaId,
        }));

    private static string Calcular<T>(T valor) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(valor, Compacto)));
}

/// <summary>Arma el contenido de una plantilla a partir de los datos con los que trabaja hoy el editor.</summary>
public static class ConstructorContenido
{
    /// <summary>Diseño de la plantilla y sus párrafos agrupados por bloque, en el orden del documento.</summary>
    public static ContenidoPlantilla Desde(Plantilla plantilla, IEnumerable<Parrafo> parrafos)
    {
        var propios = parrafos.Where(p => p.PlantillaId == plantilla.Id).ToList();
        return new ContenidoPlantilla
        {
            Diseno = new DisenoPlantilla
            {
                LogoId = plantilla.LogoId,
                AlineacionLogo = plantilla.AlineacionLogo,
                PiePagina = plantilla.PiePagina,
                EspacioFirmasCm = plantilla.EspacioFirmasCm,
            },
            Bloques = ServicioDocumento.OrdenSecciones.Select(seccion => new BloqueContenido
            {
                Codigo = CodigosBloque.De(seccion),
                Parrafos = propios.Where(p => p.Seccion == seccion).OrderBy(p => p.Orden).Select(p => new ParrafoContenido
                {
                    Id = p.Id,
                    Orden = p.Orden,
                    Contenido = p.Contenido,
                    Activo = p.Activo,
                    MostrarSoloSiCumple = p.MostrarSoloSiCumple,
                    Condicion = p.Condicion,
                    CamposUsados = [.. p.CamposUsados],
                }).ToList(),
            }).ToList(),
        };
    }
}

/// <summary>Formato del número de Documento Consolidado: PLAN-AAAANNNN.</summary>
public static class NumeroPlan
{
    public const string Prefijo = "PLAN-";
    public const int SecuenciaMaxima = 9999;

    public static string Formatear(int anio, int secuencia)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anio, 1000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(anio, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(secuencia, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(secuencia, SecuenciaMaxima);
        return $"{Prefijo}{anio:D4}{secuencia:D4}";
    }
}

/// <summary>Zona horaria institucional: define el año del número PLAN (un registro del 31/12 a las 22:00 no toma el año siguiente).</summary>
public sealed record ZonaHorariaInstitucional(TimeZoneInfo Zona)
{
    public int Anio(DateTimeOffset momento) => TimeZoneInfo.ConvertTime(momento, Zona).Year;
}
