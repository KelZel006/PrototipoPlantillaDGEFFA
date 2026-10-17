using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>Fila del listado de consolidados. Los tipos y las cantidades se calculan de las propuestas, no se escriben.</summary>
public sealed record ResumenConsolidado(
    string Id,
    string Numero,
    int Anio,
    EstadoConsolidado Estado,
    string AnalistaId,
    DateTimeOffset FechaCreacion,
    DateTimeOffset FechaActualizacion,
    IReadOnlyList<string> TiposDocumento,
    int TotalPlantillas);

/// <summary>Consolidado con sus propuestas, presentaciones, devoluciones y bitácora.</summary>
public sealed record DetalleConsolidado(
    DocumentoConsolidado Consolidado,
    IReadOnlyList<Propuesta> Propuestas,
    IReadOnlyList<Presentacion> Presentaciones,
    IReadOnlyList<Devolucion> Devoluciones,
    IReadOnlyList<Movimiento> Movimientos);

/// <summary>Creación, numeración y consulta de Documentos Consolidados, con las reglas de visibilidad por bandeja.</summary>
public sealed class ServicioConsolidados(
    IUnidadDeTrabajo unidad,
    IRepositorio<DocumentoConsolidado> consolidados,
    IRepositorio<Propuesta> propuestas,
    IRepositorio<Presentacion> presentaciones,
    IRepositorio<Devolucion> devoluciones,
    IRepositorio<Movimiento> movimientos,
    IRepositorio<TipoDocumento> tipos,
    ServicioAutorizacion autorizacion,
    ZonaHorariaInstitucional zona,
    TimeProvider reloj)
{
    /// <summary>
    /// Crea un consolidado EN REGISTRO con el siguiente número PLAN del año. La numeración se calcula dentro de la
    /// transacción, con la colección bloqueada: dos creaciones simultáneas nunca obtienen el mismo número.
    /// </summary>
    public async Task<DocumentoConsolidado> CrearAsync(CancellationToken ct = default)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
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
                Detalle = $"Se creó el documento consolidado {consolidado.Numero}.",
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
                return new ResumenConsolidado(c.Id, c.Numero, c.Anio, c.Estado, c.AnalistaId, c.FechaCreacion,
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

        return new DetalleConsolidado(
            consolidado,
            (await propuestas.ListarAsync(ct)).Where(p => p.ConsolidadoId == id).OrderBy(p => p.FechaCreacion).ToList(),
            (await presentaciones.ListarAsync(ct)).Where(p => p.ConsolidadoId == id).OrderBy(p => p.Numero).ToList(),
            (await devoluciones.ListarAsync(ct)).Where(d => d.ConsolidadoId == id).OrderBy(d => d.Fecha).ToList(),
            (await movimientos.ListarAsync(ct)).Where(m => m.ConsolidadoId == id).OrderBy(m => m.Fecha).ToList());
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
