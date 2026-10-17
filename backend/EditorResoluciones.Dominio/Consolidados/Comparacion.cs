using System.Globalization;
using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Consolidados;

public enum TipoSegmento
{
    Igual,
    Agregado,
    Eliminado,

    /// <summary>Texto reemplazado: en el original es lo que sale; en la propuesta, lo que entra en su lugar.</summary>
    Sustituido
}

/// <summary>Fragmento de texto con su tipo de cambio.</summary>
public sealed record Segmento(TipoSegmento Tipo, string Texto);

public enum EstadoBloque
{
    SinCambios,
    Modificado,
    Nuevo,
    Eliminado
}

public enum EstadoParrafo
{
    SinCambios,
    Agregado,
    Eliminado,
    Modificado
}

/// <summary>
/// Párrafo en la comparación. <c>Original</c> y <c>Propuesto</c> son el texto con sus diferencias (null si el párrafo
/// no existe en ese lado). <c>Movido</c> indica que cambió de orden relativo.
/// </summary>
public sealed record ParrafoComparado(
    string ParrafoId,
    EstadoParrafo Estado,
    bool Movido,
    int? PosicionOriginal,
    int? PosicionPropuesta,
    IReadOnlyList<Segmento>? Original,
    IReadOnlyList<Segmento>? Propuesto,
    bool SoloFormato,
    IReadOnlyList<string> Configuracion,
    IReadOnlyList<string> Ajustes);

public sealed record BloqueComparado(string Codigo, string Nombre, EstadoBloque Estado, IReadOnlyList<ParrafoComparado> Parrafos);

/// <summary>Dato de diseño que cambió (logo, alineación, pie de página o espacio entre firmas).</summary>
public sealed record CampoDiseno(string Campo, string? Original, string? Propuesto);

/// <summary>Comparación de la propuesta con su versión original, bloque por bloque y párrafo por párrafo.</summary>
public sealed record ComparacionPropuesta(
    string PropuestaId,
    OperacionPropuesta Operacion,
    IReadOnlyList<BloqueComparado> Bloques,
    IReadOnlyList<CampoDiseno> Diseno,
    IReadOnlyList<Ajuste> Ajustes);

public static partial class ComparadorContenido
{
    /// <summary>
    /// Arma la comparación lado a lado. Los bloques se emparejan por código y los párrafos por identificador; el texto
    /// se compara palabra por palabra. Los párrafos eliminados aparecen en el lugar que ocupaban en el original.
    /// </summary>
    public static ComparacionPropuesta Comparar(
        string propuestaId,
        OperacionPropuesta operacion,
        ContenidoPlantilla original,
        ContenidoPlantilla propuesto,
        IReadOnlyList<BloqueConfigurado> configurados,
        IReadOnlyDictionary<string, Campo> catalogo)
    {
        var ajustes = Ajustes(original, propuesto);
        var codigos = configurados.Select(c => c.Codigo)
            .Concat(original.Bloques.Select(b => b.Codigo))
            .Concat(propuesto.Bloques.Select(b => b.Codigo))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var bloques = new List<BloqueComparado>();
        foreach (var codigo in codigos)
        {
            var antes = original.Bloques.FirstOrDefault(b => b.Codigo == codigo) is { Incluido: true } a ? a : null;
            var despues = propuesto.Bloques.FirstOrDefault(b => b.Codigo == codigo) is { Incluido: true } d ? d : null;
            if ((antes?.Parrafos.Count ?? 0) == 0 && (despues?.Parrafos.Count ?? 0) == 0)
                continue;

            var nombre = configurados.FirstOrDefault(c => c.Codigo == codigo)?.Nombre ?? CodigosBloque.Nombre(codigo);
            var parrafos = CompararParrafos(antes?.Parrafos ?? [], despues?.Parrafos ?? [], codigo, ajustes, catalogo);
            var estado = antes is null || antes.Parrafos.Count == 0 ? EstadoBloque.Nuevo
                : despues is null || despues.Parrafos.Count == 0 ? EstadoBloque.Eliminado
                : parrafos.Any(p => p.Estado != EstadoParrafo.SinCambios || p.Movido) ? EstadoBloque.Modificado
                : EstadoBloque.SinCambios;
            bloques.Add(new BloqueComparado(codigo, nombre, estado, parrafos));
        }

        return new ComparacionPropuesta(propuestaId, operacion, bloques, CompararDiseno(original.Diseno, propuesto.Diseno), ajustes);
    }

    private static List<ParrafoComparado> CompararParrafos(
        List<ParrafoContenido> antes,
        List<ParrafoContenido> despues,
        string codigo,
        IReadOnlyList<Ajuste> ajustes,
        IReadOnlyDictionary<string, Campo> catalogo)
    {
        var viejos = antes.OrderBy(p => p.Orden).ToList();
        var nuevos = despues.OrderBy(p => p.Orden).ToList();
        var posVieja = viejos.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i + 1, StringComparer.Ordinal);
        var posNueva = nuevos.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i + 1, StringComparer.Ordinal);
        var movidos = ajustes.Where(a => a.Tipo == TipoAjuste.ParrafoMovido && a.BloqueCodigo == codigo)
            .Select(a => a.ParrafoId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var porIdViejo = viejos.ToDictionary(p => p.Id, StringComparer.Ordinal);

        // Orden de la propuesta; cada eliminado va después del párrafo que lo precedía en el original.
        var secuencia = nuevos.Select(p => p.Id).ToList();
        foreach (var eliminado in viejos.Where(p => !posNueva.ContainsKey(p.Id)))
        {
            var indice = 0;
            for (var i = viejos.IndexOf(eliminado) - 1; i >= 0; i--)
            {
                var previo = secuencia.IndexOf(viejos[i].Id);
                if (previo >= 0)
                {
                    indice = previo + 1;
                    break;
                }
            }
            secuencia.Insert(indice, eliminado.Id);
        }

        var resultado = new List<ParrafoComparado>();
        foreach (var id in secuencia)
        {
            porIdViejo.TryGetValue(id, out var viejo);
            var nuevo = nuevos.FirstOrDefault(p => p.Id == id);
            var etiquetas = ajustes.Where(a => a.BloqueCodigo == codigo && a.ParrafoId == id).Select(a => a.Etiqueta).ToList();
            var textoViejo = viejo is null ? null : ContenidoEditor.TextoPlano(viejo.Contenido, catalogo, conTablas: true);
            var textoNuevo = nuevo is null ? null : ContenidoEditor.TextoPlano(nuevo.Contenido, catalogo, conTablas: true);

            if (viejo is null)
            {
                resultado.Add(new ParrafoComparado(id, EstadoParrafo.Agregado, false, null, posNueva[id], null,
                    [new Segmento(TipoSegmento.Agregado, textoNuevo!)], false, [], etiquetas));
                continue;
            }
            if (nuevo is null)
            {
                resultado.Add(new ParrafoComparado(id, EstadoParrafo.Eliminado, false, posVieja[id], null,
                    [new Segmento(TipoSegmento.Eliminado, textoViejo!)], null, false, [], etiquetas));
                continue;
            }

            var cambioContenido = !MismoJson(viejo.Contenido, nuevo.Contenido);
            var (segViejo, segNuevo) = cambioContenido && textoViejo != textoNuevo
                ? DiferenciaTexto(textoViejo!, textoNuevo!)
                : ([new Segmento(TipoSegmento.Igual, textoViejo!)], [new Segmento(TipoSegmento.Igual, textoNuevo!)]);
            var configuracion = new List<string>();
            if (viejo.Activo != nuevo.Activo)
                configuracion.Add(nuevo.Activo ? "Se activó" : "Se desactivó");
            if (!MismoJson(viejo.Condicion, nuevo.Condicion))
                configuracion.Add("Cambió la condición");
            if (viejo.MostrarSoloSiCumple != nuevo.MostrarSoloSiCumple)
                configuracion.Add("Cambió «mostrar solo si cumple»");

            var estado = cambioContenido || configuracion.Count > 0 ? EstadoParrafo.Modificado : EstadoParrafo.SinCambios;
            resultado.Add(new ParrafoComparado(id, estado, movidos.Contains(id), posVieja[id], posNueva[id], segViejo, segNuevo,
                cambioContenido && textoViejo == textoNuevo, configuracion, etiquetas));
        }
        return resultado;
    }

    private static List<CampoDiseno> CompararDiseno(DisenoPlantilla o, DisenoPlantilla p)
    {
        var campos = new List<CampoDiseno>();
        if (o.LogoId != p.LogoId)
            campos.Add(new CampoDiseno("Logo", o.LogoId is null ? "Sin logo" : "Con logo", p.LogoId is null ? "Sin logo" : "Logo nuevo"));
        if (o.AlineacionLogo != p.AlineacionLogo)
            campos.Add(new CampoDiseno("Alineación del logo", o.AlineacionLogo.ToString(), p.AlineacionLogo.ToString()));
        if (o.PiePagina != p.PiePagina)
            campos.Add(new CampoDiseno("Pie de página", o.PiePagina, p.PiePagina));
        if (!o.EspacioFirmasCm.Equals(p.EspacioFirmasCm))
            campos.Add(new CampoDiseno("Espacio entre firmas",
                o.EspacioFirmasCm.ToString("0.0", CultureInfo.InvariantCulture) + " cm",
                p.EspacioFirmasCm.ToString("0.0", CultureInfo.InvariantCulture) + " cm"));
        return campos;
    }

    /// <summary>Máximo de comparaciones palabra por palabra (párrafos de hasta 2000 caracteres caben de sobra).</summary>
    private const int LimiteCeldas = 400_000;

    /// <summary>
    /// Diferencia palabra por palabra (subsecuencia común más larga sobre palabras y espacios). Un tramo con texto que
    /// sale y texto que entra entre dos partes iguales se marca como sustituido en ambos lados.
    /// </summary>
    public static (IReadOnlyList<Segmento> Original, IReadOnlyList<Segmento> Propuesto) DiferenciaTexto(string original, string propuesto)
    {
        var a = Palabras().Matches(original).Select(m => m.Value).ToArray();
        var b = Palabras().Matches(propuesto).Select(m => m.Value).ToArray();
        if ((long)(a.Length + 1) * (b.Length + 1) > LimiteCeldas)
            return ([new Segmento(TipoSegmento.Sustituido, original)], [new Segmento(TipoSegmento.Sustituido, propuesto)]);

        var largo = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        for (var j = b.Length - 1; j >= 0; j--)
            largo[i, j] = a[i] == b[j] ? largo[i + 1, j + 1] + 1 : Math.Max(largo[i + 1, j], largo[i, j + 1]);

        // Operaciones: 0 igual, -1 sale (original), +1 entra (propuesta).
        var operaciones = new List<(int Tipo, string Texto)>();
        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                operaciones.Add((0, a[x]));
                x++;
                y++;
            }
            else if (largo[x + 1, y] >= largo[x, y + 1])
                operaciones.Add((-1, a[x++]));
            else
                operaciones.Add((1, b[y++]));
        }
        while (x < a.Length)
            operaciones.Add((-1, a[x++]));
        while (y < b.Length)
            operaciones.Add((1, b[y++]));

        var izquierda = new List<Segmento>();
        var derecha = new List<Segmento>();
        for (var i = 0; i < operaciones.Count;)
        {
            if (operaciones[i].Tipo == 0)
            {
                Agregar(izquierda, TipoSegmento.Igual, operaciones[i].Texto);
                Agregar(derecha, TipoSegmento.Igual, operaciones[i].Texto);
                i++;
                continue;
            }
            // Tramo de cambios hasta la siguiente parte igual.
            var fin = i;
            while (fin < operaciones.Count && operaciones[fin].Tipo != 0)
                fin++;
            var tramo = operaciones.GetRange(i, fin - i);
            var sustitucion = tramo.Any(o => o.Tipo < 0) && tramo.Any(o => o.Tipo > 0);
            foreach (var (tipo, texto) in tramo)
            {
                if (tipo < 0)
                    Agregar(izquierda, sustitucion ? TipoSegmento.Sustituido : TipoSegmento.Eliminado, texto);
                else
                    Agregar(derecha, sustitucion ? TipoSegmento.Sustituido : TipoSegmento.Agregado, texto);
            }
            i = fin;
        }
        return (izquierda, derecha);
    }

    private static void Agregar(List<Segmento> segmentos, TipoSegmento tipo, string texto)
    {
        if (segmentos.Count > 0 && segmentos[^1].Tipo == tipo)
            segmentos[^1] = segmentos[^1] with { Texto = segmentos[^1].Texto + texto };
        else
            segmentos.Add(new Segmento(tipo, texto));
    }

    /// <summary>Unidades de comparación: espacios, palabras (letras y números) y cada signo de puntuación por separado.</summary>
    [GeneratedRegex(@"\s+|[\p{L}\p{N}]+|[^\s\p{L}\p{N}]")]
    private static partial Regex Palabras();
}
