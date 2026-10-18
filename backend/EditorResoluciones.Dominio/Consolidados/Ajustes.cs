using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Consolidados;

public enum TipoAjuste
{
    ParrafoAgregado,
    ParrafoEliminado,
    ParrafoModificado,
    ParrafoMovido,
    ConfiguracionModificada,
    DisenoModificado,
    BloqueAgregado,
    BloqueEliminado
}

/// <summary>
/// Un ajuste de la propuesta respecto de la versión original (decisión D8): cada uno cuenta 1 y se muestra por separado
/// al Oficial Jurídico, con una etiqueta como «NUEVO CONSIDERANDO (posición 2)» o «CONSIDERANDO 3 modificado».
/// </summary>
public sealed record Ajuste(TipoAjuste Tipo, string BloqueCodigo, string? ParrafoId, string Etiqueta);

/// <summary>
/// Compara el contenido original con el propuesto a nivel de bloque y párrafo. Los bloques se emparejan por código y
/// los párrafos por su identificador estable, nunca por la posición. La diferencia de texto palabra por palabra y la
/// pantalla lado a lado llegan en la Fase 4.
/// </summary>
public static partial class ComparadorContenido
{
    public static IReadOnlyList<Ajuste> Ajustes(ContenidoPlantilla original, ContenidoPlantilla propuesto)
    {
        var ajustes = new List<Ajuste>();
        var codigos = original.Bloques.Select(b => b.Codigo)
            .Concat(propuesto.Bloques.Select(b => b.Codigo))
            .Distinct(StringComparer.Ordinal);

        foreach (var codigo in codigos)
        {
            var nombre = CodigosBloque.Nombre(codigo);
            var antes = original.Bloques.FirstOrDefault(b => b.Codigo == codigo) is { Incluido: true } a ? a : null;
            var despues = propuesto.Bloques.FirstOrDefault(b => b.Codigo == codigo) is { Incluido: true } d ? d : null;
            if (antes is null && despues is null)
                continue;
            if (despues is null)
            {
                ajustes.Add(new Ajuste(TipoAjuste.BloqueEliminado, codigo, null, $"BLOQUE ELIMINADO: {nombre}"));
                continue;
            }
            if (antes is null && original.Bloques.Count > 0 && despues.Parrafos.Count > 0)
                ajustes.Add(new Ajuste(TipoAjuste.BloqueAgregado, codigo, null, $"NUEVO BLOQUE: {nombre}"));

            var viejos = (antes?.Parrafos ?? []).OrderBy(p => p.Orden).ToList();
            var nuevos = despues.Parrafos.OrderBy(p => p.Orden).ToList();
            var posicionVieja = viejos.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i + 1, StringComparer.Ordinal);
            var posicionNueva = nuevos.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i + 1, StringComparer.Ordinal);

            foreach (var nuevo in nuevos.Where(p => !posicionVieja.ContainsKey(p.Id)))
                ajustes.Add(new Ajuste(TipoAjuste.ParrafoAgregado, codigo, nuevo.Id, $"NUEVO {nombre} (posición {posicionNueva[nuevo.Id]})"));
            foreach (var viejo in viejos.Where(p => !posicionNueva.ContainsKey(p.Id)))
                ajustes.Add(new Ajuste(TipoAjuste.ParrafoEliminado, codigo, viejo.Id, $"{nombre} {posicionVieja[viejo.Id]} eliminado"));

            var comunesViejo = viejos.Where(p => posicionNueva.ContainsKey(p.Id)).ToList();
            var comunesNuevo = nuevos.Where(p => posicionVieja.ContainsKey(p.Id)).ToList();
            var porId = comunesViejo.ToDictionary(p => p.Id, StringComparer.Ordinal);
            foreach (var nuevo in comunesNuevo)
            {
                var viejo = porId[nuevo.Id];
                var posicion = posicionNueva[nuevo.Id];
                if (!MismoJson(viejo.Contenido, nuevo.Contenido))
                    ajustes.Add(new Ajuste(TipoAjuste.ParrafoModificado, codigo, nuevo.Id, $"{nombre} {posicion} modificado"));

                var configuracion = new List<string>();
                if (viejo.Activo != nuevo.Activo)
                    configuracion.Add(nuevo.Activo ? "se activó" : "se desactivó");
                if (!MismoJson(viejo.Condicion, nuevo.Condicion))
                    configuracion.Add("cambió la condición");
                if (viejo.MostrarSoloSiCumple != nuevo.MostrarSoloSiCumple)
                    configuracion.Add("cambió «mostrar solo si cumple»");
                if (viejo.UnirConAnterior != nuevo.UnirConAnterior)
                    configuracion.Add(nuevo.UnirConAnterior ? "se unió con el párrafo anterior" : "se separó del párrafo anterior");
                if (configuracion.Count > 0)
                    ajustes.Add(new Ajuste(TipoAjuste.ConfiguracionModificada, codigo, nuevo.Id, $"{nombre} {posicion}: {string.Join(", ", configuracion)}"));
            }

            // Movidos: los comunes que quedan fuera de la subsecuencia común más larga cambiaron de orden relativo.
            var fijos = SubsecuenciaComun(comunesViejo.Select(p => p.Id).ToList(), comunesNuevo.Select(p => p.Id).ToList());
            foreach (var movido in comunesNuevo.Where(p => !fijos.Contains(p.Id)))
            {
                ajustes.Add(new Ajuste(TipoAjuste.ParrafoMovido, codigo, movido.Id,
                    $"{nombre} {posicionVieja[movido.Id]} movido a la posición {posicionNueva[movido.Id]}"));
            }
        }

        var (o, p) = (original.Diseno, propuesto.Diseno);
        if (o.LogoId != p.LogoId)
            ajustes.Add(new Ajuste(TipoAjuste.DisenoModificado, CodigosBloque.Diseno, null, p.LogoId is null ? "Logo quitado" : "Logo modificado"));
        if (o.AlineacionLogo != p.AlineacionLogo)
            ajustes.Add(new Ajuste(TipoAjuste.DisenoModificado, CodigosBloque.Diseno, null, "Alineación del logo modificada"));
        if (o.PiePagina != p.PiePagina)
            ajustes.Add(new Ajuste(TipoAjuste.DisenoModificado, CodigosBloque.Diseno, null, "Pie de página modificado"));
        if (!o.EspacioFirmasCm.Equals(p.EspacioFirmasCm))
            ajustes.Add(new Ajuste(TipoAjuste.DisenoModificado, CodigosBloque.Diseno, null, "Espacio entre firmas modificado"));

        return ajustes;
    }

    private static HashSet<string> SubsecuenciaComun(List<string> a, List<string> b)
    {
        var largo = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        for (var j = b.Count - 1; j >= 0; j--)
            largo[i, j] = a[i] == b[j] ? largo[i + 1, j + 1] + 1 : Math.Max(largo[i + 1, j], largo[i, j + 1]);

        var resultado = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0, j = 0; i < a.Count && j < b.Count;)
        {
            if (a[i] == b[j])
            {
                resultado.Add(a[i]);
                i++;
                j++;
            }
            else if (largo[i + 1, j] >= largo[i, j + 1])
                i++;
            else
                j++;
        }
        return resultado;
    }

    internal static bool MismoJson<T>(T a, T b) =>
        JsonSerializer.Serialize(a, OpcionesJson.Predeterminadas) == JsonSerializer.Serialize(b, OpcionesJson.Predeterminadas);
}

/// <summary>Cambio de un párrafo en la propuesta de origen (bloque y párrafo antes y después; null al crear o eliminar).</summary>
public sealed record CambioParrafo(string? BloqueAnterior, ParrafoContenido? Anterior, string? BloqueActual, ParrafoContenido? Actual);

/// <summary>
/// Lleva a la propuesta derivada (la Certificación) los cambios de la propuesta de origen (la Resolución), con la regla
/// que ya tenía la copia automática (decisión D3):
/// <list type="bullet">
/// <item>Solo se copia lo que cambió. Si ese mismo dato se había editado en la Certificación, gana la Resolución; lo
/// demás se conserva.</item>
/// <item>Un párrafo nuevo se agrega después de la copia del párrafo que lo precede; uno eliminado se elimina.</item>
/// <item>Los párrafos propios de la Certificación (sin <see cref="ParrafoContenido.OrigenParrafoId"/>) no se tocan.</item>
/// <item>En el texto, «resolución» se cambia por «certificación».</item>
/// </list>
/// </summary>
public static class FusionDerivada
{
    public static void Aplicar(
        ContenidoPlantilla destino,
        ContenidoPlantilla origenActual,
        IReadOnlyList<CambioParrafo> cambios,
        CambioPalabra? palabra,
        Func<string> nuevoId)
    {
        var antes = Instantanea(destino);
        var reubicar = new List<ParrafoContenido>();
        var bloques = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cambio in cambios)
        {
            var origenId = (cambio.Actual ?? cambio.Anterior)!.Id;
            var copias = destino.Bloques.SelectMany(b => b.Parrafos.Select(p => (Bloque: b, Parrafo: p)))
                .Where(x => x.Parrafo.OrigenParrafoId == origenId).ToList();

            if (cambio.Actual is null)
            {
                foreach (var (bloque, parrafo) in copias)
                {
                    bloque.Parrafos.Remove(parrafo);
                    bloques.Add(bloque.Codigo);
                }
                continue;
            }

            var actual = cambio.Actual;
            if (copias.Count == 0)
            {
                var nuevo = Clonar(actual);
                nuevo.Id = nuevoId();
                nuevo.Version = 1;
                nuevo.OrigenParrafoId = actual.Id;
                nuevo.Contenido = palabra?.Aplicar(nuevo.Contenido) ?? nuevo.Contenido;
                Bloque(destino, cambio.BloqueActual!).Parrafos.Add(nuevo);
                reubicar.Add(nuevo);
                bloques.Add(cambio.BloqueActual!);
                continue;
            }

            var anterior = cambio.Anterior;
            foreach (var (bloque, parrafo) in copias)
            {
                if (anterior is null || !ComparadorContenido.MismoJson(anterior.Contenido, actual.Contenido))
                {
                    parrafo.Contenido = palabra?.Aplicar(Clonar(actual.Contenido)) ?? Clonar(actual.Contenido);
                    parrafo.CamposUsados = [.. actual.CamposUsados];
                }
                if (anterior is null || anterior.Activo != actual.Activo)
                    parrafo.Activo = actual.Activo;
                if (anterior is null || anterior.MostrarSoloSiCumple != actual.MostrarSoloSiCumple)
                    parrafo.MostrarSoloSiCumple = actual.MostrarSoloSiCumple;
                if (anterior is null || anterior.UnirConAnterior != actual.UnirConAnterior)
                    parrafo.UnirConAnterior = actual.UnirConAnterior;
                if (anterior is null || !ComparadorContenido.MismoJson(anterior.Condicion, actual.Condicion))
                    parrafo.Condicion = actual.Condicion is null ? null : Clonar(actual.Condicion);
                if (anterior is null || cambio.BloqueAnterior != cambio.BloqueActual || anterior.Orden != actual.Orden)
                {
                    bloques.Add(bloque.Codigo);
                    if (bloque.Codigo != cambio.BloqueActual)
                    {
                        bloque.Parrafos.Remove(parrafo);
                        Bloque(destino, cambio.BloqueActual!).Parrafos.Add(parrafo);
                    }
                    bloques.Add(cambio.BloqueActual!);
                    reubicar.Add(parrafo);
                }
            }
        }

        foreach (var codigo in bloques)
            Ordenar(Bloque(destino, codigo), reubicar, Bloque(origenActual, codigo));

        // Cada párrafo que cambió sube su versión (una edición abierta en otra pestaña se rechazará).
        foreach (var parrafo in destino.Bloques.SelectMany(b => b.Parrafos))
        {
            if (antes.TryGetValue(parrafo.Id, out var huella) && huella != Huella(destino, parrafo))
                parrafo.Version++;
        }
    }

    /// <summary>Copia en el diseño de la derivada solo los datos de diseño que cambiaron en el origen.</summary>
    public static void AplicarDiseno(DisenoPlantilla destino, DisenoPlantilla anterior, DisenoPlantilla actual)
    {
        if (anterior.LogoId != actual.LogoId)
            destino.LogoId = actual.LogoId;
        if (anterior.AlineacionLogo != actual.AlineacionLogo)
            destino.AlineacionLogo = actual.AlineacionLogo;
        if (anterior.PiePagina != actual.PiePagina)
            destino.PiePagina = actual.PiePagina;
        if (!anterior.EspacioFirmasCm.Equals(actual.EspacioFirmasCm))
            destino.EspacioFirmasCm = actual.EspacioFirmasCm;
    }

    /// <summary>
    /// Contenido inicial de una Certificación nueva: una copia de la Resolución con identificadores nuevos, el vínculo
    /// con cada párrafo de origen y la palabra cambiada.
    /// </summary>
    public static ContenidoPlantilla CopiaInicial(ContenidoPlantilla origen, CambioPalabra? palabra, Func<string> nuevoId)
    {
        var copia = Clonar(origen);
        foreach (var parrafo in copia.Bloques.SelectMany(b => b.Parrafos))
        {
            parrafo.OrigenParrafoId = parrafo.Id;
            parrafo.Id = nuevoId();
            parrafo.Version = 1;
            parrafo.Contenido = palabra?.Aplicar(parrafo.Contenido) ?? parrafo.Contenido;
        }
        return copia;
    }

    /// <summary>
    /// Los párrafos que no se mueven conservan su orden (incluidos los propios); cada uno a reubicar se coloca después
    /// de la copia del párrafo que lo precede en el origen. Al final, el bloque queda numerado 1, 2, 3…
    /// </summary>
    private static void Ordenar(BloqueContenido bloque, List<ParrafoContenido> reubicar, BloqueContenido origen)
    {
        var enOrigen = origen.Parrafos.ToDictionary(p => p.Id, p => p.Orden, StringComparer.Ordinal);
        int OrdenEnOrigen(ParrafoContenido p) => enOrigen.GetValueOrDefault(p.OrigenParrafoId ?? "", int.MaxValue);

        var lista = bloque.Parrafos.Where(p => !reubicar.Contains(p)).OrderBy(p => p.Orden).ToList();
        foreach (var movil in bloque.Parrafos.Where(reubicar.Contains).OrderBy(OrdenEnOrigen).ToList())
        {
            var posicion = 0;
            var orden = OrdenEnOrigen(movil);
            foreach (var previo in origen.Parrafos.Where(p => p.Orden < orden).OrderByDescending(p => p.Orden))
            {
                var indice = lista.FindLastIndex(p => p.OrigenParrafoId == previo.Id);
                if (indice >= 0)
                {
                    posicion = indice + 1;
                    break;
                }
            }
            lista.Insert(posicion, movil);
        }

        for (var i = 0; i < lista.Count; i++)
            lista[i].Orden = i + 1;
        bloque.Parrafos.Clear();
        bloque.Parrafos.AddRange(lista);
    }

    private static BloqueContenido Bloque(ContenidoPlantilla contenido, string codigo)
    {
        var bloque = contenido.Bloques.FirstOrDefault(b => b.Codigo == codigo);
        if (bloque is null)
        {
            bloque = new BloqueContenido { Codigo = codigo };
            contenido.Bloques.Add(bloque);
        }
        return bloque;
    }

    private static Dictionary<string, string> Instantanea(ContenidoPlantilla contenido) =>
        contenido.Bloques.SelectMany(b => b.Parrafos).ToDictionary(p => p.Id, p => Huella(contenido, p), StringComparer.Ordinal);

    private static string Huella(ContenidoPlantilla contenido, ParrafoContenido p) => JsonSerializer.Serialize(new
    {
        Bloque = contenido.Bloques.First(b => b.Parrafos.Contains(p)).Codigo,
        p.Orden, p.Contenido, p.Activo, p.MostrarSoloSiCumple, p.Condicion, p.UnirConAnterior,
    }, OpcionesJson.Predeterminadas);

    internal static T Clonar<T>(T valor) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(valor, OpcionesJson.Predeterminadas), OpcionesJson.Predeterminadas)!;
}
