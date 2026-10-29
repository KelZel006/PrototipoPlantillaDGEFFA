using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Fila del listado de consolidados. Los tipos y las cantidades se calculan de las propuestas, no se escriben.</summary>
/// <summary>Datos para crear un Documento Consolidado: la unidad a la que pertenece.</summary>
public sealed record SolicitudCrearConsolidado(string? UnidadId);

public sealed record ResumenConsolidado(
    string Id,
    string Numero,
    int Anio,
    EstadoConsolidado Estado,
    string UnidadId,
    string AnalistaId,
    DateTimeOffset FechaCreacion,
    DateTimeOffset FechaActualizacion,
    IReadOnlyList<string> TiposDocumento,
    int TotalPlantillas);

/// <summary>
/// Fila de «Tipos de documento incluidos»: cada tipo documental una sola vez, aunque tenga varias plantillas o ajustes.
/// </summary>
public sealed record ResumenTipo(string TipoDocumentoId, string TipoDocumento, string Operacion, int Plantillas, int Ajustes);

/// <summary>
/// Consolidado con sus propuestas, presentaciones, devoluciones y bitácora. <c>Ajustes</c> tiene los ajustes de cada
/// propuesta no retirada, por identificador de propuesta (D8).
/// </summary>
public sealed record DetalleConsolidado(
    DocumentoConsolidado Consolidado,
    IReadOnlyList<ResumenTipo> TiposIncluidos,
    IReadOnlyList<Propuesta> Propuestas,
    IReadOnlyDictionary<string, IReadOnlyList<Ajuste>> Ajustes,
    IReadOnlyList<Presentacion> Presentaciones,
    IReadOnlyList<Devolucion> Devoluciones,
    IReadOnlyList<Movimiento> Movimientos,
    IReadOnlyList<AlertaCampo> Alertas);

/// <summary>Creación, numeración y consulta de Documentos Consolidados, con las reglas de visibilidad por bandeja.</summary>
public sealed class ServicioConsolidados(
    IUnidadDeTrabajo unidad,
    IRepositorio<DocumentoConsolidado> consolidados,
    IRepositorio<Propuesta> propuestas,
    IRepositorio<Presentacion> presentaciones,
    IRepositorio<Devolucion> devoluciones,
    IRepositorio<Movimiento> movimientos,
    IRepositorio<TipoDocumento> tipos,
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<Campo> campos,
    IRepositorio<Unidad> unidades,
    ServicioAutorizacion autorizacion,
    ZonaHorariaInstitucional zona,
    TimeProvider reloj)
{
    /// <summary>
    /// Crea un consolidado EN REGISTRO con el siguiente número PLAN del año. La numeración se calcula dentro de la
    /// transacción, con la colección bloqueada: dos creaciones simultáneas nunca obtienen el mismo número. El documento es
    /// de la unidad indicada; sin indicarla, de la primera unidad activa (Exoneraciones).
    /// </summary>
    public async Task<DocumentoConsolidado> CrearAsync(string? unidadId = null, CancellationToken ct = default)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
        var activas = (await unidades.ListarAsync(ct)).Where(u => u.Activo).OrderBy(u => u.Orden).ToList();
        var unidadDocumento = string.IsNullOrWhiteSpace(unidadId) ? activas.FirstOrDefault() : activas.FirstOrDefault(u => u.Id == unidadId);
        if (unidadDocumento is null)
        {
            var resultado = new ResultadoValidacion();
            resultado.Agregar("unidadId", "La unidad no existe o está inactiva.");
            resultado.LanzarSiHayErrores();
        }
        var ahora = reloj.GetUtcNow();
        var anio = zona.Anio(ahora);

        return await unidad.EjecutarAsync(async tx =>
        {
            var existentes = await tx.ListarAsync<DocumentoConsolidado>(ct);
            var secuencia = existentes.Where(c => c.Anio == anio).Select(c => c.Secuencia).DefaultIfEmpty(0).Max() + 1;
            if (secuencia > NumeroPlan.SecuenciaMaxima)
                throw new ConflictoException($"Se alcanzó el máximo de {NumeroPlan.SecuenciaMaxima} documentos consolidados del año {anio}.");

            var consolidado = await tx.CrearAsync(new DocumentoConsolidado
            {
                Numero = NumeroPlan.Formatear(anio, secuencia),
                Anio = anio,
                Secuencia = secuencia,
                Estado = EstadoConsolidado.EnRegistro,
                UnidadId = unidadDocumento!.Id,
                AnalistaId = analista.Id,
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
            }, ct);
            await tx.CrearAsync(new Movimiento
            {
                ConsolidadoId = consolidado.Id,
                Tipo = TipoMovimiento.Creacion,
                Usuario = analista.Id,
                Fecha = ahora,
                EstadoDestino = EstadoConsolidado.EnRegistro,
                Detalle = $"Se creó el documento consolidado {consolidado.Numero} de {unidadDocumento!.Nombre}.",
            }, ct);
            await tx.CrearAsync(ServicioHistorial.Construir(
                new CambioHistorial(consolidados.Coleccion, consolidado.Id, AccionHistorial.Creacion, consolidado.Version, null, consolidado),
                analista.Id, ahora), ct);
            return consolidado;
        }, ct);
    }

    /// <summary>Consolidados que el usuario puede ver, del más reciente al más antiguo.</summary>
    public async Task<IReadOnlyList<ResumenConsolidado>> ListarAsync(CancellationToken ct = default)
    {
        var usuario = await autorizacion.UsuarioAsync(ct);
        var visibles = (await consolidados.ListarAsync(ct)).Where(c => PuedeVer(usuario, c)).ToList();
        var todasPropuestas = await propuestas.ListarAsync(ct);
        var nombresTipo = (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre, StringComparer.Ordinal);

        return visibles
            .OrderByDescending(c => c.Anio).ThenByDescending(c => c.Secuencia)
            .Select(c =>
            {
                var activas = todasPropuestas.Where(p => p.ConsolidadoId == c.Id && !p.Retirada).ToList();
                // Cada tipo documental una sola vez, aunque tenga varias propuestas o ajustes.
                var tiposIncluidos = activas.Select(p => p.TipoDocumentoId).Distinct(StringComparer.Ordinal)
                    .Select(id => nombresTipo.GetValueOrDefault(id, id)).Order(StringComparer.CurrentCulture).ToList();
                return new ResumenConsolidado(c.Id, c.Numero, c.Anio, c.Estado, c.UnidadId, c.AnalistaId, c.FechaCreacion,
                    c.FechaActualizacion, tiposIncluidos, activas.Count);
            })
            .ToList();
    }

    /// <summary>Detalle del consolidado. Si el usuario no puede verlo, se responde como si no existiera (404).</summary>
    public async Task<DetalleConsolidado> ObtenerAsync(string id, CancellationToken ct = default)
    {
        var usuario = await autorizacion.UsuarioAsync(ct);
        var consolidado = await consolidados.ObtenerAsync(id, ct);
        if (consolidado is null || !PuedeVer(usuario, consolidado))
            throw new EntidadNoEncontradaException(consolidados.Coleccion, id);

        var deConsolidado = (await propuestas.ListarAsync(ct)).Where(p => p.ConsolidadoId == id).OrderBy(p => p.FechaCreacion).ToList();
        var porVersion = (await versiones.ListarAsync(ct)).ToDictionary(v => v.Id, StringComparer.Ordinal);
        var activas = deConsolidado.Where(p => !p.Retirada).ToList();
        var ajustes = activas.ToDictionary(
            p => p.Id,
            p => ComparadorContenido.Ajustes(
                p.VersionOrigenId is { } origen && porVersion.TryGetValue(origen, out var v) ? v.Contenido : new ContenidoPlantilla(),
                p.ContenidoPropuesto),
            StringComparer.Ordinal);
        var nombresTipo = (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre, StringComparer.Ordinal);
        var resumen = activas
            .GroupBy(p => p.TipoDocumentoId, StringComparer.Ordinal)
            .Select(g =>
            {
                var operaciones = g.Select(p => p.Operacion).Distinct().Order().ToList();
                var operacion = operaciones.Count == 2 ? "Crear y Modificar" : operaciones[0] == OperacionPropuesta.Crear ? "Crear" : "Modificar";
                return new ResumenTipo(g.Key, nombresTipo.GetValueOrDefault(g.Key, g.Key), operacion, g.Count(), g.Sum(p => ajustes[p.Id].Count));
            })
            .OrderBy(r => r.TipoDocumento, StringComparer.CurrentCulture)
            .ToList();

        return new DetalleConsolidado(
            consolidado,
            resumen,
            deConsolidado,
            ajustes,
            (await presentaciones.ListarAsync(ct)).Where(p => p.ConsolidadoId == id).OrderBy(p => p.Numero).ToList(),
            (await devoluciones.ListarAsync(ct)).Where(d => d.ConsolidadoId == id).OrderBy(d => d.Fecha).ToList(),
            (await movimientos.ListarAsync(ct)).Where(m => m.ConsolidadoId == id).OrderBy(m => m.Fecha).ToList(),
            await AlertasAsync(usuario, activas, porVersion, ct));
    }

    /// <summary>
    /// Cambios en los campos vigilados (por ejemplo, Operador). Solo se le muestran al aprobador (Oficial Jurídico).
    /// </summary>
    private async Task<IReadOnlyList<AlertaCampo>> AlertasAsync(
        Usuario usuario, IReadOnlyList<Propuesta> activas, IReadOnlyDictionary<string, VersionPlantilla> porVersion, CancellationToken ct)
    {
        if (!usuario.Tiene(Rol.OficialJuridico))
            return [];
        var vigilados = (await campos.ListarAsync(ct)).Where(c => c.AlertarCambios).ToList();
        return activas
            .SelectMany(p => AlertasCampos.Detectar(p,
                p.VersionOrigenId is { } origen && porVersion.TryGetValue(origen, out var v) ? v.Contenido : new ContenidoPlantilla(), vigilados))
            .ToList();
    }

    /// <summary>
    /// Reglas de bandeja (decisiones D6 y D10):
    /// <list type="bullet">
    /// <item>Analista: solo los suyos, EN REGISTRO (incluidos los devueltos) o APROBADOS. Al enviarlo a verificación
    /// sale de su bandeja.</item>
    /// <item>Oficial Jurídico: los VERIFICADOS (su bandeja) y los APROBADOS.</item>
    /// <item>Administrador: todos, en consulta.</item>
    /// </list>
    /// </summary>
    public static bool PuedeVer(Usuario usuario, DocumentoConsolidado consolidado) =>
        usuario.Tiene(Rol.Administrador) ||
        (usuario.Tiene(Rol.Analista) && consolidado.AnalistaId == usuario.Id && consolidado.Estado != EstadoConsolidado.Verificado) ||
        (usuario.Tiene(Rol.OficialJuridico) && consolidado.Estado != EstadoConsolidado.EnRegistro);
}
