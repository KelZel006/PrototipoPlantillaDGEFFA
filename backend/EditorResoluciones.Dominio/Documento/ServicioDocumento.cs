using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>
/// Construye el documento a partir de la plantilla guardada:
/// <list type="number">
/// <item>Recorre las secciones en el orden de las pestañas: Cabecera, Considerando y Resuelve.</item>
/// <item>Omite los párrafos inactivos y, con una solicitud de prueba, los que no cumplen su condición.</item>
/// <item>Sustituye cada campo por su valor. Sin solicitud, lo deja como "{Etiqueta}".</item>
/// </list>
/// Si faltan datos para evaluar una condición, el párrafo no se incluye y se registra una advertencia.
/// </summary>
public sealed class ServicioDocumento(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Parrafo> parrafos,
    IRepositorio<Campo> campos,
    IRepositorio<Solicitud> solicitudes,
    TimeProvider reloj)
{
    public static readonly Seccion[] OrdenSecciones = [Seccion.Cabecera, Seccion.Considerando, Seccion.Resuelve];

    public async Task<DocumentoResuelto> GenerarAsync(string plantillaId, string? solicitudId, CancellationToken ct = default)
    {
        var plantilla = await plantillas.ObtenerAsync(plantillaId, ct)
            ?? throw new EntidadNoEncontradaException(plantillas.Coleccion, plantillaId);
        Solicitud? solicitud = null;
        if (!string.IsNullOrEmpty(solicitudId))
        {
            solicitud = await solicitudes.ObtenerAsync(solicitudId, ct)
                ?? throw new EntidadNoEncontradaException(solicitudes.Coleccion, solicitudId);
        }

        var catalogo = (await campos.ListarAsync(ct)).ToDictionary(c => c.Id, StringComparer.Ordinal);
        var todos = (await parrafos.ListarAsync(ct)).Where(p => p.PlantillaId == plantilla.Id).ToList();
        var advertencias = new List<string>();

        if (solicitud is not null && (solicitud.RubroId != plantilla.RubroId || solicitud.SubRubroId != plantilla.SubRubroId))
            advertencias.Add("La solicitud de prueba pertenece a otro rubro o sub rubro que la plantilla.");

        (Campo? Campo, object? Valor) ObtenerValor(string campoId)
        {
            if (!catalogo.TryGetValue(campoId, out var campo))
                return (null, null);
            if (solicitud is null || campo.Origen.Coleccion != OrigenesDatos.Solicitudes)
                return (campo, null);
            return (campo, OrigenesDatos.Propiedad(campo.Origen.Coleccion, campo.Origen.Propiedad)?.GetValue(solicitud));
        }

        var secciones = new List<SeccionDocumento>();
        foreach (var seccion in OrdenSecciones)
        {
            var incluidos = new List<ParrafoDocumento>();
            var omitidos = new List<ParrafoOmitido>();
            var deSeccion = todos.Where(p => p.Seccion == seccion).OrderBy(p => p.Orden).ToList();

            for (var i = 0; i < deSeccion.Count; i++)
            {
                var parrafo = deSeccion[i];
                var posicion = i + 1;
                var resumen = Resumir(parrafo.Contenido, catalogo);
                var ubicacion = $"{seccion} {posicion}";

                if (!parrafo.Activo)
                {
                    omitidos.Add(new ParrafoOmitido(parrafo.Id, posicion, resumen, "Párrafo inactivo."));
                    continue;
                }

                var condicion = parrafo.Condicion;
                if (solicitud is not null && parrafo.MostrarSoloSiCumple && condicion is not null &&
                    ReglasCondiciones.Reglas(condicion).Any())
                {
                    var descripcion = ReglasCondiciones.Describir(condicion, catalogo);
                    switch (EvaluadorCondiciones.Evaluar(condicion, ObtenerValor))
                    {
                        case ResultadoCondicion.Falso:
                            omitidos.Add(new ParrafoOmitido(parrafo.Id, posicion, resumen, $"No se cumple la condición: {descripcion}."));
                            continue;
                        case ResultadoCondicion.Indeterminado:
                            omitidos.Add(new ParrafoOmitido(parrafo.Id, posicion, resumen, $"Faltan datos para evaluar la condición: {descripcion}."));
                            advertencias.Add($"{ubicacion}: no se incluyó porque faltan datos para evaluar su condición.");
                            continue;
                    }
                }

                var contenido = Sustituir(parrafo.Contenido, catalogo, ObtenerValor, solicitud is not null, ubicacion, advertencias);
                incluidos.Add(new ParrafoDocumento(parrafo.Id, posicion, contenido));
            }

            secciones.Add(new SeccionDocumento(seccion, incluidos, omitidos));
        }

        return new DocumentoResuelto(plantilla, solicitud, reloj.GetUtcNow(), secciones, advertencias.Distinct().ToList());
    }

    /// <summary>Copia el contenido reemplazando cada campo por un texto con sus mismas marcas de formato.</summary>
    private static NodoContenido Sustituir(
        NodoContenido nodo,
        IReadOnlyDictionary<string, Campo> catalogo,
        Func<string, (Campo? Campo, object? Valor)> obtenerValor,
        bool conSolicitud,
        string ubicacion,
        List<string> advertencias)
    {
        if (nodo.Tipo == ContenidoEditor.NodoCampo)
        {
            var campoId = nodo.Atributos is not null && nodo.Atributos.TryGetValue("campoId", out var id) ? id.GetString() ?? "" : "";
            var (campo, valor) = obtenerValor(campoId);
            var etiqueta = campo?.Etiqueta ?? campoId;
            var marcas = new List<MarcaContenido>(nodo.Marcas ?? []);

            string texto;
            if (!conSolicitud)
            {
                texto = $"{{{etiqueta}}}";
                marcas.Add(new MarcaContenido { Tipo = MarcasDocumento.CampoPlantilla });
            }
            else if (campo is not null && FormatoValores.Formatear(campo.TipoDato, valor) is { } formateado)
            {
                texto = formateado;
                marcas.Add(new MarcaContenido { Tipo = MarcasDocumento.CampoResuelto });
            }
            else
            {
                texto = $"[sin dato: {etiqueta}]";
                marcas.Add(new MarcaContenido { Tipo = MarcasDocumento.CampoSinDato });
                advertencias.Add($"{ubicacion}: la solicitud no tiene dato para «{etiqueta}».");
            }
            return new NodoContenido { Tipo = "text", Texto = texto, Marcas = marcas };
        }

        return new NodoContenido
        {
            Tipo = nodo.Tipo,
            Texto = nodo.Texto,
            Atributos = nodo.Atributos is null ? null : new Dictionary<string, JsonElement>(nodo.Atributos),
            Marcas = nodo.Marcas?.ToList(),
            Contenido = nodo.Contenido?.Select(h => Sustituir(h, catalogo, obtenerValor, conSolicitud, ubicacion, advertencias)).ToList(),
        };
    }

    private static string Resumir(NodoContenido contenido, IReadOnlyDictionary<string, Campo> catalogo)
    {
        var texto = ContenidoEditor.TextoPlano(contenido, catalogo);
        return texto.Length > 90 ? texto[..90] + "…" : texto;
    }
}
