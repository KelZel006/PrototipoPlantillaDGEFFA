using System.Text.RegularExpressions;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;

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

public sealed class ValidadorTipoDocumento(IRepositorio<Plantilla> plantillas) : ValidadorEntidad<TipoDocumento>
{
    public override async Task ValidarEliminacionAsync(TipoDocumento tipo, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await plantillas.ListarAsync(ct)).Any(p => p.TipoDocumentoId == tipo.Id))
            resultado.Agregar("id", "No se puede eliminar el tipo de documento porque lo usan plantillas.");
    }
}

public sealed class ValidadorPlantilla(
    IRepositorio<Rubro> rubros,
    IRepositorio<SubRubro> subRubros,
    IRepositorio<TipoDocumento> tiposDocumento,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Parrafo> parrafos) : ValidadorEntidad<Plantilla>
{
    public override async Task ValidarGuardadoAsync(Plantilla plantilla, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (!Enum.IsDefined(plantilla.Estado))
            resultado.Agregar("estado", "El estado de la plantilla no es válido.");

        if (await rubros.ObtenerAsync(plantilla.RubroId, ct) is null)
            resultado.Agregar("rubroId", $"El rubro '{plantilla.RubroId}' no existe.");

        var subRubro = await subRubros.ObtenerAsync(plantilla.SubRubroId, ct);
        if (subRubro is null)
            resultado.Agregar("subRubroId", $"El sub rubro '{plantilla.SubRubroId}' no existe.");
        else if (subRubro.RubroId != plantilla.RubroId)
            resultado.Agregar("subRubroId", "El sub rubro no pertenece al rubro seleccionado.");

        if (await tiposDocumento.ObtenerAsync(plantilla.TipoDocumentoId, ct) is null)
            resultado.Agregar("tipoDocumentoId", $"El tipo de documento '{plantilla.TipoDocumentoId}' no existe.");

        var repetida = (await plantillas.ListarAsync(ct)).Any(p =>
            p.Id != plantilla.Id &&
            p.RubroId == plantilla.RubroId &&
            p.SubRubroId == plantilla.SubRubroId &&
            p.TipoDocumentoId == plantilla.TipoDocumentoId);
        if (repetida)
            resultado.Agregar("tipoDocumentoId", "Ya existe una plantilla para esta combinación de Rubro, Sub Rubro y Tipo Documento.");
    }

    public override async Task ValidarEliminacionAsync(Plantilla plantilla, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await parrafos.ListarAsync(ct)).Any(p => p.PlantillaId == plantilla.Id))
            resultado.Agregar("id", "No se puede eliminar la plantilla porque tiene párrafos. Elimínelos primero.");
    }
}

public sealed class ValidadorParrafo(
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Condicion> condiciones,
    IRepositorio<Campo> campos) : ValidadorEntidad<Parrafo>
{
    private const int ProfundidadMaximaContenido = 20;
    private const int NodosMaximos = 2000;

    public override async Task ValidarGuardadoAsync(Parrafo parrafo, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (!Enum.IsDefined(parrafo.Seccion))
            resultado.Agregar("seccion", "La sección no es válida.");

        if (await plantillas.ObtenerAsync(parrafo.PlantillaId, ct) is null)
            resultado.Agregar("plantillaId", $"La plantilla '{parrafo.PlantillaId}' no existe.");

        if (!string.IsNullOrEmpty(parrafo.CondicionId) && await condiciones.ObtenerAsync(parrafo.CondicionId, ct) is null)
            resultado.Agregar("condicionId", $"La condición '{parrafo.CondicionId}' no existe.");

        var catalogo = (await campos.ListarAsync(ct)).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var campoId in parrafo.CamposUsados.Where(id => !catalogo.Contains(id)))
            resultado.Agregar("camposUsados", $"El campo '{campoId}' no existe en el catálogo.");

        if (parrafo.Contenido.Tipo != "doc")
            resultado.Agregar("contenido", "El contenido debe ser un documento del editor.");

        var nodos = 0;
        if (!RecorrerContenido(parrafo.Contenido, 1, ref nodos))
            resultado.Agregar("contenido", "El contenido es demasiado grande o tiene demasiados niveles.");
    }

    private static bool RecorrerContenido(NodoContenido nodo, int profundidad, ref int nodos)
    {
        nodos++;
        if (profundidad > ProfundidadMaximaContenido || nodos > NodosMaximos)
            return false;
        foreach (var hijo in nodo.Contenido ?? [])
        {
            if (!RecorrerContenido(hijo, profundidad + 1, ref nodos))
                return false;
        }
        return true;
    }
}

public sealed partial class ValidadorCampo(
    IRepositorio<Parrafo> parrafos,
    IRepositorio<Condicion> condiciones) : ValidadorEntidad<Campo>
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
        if ((await parrafos.ListarAsync(ct)).Any(p => p.CamposUsados.Contains(campo.Id)))
            resultado.Agregar("id", "No se puede eliminar el campo porque lo usan párrafos.");
        if ((await condiciones.ListarAsync(ct)).Any(c => ValidadorCondicion.CamposUsados(c.Raiz).Contains(campo.Id)))
            resultado.Agregar("id", "No se puede eliminar el campo porque lo usan condiciones.");
    }
}

public sealed class ValidadorCondicion(
    IRepositorio<Campo> campos,
    IRepositorio<Parrafo> parrafos) : ValidadorEntidad<Condicion>
{
    private static readonly OperadorComparacion[] OperadoresSinValor = [OperadorComparacion.EsNulo, OperadorComparacion.NoEsNulo];

    public override async Task ValidarGuardadoAsync(Condicion condicion, ResultadoValidacion resultado, CancellationToken ct)
    {
        if (Profundidad(condicion.Raiz) > Condicion.ProfundidadMaxima)
            resultado.Agregar("raiz", $"Los grupos de condiciones admiten hasta {Condicion.ProfundidadMaxima} niveles.");

        var reglas = Reglas(condicion.Raiz).ToList();
        if (reglas.Count == 0)
            resultado.Agregar("raiz", "La condición debe tener al menos una regla.");

        var catalogo = (await campos.ListarAsync(ct)).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var regla in reglas)
        {
            if (!catalogo.Contains(regla.CampoId))
                resultado.Agregar("raiz", $"El campo '{regla.CampoId}' no existe en el catálogo.");
            if (!Enum.IsDefined(regla.Operador))
                resultado.Agregar("raiz", "Uno de los operadores no es válido.");
            else if (!OperadoresSinValor.Contains(regla.Operador) && regla.Valor is null)
                resultado.Agregar("raiz", $"La regla sobre '{regla.CampoId}' necesita un valor de comparación.");
        }
    }

    public override async Task ValidarEliminacionAsync(Condicion condicion, ResultadoValidacion resultado, CancellationToken ct)
    {
        if ((await parrafos.ListarAsync(ct)).Any(p => p.CondicionId == condicion.Id))
            resultado.Agregar("id", "No se puede eliminar la condición porque la usan párrafos.");
    }

    public static IEnumerable<ReglaCondicion> Reglas(GrupoCondiciones grupo) =>
        grupo.Reglas.Concat(grupo.Grupos.SelectMany(Reglas));

    public static HashSet<string> CamposUsados(GrupoCondiciones grupo) =>
        Reglas(grupo).Select(r => r.CampoId).ToHashSet(StringComparer.Ordinal);

    private static int Profundidad(GrupoCondiciones grupo) =>
        1 + (grupo.Grupos.Count == 0 ? 0 : grupo.Grupos.Max(Profundidad));
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
