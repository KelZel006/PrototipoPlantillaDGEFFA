using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Validacion;

public sealed class ValidadorRubro(
    IRepositorio<SubRubro> subRubros,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Solicitud> solicitudes) : ValidadorEntidad<Rubro>
{
    public override async Task ValidarEliminacionAsync(Rubro rubro, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await subRubros.ListarAsync(ct)).Any(s => s.RubroId == rubro.Id))
            resultado.Agregar("id", "No se puede eliminar el rubro porque tiene sub rubros.");
        if ((await plantillas.ListarAsync(ct)).Any(p => p.RubroId == rubro.Id))
            resultado.Agregar("id", "No se puede eliminar el rubro porque lo usan plantillas.");
        if ((await solicitudes.ListarAsync(ct)).Any(s => s.RubroId == rubro.Id))
            resultado.Agregar("id", "No se puede eliminar el rubro porque lo usan solicitudes de prueba.");
    }
}

public sealed class ValidadorSubRubro(
    IRepositorio<Rubro> rubros,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Solicitud> solicitudes) : ValidadorEntidad<SubRubro>
{
    public override async Task ValidarGuardadoAsync(SubRubro subRubro, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (await rubros.ObtenerAsync(subRubro.RubroId, ct) is null)
            resultado.Agregar("rubroId", $"El rubro '{subRubro.RubroId}' no existe.");
    }

    public override async Task ValidarEliminacionAsync(SubRubro subRubro, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await plantillas.ListarAsync(ct)).Any(p => p.SubRubroId == subRubro.Id))
            resultado.Agregar("id", "No se puede eliminar el sub rubro porque lo usan plantillas.");
        if ((await solicitudes.ListarAsync(ct)).Any(s => s.SubRubroId == subRubro.Id))
            resultado.Agregar("id", "No se puede eliminar el sub rubro porque lo usan solicitudes de prueba.");
    }
}

public sealed class ValidadorTipoDocumento(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<BloqueDocumental> bloques) : ValidadorEntidad<TipoDocumento>
{
    public override async Task ValidarGuardadoAsync(TipoDocumento tipo, ResultadoValidacion resultado, CancellationToken ct)
    {
        var catalogo = (await bloques.ListarAsync(ct)).ToDictionary(b => b.Id, StringComparer.Ordinal);
        foreach (var bloque in tipo.Bloques.Where(b => !catalogo.ContainsKey(b.BloqueId)))
            resultado.Agregar("bloques", $"El bloque '{bloque.BloqueId}' no existe en el catálogo.");
        if (tipo.Bloques.Select(b => b.BloqueId).Distinct(StringComparer.Ordinal).Count() != tipo.Bloques.Count)
            resultado.Agregar("bloques", "Un bloque no puede repetirse en el mismo tipo de documento.");
    }

    public override async Task ValidarEliminacionAsync(TipoDocumento tipo, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await plantillas.ListarAsync(ct)).Any(p => p.TipoDocumentoId == tipo.Id))
            resultado.Agregar("id", "No se puede eliminar el tipo de documento porque lo usan plantillas.");
    }
}

public sealed partial class ValidadorBloqueDocumental(IRepositorio<TipoDocumento> tipos) : ValidadorEntidad<BloqueDocumental>
{
    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,39}$")]
    private static partial Regex CodigoValido();

    public override Task ValidarGuardadoAsync(BloqueDocumental bloque, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (!CodigoValido().IsMatch(bloque.Id))
            resultado.Agregar("id", "El código debe empezar con una letra y usar solo mayúsculas, números y guion bajo (2 a 40 caracteres).");
        if (bloque.Id == CodigosBloque.Diseno)
            resultado.Agregar("id", "El código DISENO está reservado para el diseño del documento.");
        if (!Campo.ContextosValidos.Contains(bloque.ContextoCampos) || bloque.ContextoCampos == "tabla")
            resultado.Agregar("contextoCampos", $"El contexto debe ser uno de: {string.Join(", ", Campo.ContextosValidos.Where(c => c != "tabla"))}.");
        if (!Enum.IsDefined(bloque.Numeracion))
            resultado.Agregar("numeracion", "La numeración no es válida.");
        return Task.CompletedTask;
    }

    public override async Task ValidarEliminacionAsync(BloqueDocumental bloque, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await tipos.ListarAsync(ct)).Any(t => t.Bloques.Any(b => b.BloqueId == bloque.Id)))
            resultado.Agregar("id", "No se puede eliminar el bloque porque lo usa un tipo de documento.");
    }
}

public sealed class ValidadorUnidad(IRepositorio<Plantilla> plantillas, IRepositorio<DocumentoConsolidado> consolidados)
    : ValidadorEntidad<Unidad>
{
    public override async Task ValidarEliminacionAsync(Unidad unidad, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await plantillas.ListarAsync(ct)).Any(p => p.UnidadId == unidad.Id) ||
            (await consolidados.ListarAsync(ct)).Any(c => c.UnidadId == unidad.Id))
            resultado.Agregar("id", "No se puede eliminar la unidad porque tiene plantillas o documentos. Desactívela.");
    }
}

public sealed class ValidadorClaseDocumento(IRepositorio<Plantilla> plantillas) : ValidadorEntidad<ClaseDocumento>
{
    public override async Task ValidarEliminacionAsync(ClaseDocumento clase, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await plantillas.ListarAsync(ct)).Any(p => p.ClaseDocumentoId == clase.Id))
            resultado.Agregar("id", "No se puede eliminar el documento porque lo usan plantillas.");
    }
}

public sealed class ValidadorPlantilla(
    IRepositorio<Unidad> unidades,
    IRepositorio<Rubro> rubros,
    IRepositorio<SubRubro> subRubros,
    IRepositorio<TipoDocumento> tiposDocumento,
    IRepositorio<ClaseDocumento> clasesDocumento,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Parrafo> parrafos,
    IRepositorio<ArchivoRecurso> archivos) : ValidadorEntidad<Plantilla>
{
    public override async Task ValidarGuardadoAsync(Plantilla plantilla, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (!Enum.IsDefined(plantilla.Estado))
            resultado.Agregar("estado", "El estado de la plantilla no es válido.");
        if (!Enum.IsDefined(plantilla.AlineacionLogo))
            resultado.Agregar("alineacionLogo", "La alineación del logo no es válida.");
        if (!string.IsNullOrEmpty(plantilla.LogoId) && await archivos.ObtenerAsync(plantilla.LogoId, ct) is null)
            resultado.Agregar("logoId", "El logo indicado no existe. Use la opción de cargar logo.");

        if (await unidades.ObtenerAsync(plantilla.UnidadId, ct) is null)
            resultado.Agregar("unidadId", $"La unidad '{plantilla.UnidadId}' no existe.");

        if (await rubros.ObtenerAsync(plantilla.RubroId, ct) is null)
            resultado.Agregar("rubroId", $"El rubro '{plantilla.RubroId}' no existe.");

        var subRubro = await subRubros.ObtenerAsync(plantilla.SubRubroId, ct);
        if (subRubro is null)
            resultado.Agregar("subRubroId", $"El sub rubro '{plantilla.SubRubroId}' no existe.");
        else if (subRubro.RubroId != plantilla.RubroId)
            resultado.Agregar("subRubroId", "El sub rubro no pertenece al rubro seleccionado.");

        if (await tiposDocumento.ObtenerAsync(plantilla.TipoDocumentoId, ct) is null)
            resultado.Agregar("tipoDocumentoId", $"El tipo de documento '{plantilla.TipoDocumentoId}' no existe.");

        if (await clasesDocumento.ObtenerAsync(plantilla.ClaseDocumentoId, ct) is null)
            resultado.Agregar("claseDocumentoId", $"El documento '{plantilla.ClaseDocumentoId}' no existe.");

        var repetida = (await plantillas.ListarAsync(ct)).Any(p =>
            p.Id != plantilla.Id &&
            p.UnidadId == plantilla.UnidadId &&
            p.RubroId == plantilla.RubroId &&
            p.SubRubroId == plantilla.SubRubroId &&
            p.TipoDocumentoId == plantilla.TipoDocumentoId &&
            p.ClaseDocumentoId == plantilla.ClaseDocumentoId);
        if (repetida)
            resultado.Agregar("claseDocumentoId", "Ya existe una plantilla de esta unidad para esta combinación de Rubro, Sub Rubro, Tipo Documento y Documento.");
    }

    public override async Task ValidarEliminacionAsync(Plantilla plantilla, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await parrafos.ListarAsync(ct)).Any(p => p.PlantillaId == plantilla.Id))
            resultado.Agregar("id", "No se puede eliminar la plantilla porque tiene párrafos. Elimínelos primero.");
    }
}

public sealed class ValidadorParrafo(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Campo> campos) : ValidadorEntidad<Parrafo>
{
    public override async Task ValidarGuardadoAsync(Parrafo parrafo, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (!Enum.IsDefined(parrafo.Seccion))
        {
            resultado.Agregar("seccion", "La sección no es válida.");
            return;
        }

        if (await plantillas.ObtenerAsync(parrafo.PlantillaId, ct) is null)
            resultado.Agregar("plantillaId", $"La plantilla '{parrafo.PlantillaId}' no existe.");

        var catalogo = (await campos.ListarAsync(ct)).ToDictionary(c => c.Id, StringComparer.Ordinal);
        ContenidoEditor.Validar(parrafo.Contenido, Contexto(parrafo.Seccion), catalogo, resultado);

        // La condición de un párrafo se evalúa con los datos generales de la solicitud.
        if (parrafo.Condicion is not null)
            ReglasCondiciones.Validar(parrafo.Condicion, OrigenesDatos.Solicitudes, catalogo, resultado);
    }

    /// <summary>Contexto de uso de los campos para una sección (coincide con <see cref="Campo.Contextos"/>).</summary>
    public static string Contexto(Seccion seccion) => seccion.ToString().ToLowerInvariant();
}

/// <summary>Calcula en el servidor los campos usados y, si hace falta, la posición del párrafo.</summary>
public sealed class NormalizadorParrafo(IRepositorio<Parrafo> parrafos) : INormalizadorEntidad<Parrafo>
{
    public async Task NormalizarAsync(Parrafo parrafo, CancellationToken ct)
    {
        // No se confía en la lista enviada por el cliente: se obtiene del contenido.
        parrafo.CamposUsados = ContenidoEditor.CamposUsados(parrafo.Contenido);

        // El vínculo con el párrafo de la Resolución lo pone el servidor al copiar; el cliente no lo cambia.
        var guardado = string.IsNullOrEmpty(parrafo.Id) ? null : await parrafos.ObtenerAsync(parrafo.Id, ct);
        parrafo.OrigenParrafoId = guardado?.OrigenParrafoId;

        // Una condición sin reglas equivale a no tener condición.
        if (parrafo.Condicion is { Reglas.Count: 0, Grupos.Count: 0 })
            parrafo.Condicion = null;

        if (parrafo.Orden == 0)
        {
            var hermanos = (await parrafos.ListarAsync(ct))
                .Where(p => p.PlantillaId == parrafo.PlantillaId && p.Seccion == parrafo.Seccion && p.Id != parrafo.Id)
                .ToList();
            parrafo.Orden = hermanos.Count == 0 ? 1 : hermanos.Max(p => p.Orden) + 1;
        }
    }
}

public sealed partial class ValidadorCampo(
    IRepositorio<Parrafo> parrafos,
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<Propuesta> propuestas) : ValidadorEntidad<Campo>
{
    [GeneratedRegex("^[a-z][a-z0-9_]{1,59}$")]
    private static partial Regex IdentificadorValido();

    public override Task ValidarGuardadoAsync(Campo campo, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (!IdentificadorValido().IsMatch(campo.Id))
            resultado.Agregar("id", "El identificador debe empezar con una letra minúscula y usar solo minúsculas, números y guion bajo (2 a 60 caracteres).");

        if (!Enum.IsDefined(campo.TipoDato))
            resultado.Agregar("tipoDato", "El tipo de dato no es válido.");

        if (!OrigenesDatos.Colecciones.ContainsKey(campo.Origen.Coleccion))
            resultado.Agregar("origen.coleccion", $"La colección '{campo.Origen.Coleccion}' no está permitida como origen de datos.");
        else if (!OrigenesDatos.PropiedadExiste(campo.Origen.Coleccion, campo.Origen.Propiedad))
            resultado.Agregar("origen.propiedad", $"La propiedad '{campo.Origen.Propiedad}' no existe en '{campo.Origen.Coleccion}'.");

        if (campo.Contextos.Count == 0)
            resultado.Agregar("contextos", "Indique al menos un contexto de uso.");
        foreach (var contexto in campo.Contextos.Where(c => !Campo.ContextosValidos.Contains(c)))
            resultado.Agregar("contextos", $"El contexto '{contexto}' no es válido.");

        return Task.CompletedTask;
    }

    public override async Task ValidarEliminacionAsync(Campo campo, ResultadoValidacion resultado, CancellationToken ct)
    {
        // Párrafos de la colección anterior, de las versiones publicadas y de las propuestas.
        var usos = (await parrafos.ListarAsync(ct)).Select(p => (p.CamposUsados, p.Condicion))
            .Concat((await versiones.ListarAsync(ct)).SelectMany(v => v.Contenido.Bloques).SelectMany(b => b.Parrafos)
                .Select(p => (p.CamposUsados, p.Condicion)))
            .Concat((await propuestas.ListarAsync(ct)).SelectMany(p => p.ContenidoPropuesto.Bloques).SelectMany(b => b.Parrafos)
                .Select(p => (p.CamposUsados, p.Condicion)))
            .ToList();
        if (usos.Any(u => u.CamposUsados.Contains(campo.Id)))
            resultado.Agregar("id", "No se puede eliminar el campo porque lo usan párrafos.");
        if (usos.Any(u => ReglasCondiciones.CamposUsados(u.Condicion).Contains(campo.Id)))
            resultado.Agregar("id", "No se puede eliminar el campo porque lo usan condiciones de párrafos.");
    }
}

public sealed class ValidadorSolicitud(
    IRepositorio<Rubro> rubros,
    IRepositorio<SubRubro> subRubros,
    IRepositorio<DetalleBien> bienes) : ValidadorEntidad<Solicitud>
{
    public override async Task ValidarGuardadoAsync(Solicitud solicitud, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (await rubros.ObtenerAsync(solicitud.RubroId, ct) is null)
            resultado.Agregar("rubroId", $"El rubro '{solicitud.RubroId}' no existe.");

        var subRubro = await subRubros.ObtenerAsync(solicitud.SubRubroId, ct);
        if (subRubro is null)
            resultado.Agregar("subRubroId", $"El sub rubro '{solicitud.SubRubroId}' no existe.");
        else if (subRubro.RubroId != solicitud.RubroId)
            resultado.Agregar("subRubroId", "El sub rubro no pertenece al rubro de la solicitud.");
    }

    public override async Task ValidarEliminacionAsync(Solicitud solicitud, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await bienes.ListarAsync(ct)).Any(b => b.SolicitudId == solicitud.Id))
            resultado.Agregar("id", "No se puede eliminar la solicitud porque tiene bienes registrados.");
    }
}

public sealed class ValidadorDetalleBien(IRepositorio<Solicitud> solicitudes) : ValidadorEntidad<DetalleBien>
{
    public override async Task ValidarGuardadoAsync(DetalleBien bien, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (await solicitudes.ObtenerAsync(bien.SolicitudId, ct) is null)
            resultado.Agregar("solicitudId", $"La solicitud '{bien.SolicitudId}' no existe.");
    }
}
