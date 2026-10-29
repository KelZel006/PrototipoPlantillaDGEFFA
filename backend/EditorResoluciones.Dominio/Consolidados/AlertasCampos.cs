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
/// Detecta los cambios de una propuesta, respecto de su versión original, en los párrafos que usan un campo marcado
/// «Alertar cambios al aprobador» (<see cref="Campo.AlertarCambios"/>): en el texto o en la condición, al agregarlos,
/// eliminarlos, activarlos o desactivarlos. Solo se avisa en una MODIFICACIÓN de una plantilla vigente: una plantilla
/// nueva (CREAR) no tiene un Operador anterior que haya cambiado.
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
                    if (!ComparadorContenido.MismoJson(viejo.Parrafo.Contenido, nuevo.Parrafo.Contenido) && (textoViejo || textoNuevo))
                    {
                        cambios.Add(textoViejo && !textoNuevo ? $"se quitó {etiqueta} del texto"
                            : !textoViejo && textoNuevo ? $"se agregó {etiqueta} al texto"
                            : $"cambió el texto alrededor de {etiqueta}");
                    }
                    if (!ComparadorContenido.MismoJson(viejo.Parrafo.Condicion, nuevo.Parrafo.Condicion) && (condicionVieja || condicionNueva))
                        cambios.Add($"cambió la condición sobre «{campo.Etiqueta}»");
                    if (viejo.Parrafo.Activo != nuevo.Parrafo.Activo)
                        cambios.Add(nuevo.Parrafo.Activo ? "se activó el párrafo" : "se desactivó el párrafo");
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

    /// <summary>Si el párrafo usa el campo en su texto y si lo usa en su condición.</summary>
    private static (bool Texto, bool Condicion) Usos(ParrafoContenido? parrafo, string campoId) => parrafo is null
        ? (false, false)
        : (ContenidoEditor.CamposUsados(parrafo.Contenido).Contains(campoId),
           parrafo.Condicion is not null && ReglasCondiciones.Reglas(parrafo.Condicion).Any(r => r.CampoId == campoId));
}
