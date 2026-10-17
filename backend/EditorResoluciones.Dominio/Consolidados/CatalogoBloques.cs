using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>
/// Bloques que usa cada tipo documental (decisión D2), con los datos del catálogo. Un tipo sin bloques configurados usa
/// los cinco de siempre: Cabecera, Considerando, Por Tanto, Resuelve y Firmas.
/// </summary>
public sealed class CatalogoBloques(IRepositorio<BloqueDocumental> bloques, IRepositorio<TipoDocumento> tipos)
{
    /// <summary>Los cinco bloques originales, con el comportamiento que ya tenían el editor y el PDF.</summary>
    public static IReadOnlyList<BloqueConfigurado> Predeterminados { get; } =
    [
        new(CodigosBloque.Cabecera, "Cabecera", null, NumeracionBloque.Ninguna, false, "cabecera", false, 1),
        new(CodigosBloque.Considerando, "Considerando", null, NumeracionBloque.Considerando, false, "considerando", false, 2),
        new(CodigosBloque.PorTanto, "Por Tanto", "POR TANTO:", NumeracionBloque.Ninguna, false, "portanto", false, 3),
        new(CodigosBloque.Resuelve, "Resuelve", "RESUELVE:", NumeracionBloque.Ordinal, false, "resuelve", false, 4),
        new(CodigosBloque.Firmas, "Firmas", null, NumeracionBloque.Ninguna, true, "firmas", false, 5),
    ];

    public async Task<IReadOnlyList<BloqueConfigurado>> DeTipoAsync(string tipoDocumentoId, CancellationToken ct = default) =>
        Resolver(await tipos.ObtenerAsync(tipoDocumentoId, ct), await bloques.ListarAsync(ct));

    /// <summary>
    /// Bloques de una plantilla: los del tipo documental y, al final, los que tenga el contenido aunque el tipo ya no
    /// los use (para no ocultar texto existente).
    /// </summary>
    public async Task<IReadOnlyList<BloqueConfigurado>> DeContenidoAsync(string tipoDocumentoId, ContenidoPlantilla contenido, CancellationToken ct = default)
    {
        var catalogo = await bloques.ListarAsync(ct);
        var configurados = Resolver(await tipos.ObtenerAsync(tipoDocumentoId, ct), catalogo).ToList();
        foreach (var bloque in contenido.Bloques.Where(b => b.Parrafos.Count > 0 && configurados.All(c => c.Codigo != b.Codigo)))
            configurados.Add(DelCatalogo(bloque.Codigo, catalogo, false, configurados.Count + 1));
        return configurados;
    }

    public static IReadOnlyList<BloqueConfigurado> Resolver(TipoDocumento? tipo, IReadOnlyList<BloqueDocumental> catalogo)
    {
        if (tipo is not { Bloques.Count: > 0 })
            return Predeterminados;
        return tipo.Bloques
            .OrderBy(b => b.Orden)
            .Select((b, i) => DelCatalogo(b.BloqueId, catalogo, b.Obligatorio, i + 1))
            .ToList();
    }

    /// <summary>Datos de un bloque: del catálogo, de los predeterminados o, si no existe, un bloque de texto simple.</summary>
    public static BloqueConfigurado DelCatalogo(string codigo, IReadOnlyList<BloqueDocumental> catalogo, bool obligatorio, int orden)
    {
        if (catalogo.FirstOrDefault(b => b.Id == codigo) is { } bloque)
            return new(bloque.Id, bloque.Nombre, bloque.Titulo, bloque.Numeracion, bloque.EsFirmas, bloque.ContextoCampos, obligatorio, orden);
        if (Predeterminados.FirstOrDefault(b => b.Codigo == codigo) is { } predeterminado)
            return predeterminado with { Obligatorio = obligatorio, Orden = orden };
        return new(codigo, CodigosBloque.Nombre(codigo), null, NumeracionBloque.Ninguna, false, "cabecera", obligatorio, orden);
    }

    /// <summary>Bloques obligatorios sin ningún párrafo activo (se exigirán al enviar a verificación).</summary>
    public static IReadOnlyList<string> ObligatoriosVacios(IReadOnlyList<BloqueConfigurado> configurados, ContenidoPlantilla contenido) =>
        configurados
            .Where(c => c.Obligatorio)
            .Where(c => contenido.Bloques.FirstOrDefault(b => b.Codigo == c.Codigo) is not { Incluido: true } b || !b.Parrafos.Any(p => p.Activo))
            .Select(c => c.Nombre)
            .ToList();
}
