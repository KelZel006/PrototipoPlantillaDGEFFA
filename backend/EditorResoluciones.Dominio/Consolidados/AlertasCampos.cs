using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Cambio en un campo vigilado (por ejemplo, Operador) que se le avisa al aprobador.</summary>
public sealed record AlertaCampo(
    string PropuestaId,
    string Plantilla,
    string CampoId,
    string Campo,
    string Ubicacion,
    string Detalle);

/// <summary>
/// Detecta los cambios de una propuesta, respecto de su versión original, en un campo marcado «Alertar cambios al
/// aprobador» (<see cref="Campo.AlertarCambios"/>). Solo cuenta lo que le pasa al campo mismo: que se agregue o se quite
/// del texto, que cambien las reglas de una condición sobre él, o que se agregue, elimine, active o desactive un párrafo
/// que lo usa. Cambiar el texto alrededor del campo, sin tocarlo, no se avisa. Solo se avisa en una MODIFICACIÓN de una
/// plantilla vigente: una plantilla nueva (CREAR) no tiene un Operador anterior que haya cambiado.
/// </summary>
public static class AlertasCampos
{
    public static IReadOnlyList<AlertaCampo> Detectar(Propuesta propuesta, ContenidoPlantilla original, IReadOnlyCollection<Campo> vigilados)
    {
        var alertas = new List<AlertaCampo>();
        if (vigilados.Count == 0 || propuesta.Operacion != OperacionPropuesta.Modificar)
            return alertas;

        var viejos = Parrafos(original);
        var nuevos = Parrafos(propuesta.ContenidoPropuesto);
        foreach (var campo in vigilados)
        {
            var etiqueta = $"{{{campo.Etiqueta}}}";
            foreach (var id in nuevos.Keys.Concat(viejos.Keys.Where(k => !nuevos.ContainsKey(k))))
            {
                viejos.TryGetValue(id, out var viejo);
                nuevos.TryGetValue(id, out var nuevo);
                var (textoViejo, condicionVieja) = Usos(viejo?.Parrafo, campo.Id);
                var (textoNuevo, condicionNueva) = Usos(nuevo?.Parrafo, campo.Id);
                if (!textoViejo && !condicionVieja && !textoNuevo && !condicionNueva)
                    continue;

                var cambios = new List<string>();
                if (viejo is null)
                {
                    cambios.Add(textoNuevo ? $"párrafo nuevo con {etiqueta}" : $"párrafo nuevo con una condición sobre «{campo.Etiqueta}»");
                }
                else if (nuevo is null)
                {
                    cambios.Add(textoViejo ? $"se eliminó un párrafo con {etiqueta}" : $"se eliminó un párrafo con una condición sobre «{campo.Etiqueta}»");
                }
                else
                {
                    // El campo en el texto: solo cuenta si se agrega o se quita, no lo que se escribe alrededor.
                    var vecesAntes = ContenidoEditor.VecesCampo(viejo.Parrafo.Contenido, campo.Id);
                    var vecesAhora = ContenidoEditor.VecesCampo(nuevo.Parrafo.Contenido, campo.Id);
                    if (vecesAntes != vecesAhora)
                    {
                        cambios.Add(vecesAhora == 0 ? $"se quitó {etiqueta} del texto"
                            : vecesAntes == 0 ? $"se agregó {etiqueta} al texto"
                            : vecesAhora > vecesAntes ? $"se agregó otro {etiqueta} al texto"
                            : $"se quitó un {etiqueta} del texto");
                    }
                    // Las condiciones: solo las reglas que usan el campo.
                    if (!ComparadorContenido.MismoJson(ReglasDel(viejo.Parrafo.Condicion, campo.Id), ReglasDel(nuevo.Parrafo.Condicion, campo.Id)))
                        cambios.Add($"cambió la condición sobre «{campo.Etiqueta}»");
                    var enTextoViejo = CondicionesDeTexto(viejo.Parrafo, campo.Id).SelectMany(c => ReglasDel(c, campo.Id)).ToList();
                    var enTextoNuevo = CondicionesDeTexto(nuevo.Parrafo, campo.Id).SelectMany(c => ReglasDel(c, campo.Id)).ToList();
                    if (!ComparadorContenido.MismoJson(enTextoViejo, enTextoNuevo))
                        cambios.Add($"cambió una condición dentro del texto sobre «{campo.Etiqueta}»");
                    // Activar o desactivar el párrafo hace aparecer o desaparecer el campo del documento.
                    if (viejo.Parrafo.Activo != nuevo.Parrafo.Activo && (textoViejo || textoNuevo))
                        cambios.Add(nuevo.Parrafo.Activo ? $"se activó un párrafo con {etiqueta}" : $"se desactivó un párrafo con {etiqueta}");
                    if (viejo.Parrafo.MostrarSoloSiCumple != nuevo.Parrafo.MostrarSoloSiCumple && (condicionVieja || condicionNueva))
                        cambios.Add("cambió «mostrar solo si cumple»");
                }
                if (cambios.Count == 0)
                    continue;

                var ubicacion = nuevo ?? viejo!;
                alertas.Add(new AlertaCampo(propuesta.Id, propuesta.Nombre, campo.Id, campo.Etiqueta,
                    $"{CodigosBloque.Nombre(ubicacion.Bloque)} {ubicacion.Posicion}", string.Join("; ", cambios)));
            }
        }
        return alertas;
    }

    private sealed record Ubicado(string Bloque, int Posicion, ParrafoContenido Parrafo);

    private static Dictionary<string, Ubicado> Parrafos(ContenidoPlantilla contenido) =>
        contenido.Bloques
            .SelectMany(b => b.Parrafos.OrderBy(p => p.Orden).Select((p, i) => new Ubicado(b.Codigo, i + 1, p)))
            .GroupBy(u => u.Parrafo.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    /// <summary>
    /// Si el párrafo usa el campo en su texto y si lo usa en una condición (la del párrafo o la de un texto condicionado).
    /// </summary>
    private static (bool Texto, bool Condicion) Usos(ParrafoContenido? parrafo, string campoId) => parrafo is null
        ? (false, false)
        : (ContenidoEditor.CamposUsados(parrafo.Contenido).Contains(campoId),
           (parrafo.Condicion is not null && ReglasCondiciones.Reglas(parrafo.Condicion).Any(r => r.CampoId == campoId)) ||
           CondicionesDeTexto(parrafo, campoId).Count > 0);

    /// <summary>Reglas de la condición que usan el campo; sin condición, ninguna.</summary>
    private static List<ReglaCondicion> ReglasDel(GrupoCondiciones? condicion, string campoId) =>
        condicion is null ? [] : ReglasCondiciones.Reglas(condicion).Where(r => r.CampoId == campoId).ToList();

    /// <summary>Condiciones de los textos condicionados del párrafo que usan el campo.</summary>
    private static IReadOnlyList<GrupoCondiciones> CondicionesDeTexto(ParrafoContenido parrafo, string campoId) =>
        ContenidoEditor.CondicionesDeTexto(parrafo.Contenido).Where(c => ReglasCondiciones.Reglas(c).Any(r => r.CampoId == campoId)).ToList();
}
