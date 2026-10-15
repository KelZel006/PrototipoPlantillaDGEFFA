using System.Globalization;
using System.Text.Json;
using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Documento;

/// <summary>Interlineados que se pueden elegir al generar el documento.</summary>
public static class Interlineados
{
    public const double Predeterminado = 1.5;

    /// <summary>De 0.8 a 2.0, de 0.1 en 0.1.</summary>
    public static IReadOnlyList<double> Permitidos { get; } =
        Enumerable.Range(8, 13).Select(decimas => decimas / 10.0).ToList();

    /// <summary>Interpreta el valor recibido (por ejemplo "0.8", "1.1" o "2"). Sin valor, usa el predeterminado.</summary>
    public static double Interpretar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return Predeterminado;
        if (double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero) &&
            Permitidos.Any(p => Math.Abs(p - numero) < 0.001))
        {
            return numero;
        }

        var resultado = new ResultadoValidacion();
        resultado.Agregar("interlineado", "El interlineado debe estar entre 0.8 y 2, de 0.1 en 0.1.");
        resultado.LanzarSiHayErrores();
        return Predeterminado;
    }
}

/// <summary>
/// Construye el documento a partir de la plantilla guardada:
/// <list type="number">
/// <item>Recorre las secciones en el orden de las pestañas: Cabecera, Considerando y Resuelve.</item>
/// <item>Omite los párrafos inactivos y, con una solicitud de prueba, los que no cumplen su condición.</item>
/// <item>Sustituye cada campo por su valor. Sin solicitud, lo deja como "{Etiqueta}".</item>
/// <item>En las tablas, repite cada fila con campos de lista una vez por registro de la solicitud.</item>
/// </list>
/// Si faltan datos para evaluar una condición, el párrafo no se incluye y se registra una advertencia.
/// </summary>
public sealed class ServicioDocumento(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Parrafo> parrafos,
    IRepositorio<Campo> campos,
    IRepositorio<Solicitud> solicitudes,
    IRepositorio<DetalleBien> bienes,
    IAlmacenArchivos archivos,
    TimeProvider reloj)
{
    public static readonly Seccion[] OrdenSecciones = [Seccion.Cabecera, Seccion.Considerando, Seccion.PorTanto, Seccion.Resuelve, Seccion.Firmas];

    public const string TextoSinRegistros = "No hay registros para esta solicitud.";

    public async Task<DocumentoResuelto> GenerarAsync(
        string plantillaId,
        string? solicitudId,
        double interlineado = Interlineados.Predeterminado,
        CancellationToken ct = default)
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
        var registros = solicitud is null
            ? []
            : (await bienes.ListarAsync(ct)).Where(b => b.SolicitudId == solicitud.Id).OrderBy(b => b.Id, StringComparer.Ordinal).ToList();
        var advertencias = new List<string>();

        if (solicitud is not null && (solicitud.RubroId != plantilla.RubroId || solicitud.SubRubroId != plantilla.SubRubroId))
            advertencias.Add("La solicitud de prueba pertenece a otro rubro o sub rubro que la plantilla.");

        var contexto = new ContextoSustitucion(catalogo, solicitud, registros, advertencias);
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
                    switch (EvaluadorCondiciones.Evaluar(condicion, id => contexto.Valor(id, null)))
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

                var contenido = contexto.Sustituir(parrafo.Contenido, null, ubicacion);
                // Considerando y Resuelve se numeran por su orden en el documento: los omitidos no consumen número.
                if (Numeracion.Etiqueta(seccion, incluidos.Count + 1) is { } etiqueta)
                    contenido = Numerar(contenido, etiqueta);
                incluidos.Add(new ParrafoDocumento(parrafo.Id, posicion, contenido));
            }

            secciones.Add(new SeccionDocumento(seccion, incluidos, omitidos));
        }

        byte[]? logo = null;
        if (!string.IsNullOrEmpty(plantilla.LogoId))
        {
            logo = await archivos.LeerAsync(plantilla.LogoId, ct);
            if (logo is null)
                advertencias.Add("No se encontró el archivo del logo de la plantilla; el documento se generó sin logo.");
        }
        var diseno = new DisenoDocumento(logo, plantilla.AlineacionLogo, plantilla.PiePagina, interlineado, plantilla.EspacioFirmasCm);

        return new DocumentoResuelto(plantilla, solicitud, reloj.GetUtcNow(), secciones, advertencias.Distinct().ToList(), diseno);
    }

    /// <summary>
    /// Antepone la etiqueta (por ejemplo, "CONSIDERANDO(1): " o "PRIMERO: ") en negrita al primer párrafo. Si el bloque
    /// empieza con una lista o una tabla, la etiqueta va en un párrafo propio antes de ella.
    /// </summary>
    private static NodoContenido Numerar(NodoContenido documento, string texto)
    {
        var etiqueta = new NodoContenido
        {
            Tipo = "text",
            Texto = texto,
            Marcas = [new MarcaContenido { Tipo = "bold" }],
        };
        var bloques = documento.Contenido?.ToList() ?? [];
        if (bloques.FirstOrDefault() is { Tipo: "paragraph" } primero)
        {
            bloques[0] = new NodoContenido
            {
                Tipo = primero.Tipo,
                Atributos = primero.Atributos,
                Contenido = [etiqueta, .. primero.Contenido ?? []],
            };
        }
        else
        {
            bloques.Insert(0, new NodoContenido { Tipo = "paragraph", Contenido = [etiqueta] });
        }
        return new NodoContenido { Tipo = documento.Tipo, Atributos = documento.Atributos, Contenido = bloques };
    }

    private static string Resumir(NodoContenido contenido, IReadOnlyDictionary<string, Campo> catalogo)
    {
        var texto = ContenidoEditor.TextoPlano(contenido, catalogo);
        return texto.Length > 90 ? texto[..90] + "…" : texto;
    }

    /// <summary>Datos y reglas para reemplazar los campos de un párrafo por sus valores.</summary>
    private sealed class ContextoSustitucion(
        IReadOnlyDictionary<string, Campo> catalogo,
        Solicitud? solicitud,
        IReadOnlyList<DetalleBien> registros,
        List<string> advertencias)
    {
        /// <summary>
        /// Valor de un campo: los campos generales salen de la solicitud; los de lista, del registro de la fila
        /// que se está generando.
        /// </summary>
        public (Campo? Campo, object? Valor) Valor(string campoId, DetalleBien? registro)
        {
            if (!catalogo.TryGetValue(campoId, out var campo))
                return (null, null);
            object? origen = OrigenesDatos.EsLista(campo.Origen.Coleccion) ? registro : solicitud;
            return origen is null
                ? (campo, null)
                : (campo, OrigenesDatos.Propiedad(campo.Origen.Coleccion, campo.Origen.Propiedad)?.GetValue(origen));
        }

        public NodoContenido Sustituir(NodoContenido nodo, DetalleBien? registro, string ubicacion)
        {
            if (nodo.Tipo == ContenidoEditor.NodoCampo)
                return SustituirCampo(nodo, registro, ubicacion);
            if (nodo.Tipo == "table")
                return SustituirTabla(nodo, ubicacion);

            return new NodoContenido
            {
                Tipo = nodo.Tipo,
                Texto = nodo.Texto,
                Atributos = nodo.Atributos is null ? null : new Dictionary<string, JsonElement>(nodo.Atributos),
                Marcas = nodo.Marcas?.ToList(),
                Contenido = nodo.Contenido?.Select(h => Sustituir(h, registro, ubicacion)).ToList(),
            };
        }

        /// <summary>
        /// Copia la tabla. Una fila con campos de lista se repite una vez por registro; sin registros,
        /// se reemplaza por una fila que lo indica. Sin solicitud, la fila se deja una vez con los campos como {Etiqueta}.
        /// </summary>
        private NodoContenido SustituirTabla(NodoContenido tabla, string ubicacion)
        {
            var filas = new List<NodoContenido>();
            var columnas = (tabla.Contenido ?? []).Select(f => (f.Contenido ?? []).Sum(ContenidoEditor.Expansion)).DefaultIfEmpty(1).Max();

            foreach (var fila in tabla.Contenido ?? [])
            {
                if (solicitud is null || !ContenidoEditor.EsFilaDeLista(fila, catalogo))
                {
                    filas.Add(Sustituir(fila, null, ubicacion));
                }
                else if (registros.Count == 0)
                {
                    filas.Add(FilaSinRegistros(columnas));
                    advertencias.Add($"{ubicacion}: la solicitud no tiene registros para la tabla.");
                }
                else
                {
                    filas.AddRange(registros.Select(registro => Sustituir(fila, registro, ubicacion)));
                }
            }

            return new NodoContenido { Tipo = "table", Contenido = filas };
        }

        private NodoContenido SustituirCampo(NodoContenido nodo, DetalleBien? registro, string ubicacion)
        {
            var campoId = nodo.Atributos is not null && nodo.Atributos.TryGetValue("campoId", out var id) ? id.GetString() ?? "" : "";
            var (campo, valor) = Valor(campoId, registro);
            var etiqueta = campo?.Etiqueta ?? campoId;
            var marcas = new List<MarcaContenido>(nodo.Marcas ?? []);

            string texto;
            if (solicitud is null)
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
                advertencias.Add($"{ubicacion}: falta el dato «{etiqueta}».");
            }
            return new NodoContenido { Tipo = "text", Texto = texto, Marcas = marcas };
        }

        private static NodoContenido FilaSinRegistros(int columnas) => new()
        {
            Tipo = "tableRow",
            Contenido =
            [
                new NodoContenido
                {
                    Tipo = "tableCell",
                    Atributos = new Dictionary<string, JsonElement> { ["colspan"] = JsonSerializer.SerializeToElement(columnas) },
                    Contenido =
                    [
                        new NodoContenido
                        {
                            Tipo = "paragraph",
                            Contenido = [new NodoContenido { Tipo = "text", Texto = TextoSinRegistros, Marcas = [new MarcaContenido { Tipo = "italic" }] }],
                        },
                    ],
                },
            ],
        };
    }
}
