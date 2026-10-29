using EditorResoluciones.Dominio.Comun;
using EditorResoluciones.Dominio.Modelos;
using EditorResoluciones.Dominio.Repositorios;
using EditorResoluciones.Dominio.Seguridad;
using EditorResoluciones.Dominio.Servicios;
using EditorResoluciones.Dominio.Validacion;

namespace EditorResoluciones.Dominio.Consolidados;

/// <summary>
/// CREAR una plantilla nueva. Con <c>plantillaOrigenId</c>, empieza como copia de la versión vigente de esa plantilla
/// (clonar); sin él, empieza vacía.
/// </summary>
public sealed record SolicitudCrearPlantilla(
    string RubroId, string SubRubroId, string TipoDocumentoId, string ClaseDocumentoId, string? Nombre, string? PlantillaOrigenId = null);

public sealed record SolicitudModificarPlantilla(string PlantillaId);

public sealed record SolicitudRetiro(string Motivo);

/// <summary>Posición enviada por el cliente: el párrafo y la versión que el cliente tiene cargada.</summary>
public sealed record ElementoOrden(string Id, int Version);

/// <summary>Nuevo orden de un bloque (código del bloque, por ejemplo <c>CONSIDERANDO</c>).</summary>
public sealed record SolicitudReordenPropuesta(string Bloque, List<ElementoOrden> Elementos);

/// <summary>Párrafo de una propuesta tal como lo usa el editor (código del bloque y orden dentro de él).</summary>
public sealed record ParrafoPropuesta(
    string Id,
    int Version,
    string PropuestaId,
    string Bloque,
    int Orden,
    NodoContenido Contenido,
    bool Activo,
    bool MostrarSoloSiCumple,
    GrupoCondiciones? Condicion,
    List<string> CamposUsados,
    bool UnirConAnterior = false,
    bool EsTitulo = false);

/// <summary>
/// Datos de un párrafo que envía el editor al crear o actualizar. La versión es la que el editor tiene cargada (se
/// ignora al crear); el identificador va en la ruta. Si no se indica <c>activo</c>, el párrafo queda activo.
/// <c>unirConAnterior</c> indica que el párrafo continúa el anterior del bloque; <c>esTitulo</c>, que es un título
/// (centrado y en negrita). Un título no se une con el anterior.
/// </summary>
public sealed record DatosParrafo(
    string Bloque,
    int Orden,
    NodoContenido? Contenido,
    bool MostrarSoloSiCumple,
    GrupoCondiciones? Condicion,
    int Version,
    bool Activo = true,
    bool UnirConAnterior = false,
    bool EsTitulo = false);

/// <summary>Datos de diseño que el editor puede cambiar (el logo se carga aparte).</summary>
public sealed record DatosDiseno(AlineacionLogo AlineacionLogo, string? PiePagina, double EspacioFirmasCm);

/// <summary>Plantilla en edición dentro de una propuesta: su identidad, su diseño y si todavía se puede editar.</summary>
public sealed record PlantillaEdicion(
    string Id,
    int Version,
    string Nombre,
    string RubroId,
    string SubRubroId,
    string TipoDocumentoId,
    string ClaseDocumentoId,
    string? LogoId,
    AlineacionLogo AlineacionLogo,
    string? PiePagina,
    double EspacioFirmasCm,
    DateTimeOffset FechaModificacion,
    OperacionPropuesta Operacion,
    string ConsolidadoId,
    string ConsolidadoNumero,
    EstadoConsolidado EstadoConsolidado,
    bool Editable,
    bool Retirada,
    string? DerivadaDePropuestaId,
    string? PlantillaId,
    IReadOnlyList<BloqueConfigurado> Bloques,
    IReadOnlyList<string> ObligatoriosVacios);

/// <summary>Plantilla vigente que se puede elegir para MODIFICAR. Si ya está en un consolidado abierto, se indica cuál (D4).</summary>
public sealed record PlantillaVigente(
    string PlantillaId,
    string Nombre,
    string UnidadId,
    string RubroId,
    string SubRubroId,
    string TipoDocumentoId,
    string TipoDocumento,
    string ClaseDocumentoId,
    string VersionId,
    int NumeroVersion,
    DateTimeOffset FechaPublicacion,
    string? EnConsolidado);

/// <summary>
/// Propuestas de un Documento Consolidado: agregar (CREAR o MODIFICAR), editar su contenido y retirarlas. Nada de esto
/// toca las versiones vigentes (regla 6). Reglas:
/// <list type="bullet">
/// <item>Solo el analista dueño edita, y solo mientras el consolidado está EN REGISTRO.</item>
/// <item>Una plantilla (o una identidad, en CREAR) no puede estar en dos consolidados abiertos (D4).</item>
/// <item>La Resolución lleva consigo su Certificación como propuesta derivada, que se actualiza con cada cambio de la
/// Resolución y también se puede editar (D3).</item>
/// </list>
/// </summary>
public sealed class ServicioPropuestas(
    IUnidadDeTrabajo unidad,
    IRepositorio<Propuesta> propuestas,
    IRepositorio<DocumentoConsolidado> consolidados,
    IRepositorio<VersionPlantilla> versiones,
    IRepositorio<Plantilla> plantillas,
    IRepositorio<Rubro> rubros,
    IRepositorio<SubRubro> subRubros,
    IRepositorio<TipoDocumento> tipos,
    IRepositorio<ClaseDocumento> clases,
    IRepositorio<Campo> campos,
    CatalogoBloques catalogoBloques,
    ServicioAutorizacion autorizacion,
    TimeProvider reloj)
{
    // ---------- Agregar y retirar ----------

    public async Task<IReadOnlyList<Propuesta>> AgregarCrearAsync(string consolidadoId, SolicitudCrearPlantilla solicitud, CancellationToken ct = default)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
        var catalogo = await CatalogoAsync(ct);
        var resultado = new ResultadoValidacion();
        if (!catalogo.Rubros.TryGetValue(solicitud.RubroId ?? "", out var rubro) || !rubro.Activo)
            resultado.Agregar("rubroId", "El rubro no existe o está inactivo.");
        if (!catalogo.SubRubros.TryGetValue(solicitud.SubRubroId ?? "", out var subRubro) || !subRubro.Activo || subRubro.RubroId != solicitud.RubroId)
            resultado.Agregar("subRubroId", "El sub rubro no existe, está inactivo o no pertenece al rubro.");
        if (!catalogo.Tipos.TryGetValue(solicitud.TipoDocumentoId ?? "", out var tipo) || !tipo.Activo)
            resultado.Agregar("tipoDocumentoId", "El tipo de documento no existe o está inactivo.");
        if (!catalogo.Clases.TryGetValue(solicitud.ClaseDocumentoId ?? "", out var clase) || !clase.Activo)
            resultado.Agregar("claseDocumentoId", "El documento (Original, Modificación o Aclaratoria) no existe o está inactivo.");
        if (solicitud.Nombre is { Length: > 200 })
            resultado.Agregar("nombre", "El nombre admite hasta 200 caracteres.");
        resultado.LanzarSiHayErrores();

        var ahora = reloj.GetUtcNow();
        var bloquesTipo = await catalogoBloques.DeTipoAsync(solicitud.TipoDocumentoId!, ct);
        return await unidad.EjecutarAsync(async tx =>
        {
            var consolidado = await EditableAsync(tx, consolidadoId, analista, ct);
            var identidad = new Identidad(consolidado.UnidadId, solicitud.RubroId!, solicitud.SubRubroId!, solicitud.TipoDocumentoId!, solicitud.ClaseDocumentoId!);
            await ExigirIdentidadLibreAsync(tx, identidad, ct);

            // Clonar: la nueva plantilla parte del contenido vigente de otra, que no cambia.
            var contenido = ConstructorContenido.Vacio(bloquesTipo);
            string? nota = null;
            if (!string.IsNullOrWhiteSpace(solicitud.PlantillaOrigenId))
            {
                var origen = (await tx.ListarAsync<Plantilla>(ct)).FirstOrDefault(p => p.Id == solicitud.PlantillaOrigenId)
                    ?? throw new EntidadNoEncontradaException(plantillas.Coleccion, solicitud.PlantillaOrigenId);
                var vigente = (await tx.ListarAsync<VersionPlantilla>(ct)).FirstOrDefault(v => v.PlantillaId == origen.Id && v.Vigencia == Vigencia.Vigente)
                    ?? throw new ConflictoException($"«{origen.Nombre}» no tiene una versión vigente para clonar.");
                contenido = ConstructorContenido.CopiaDe(vigente.Contenido, bloquesTipo, NuevoIdParrafo);
                nota = $"copia de «{origen.Nombre}», versión {vigente.NumeroVersion}";
            }

            var principal = await tx.CrearAsync(new Propuesta
            {
                ConsolidadoId = consolidado.Id,
                Operacion = OperacionPropuesta.Crear,
                UnidadId = identidad.UnidadId,
                RubroId = identidad.RubroId,
                SubRubroId = identidad.SubRubroId,
                TipoDocumentoId = identidad.TipoDocumentoId,
                ClaseDocumentoId = identidad.ClaseDocumentoId,
                Nombre = string.IsNullOrWhiteSpace(solicitud.Nombre) ? NombrePredeterminado(tipo!, clase!, subRubro!) : solicitud.Nombre.Trim(),
                ContenidoPropuesto = contenido,
                FechaCreacion = ahora,
                FechaModificacion = ahora,
            }, ct);
            return await DespuesDeAgregarAsync(tx, consolidado, principal, catalogo, analista, ahora, ct, nota);
        }, ct);
    }

    public async Task<IReadOnlyList<Propuesta>> AgregarModificarAsync(string consolidadoId, SolicitudModificarPlantilla solicitud, CancellationToken ct = default)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
        var catalogo = await CatalogoAsync(ct);
        var ahora = reloj.GetUtcNow();
        return await unidad.EjecutarAsync(async tx =>
        {
            var consolidado = await EditableAsync(tx, consolidadoId, analista, ct);
            var plantilla = (await tx.ListarAsync<Plantilla>(ct)).FirstOrDefault(p => p.Id == solicitud.PlantillaId)
                ?? throw new EntidadNoEncontradaException(plantillas.Coleccion, solicitud.PlantillaId);
            var principal = await CrearModificacionAsync(tx, consolidado, plantilla, ahora, ct);
            return await DespuesDeAgregarAsync(tx, consolidado, principal, catalogo, analista, ahora, ct);
        }, ct);
    }

    /// <summary>
    /// Retira la propuesta del borrador (eliminación lógica, queda en la bitácora). Retirar la Resolución retira también
    /// su Certificación; la Certificación sola no se retira, porque van juntas (D3).
    /// </summary>
    public async Task<IReadOnlyList<Propuesta>> RetirarAsync(string propuestaId, SolicitudRetiro solicitud, CancellationToken ct = default)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
        if (string.IsNullOrWhiteSpace(solicitud.Motivo) || solicitud.Motivo.Trim().Length < 5 || solicitud.Motivo.Length > 500)
        {
            var resultado = new ResultadoValidacion();
            resultado.Agregar("motivo", "Indique el motivo del retiro (de 5 a 500 caracteres).");
            resultado.LanzarSiHayErrores();
        }
        var ahora = reloj.GetUtcNow();
        return await unidad.EjecutarAsync(async tx =>
        {
            var todas = await tx.ListarAsync<Propuesta>(ct);
            var propuesta = todas.FirstOrDefault(p => p.Id == propuestaId) ?? throw new EntidadNoEncontradaException(propuestas.Coleccion, propuestaId);
            var consolidado = await EditableAsync(tx, propuesta.ConsolidadoId, analista, ct);
            if (propuesta.Retirada)
                throw new ConflictoException("La propuesta ya fue retirada.");
            if (propuesta.DerivadaDePropuestaId is { } principalId && todas.Any(p => p.Id == principalId && !p.Retirada))
                throw new ConflictoException("Esta Certificación va junto con su Resolución: para retirarla, retire la Resolución.");

            var retiradas = new List<Propuesta>();
            foreach (var p in todas.Where(p => (p.Id == propuesta.Id || p.DerivadaDePropuestaId == propuesta.Id) && !p.Retirada).ToList())
            {
                p.Retirada = true;
                p.MotivoRetiro = solicitud.Motivo.Trim();
                p.FechaModificacion = ahora;
                retiradas.Add(await tx.ActualizarAsync(p, ct));
            }
            await TocarAsync(tx, consolidado, ahora, ct);
            await tx.CrearAsync(new Movimiento
            {
                ConsolidadoId = consolidado.Id,
                Tipo = TipoMovimiento.PropuestaRetirada,
                Usuario = analista.Id,
                Fecha = ahora,
                Detalle = $"Se retiró: {string.Join("; ", retiradas.Select(p => p.Nombre))}. Motivo: {solicitud.Motivo.Trim()}",
            }, ct);
            return (IReadOnlyList<Propuesta>)retiradas;
        }, ct);
    }

    // ---------- Consulta ----------

    public async Task<Propuesta> ObtenerAsync(string propuestaId, CancellationToken ct = default)
    {
        var (propuesta, _) = await VisibleAsync(propuestaId, ct);
        return propuesta;
    }

    public async Task<PlantillaEdicion> EdicionAsync(string propuestaId, CancellationToken ct = default)
    {
        var (propuesta, consolidado) = await VisibleAsync(propuestaId, ct);
        var usuario = await autorizacion.UsuarioAsync(ct);
        var bloques = await catalogoBloques.DeContenidoAsync(propuesta.TipoDocumentoId, propuesta.ContenidoPropuesto, ct);
        return Edicion(propuesta, consolidado, usuario, bloques);
    }

    /// <summary>Párrafos de la propuesta (de un bloque, o de todos en el orden del tipo documental).</summary>
    public async Task<IReadOnlyList<ParrafoPropuesta>> ParrafosAsync(string propuestaId, string? bloque, CancellationToken ct = default)
    {
        var (propuesta, _) = await VisibleAsync(propuestaId, ct);
        var configurados = await catalogoBloques.DeContenidoAsync(propuesta.TipoDocumentoId, propuesta.ContenidoPropuesto, ct);
        return Parrafos(propuesta, bloque, configurados);
    }

    /// <summary>Ajustes de la propuesta respecto de la versión original (en CREAR, respecto de un documento vacío).</summary>
    public async Task<IReadOnlyList<Ajuste>> AjustesAsync(string propuestaId, CancellationToken ct = default)
    {
        var (propuesta, _) = await VisibleAsync(propuestaId, ct);
        return await AjustesAsync(propuesta, ct);
    }

    public async Task<IReadOnlyList<Ajuste>> AjustesAsync(Propuesta propuesta, CancellationToken ct = default)
    {
        return ComparadorContenido.Ajustes(await OriginalAsync(propuesta, ct), propuesta.ContenidoPropuesto);
    }

    /// <summary>Contenido original de la propuesta: la versión vigente que modifica o, en CREAR, un documento vacío.</summary>
    public async Task<ContenidoPlantilla> OriginalAsync(Propuesta propuesta, CancellationToken ct = default) =>
        propuesta.VersionOrigenId is { } origen && await versiones.ObtenerAsync(origen, ct) is { } version
            ? version.Contenido
            : new ContenidoPlantilla();

    /// <summary>
    /// Comparación lado a lado con la versión original, con la diferencia de texto palabra por palabra. Es la revisión
    /// del Oficial Jurídico: solo él la consulta y solo con el documento VERIFICADO.
    /// </summary>
    public async Task<ComparacionPropuesta> ComparacionAsync(string propuestaId, CancellationToken ct = default)
    {
        await autorizacion.ExigirAsync(Rol.OficialJuridico, ct);
        var (propuesta, consolidado) = await VisibleAsync(propuestaId, ct);
        if (consolidado.Estado != EstadoConsolidado.Verificado)
            throw new ConflictoException("Las diferencias se revisan cuando el documento está VERIFICADO.");
        var original = await OriginalAsync(propuesta, ct);
        var configurados = await catalogoBloques.DeContenidoAsync(propuesta.TipoDocumentoId, propuesta.ContenidoPropuesto, ct);
        var catalogo = (await campos.ListarAsync(ct)).ToDictionary(c => c.Id, StringComparer.Ordinal);
        return ComparadorContenido.Comparar(propuesta.Id, propuesta.Operacion, original, propuesta.ContenidoPropuesto, configurados, catalogo);
    }

    /// <summary>Catálogo de plantillas vigentes para la operación MODIFICAR.</summary>
    public async Task<IReadOnlyList<PlantillaVigente>> VigentesAsync(CancellationToken ct = default)
    {
        await autorizacion.UsuarioAsync(ct);
        var nombresTipo = (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre, StringComparer.Ordinal);
        var vigentes = (await versiones.ListarAsync(ct)).Where(v => v.Vigencia == Vigencia.Vigente)
            .ToDictionary(v => v.PlantillaId, StringComparer.Ordinal);
        var abiertas = await AbiertasAsync(await propuestas.ListarAsync(ct), await consolidados.ListarAsync(ct));
        return (await plantillas.ListarAsync(ct))
            .Where(p => vigentes.ContainsKey(p.Id))
            .Select(p =>
            {
                var v = vigentes[p.Id];
                var bloqueo = abiertas.FirstOrDefault(a => a.Propuesta.PlantillaId == p.Id);
                return new PlantillaVigente(p.Id, p.Nombre, p.UnidadId, p.RubroId, p.SubRubroId, p.TipoDocumentoId,
                    nombresTipo.GetValueOrDefault(p.TipoDocumentoId, p.TipoDocumentoId), p.ClaseDocumentoId, v.Id, v.NumeroVersion,
                    v.FechaPublicacion, bloqueo.Consolidado?.Numero);
            })
            .OrderBy(p => p.TipoDocumento, StringComparer.CurrentCulture).ThenBy(p => p.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    // ---------- Edición del contenido ----------

    public async Task<ParrafoPropuesta> CrearParrafoAsync(string propuestaId, DatosParrafo datos, CancellationToken ct = default)
    {
        // Primero se comprueba que la propuesta exista y sea visible: una inexistente responde 404, no 400.
        var (propuesta, _) = await VisibleAsync(propuestaId, ct);
        var normalizado = await ValidarAsync(propuesta, datos, ct);
        return await EditarAsync(propuestaId, edicion => edicion.Crear(datos.Bloque, normalizado, datos.Orden), ct);
    }

    public async Task<ParrafoPropuesta> ActualizarParrafoAsync(string propuestaId, string parrafoId, DatosParrafo datos, CancellationToken ct = default)
    {
        // Primero se comprueba que la propuesta exista y sea visible: una inexistente responde 404, no 400.
        var (propuesta, _) = await VisibleAsync(propuestaId, ct);
        var normalizado = await ValidarAsync(propuesta, datos, ct);
        return await EditarAsync(propuestaId, edicion => edicion.Actualizar(parrafoId, datos.Version, datos.Bloque, normalizado), ct);
    }

    public Task<bool> EliminarParrafoAsync(string propuestaId, string parrafoId, int? version, CancellationToken ct = default) =>
        EditarAsync(propuestaId, edicion => edicion.Eliminar(parrafoId, version), ct);

    public Task<ParrafoPropuesta> DuplicarParrafoAsync(string propuestaId, string parrafoId, CancellationToken ct = default) =>
        EditarAsync(propuestaId, edicion => edicion.Duplicar(parrafoId), ct);

    public Task<IReadOnlyList<ParrafoPropuesta>> ReordenarAsync(string propuestaId, SolicitudReordenPropuesta solicitud, CancellationToken ct = default) =>
        EditarAsync(propuestaId, edicion => edicion.Reordenar(solicitud.Bloque, solicitud.Elementos), ct);

    public async Task<PlantillaEdicion> ActualizarDisenoAsync(string propuestaId, DatosDiseno datos, CancellationToken ct = default)
    {
        var resultado = new ResultadoValidacion();
        if (!Enum.IsDefined(datos.AlineacionLogo))
            resultado.Agregar("alineacionLogo", "La alineación del logo no es válida.");
        if (datos.PiePagina is { Length: > 500 })
            resultado.Agregar("piePagina", "El pie de página admite hasta 500 caracteres.");
        if (datos.EspacioFirmasCm is < 0 or > 10 || double.IsNaN(datos.EspacioFirmasCm))
            resultado.Agregar("espacioFirmasCm", "El espacio entre firmas debe estar entre 0 y 10 cm.");
        resultado.LanzarSiHayErrores();

        await EditarAsync(propuestaId, edicion => edicion.CambiarDiseno(d =>
        {
            d.AlineacionLogo = datos.AlineacionLogo;
            d.PiePagina = string.IsNullOrWhiteSpace(datos.PiePagina) ? null : datos.PiePagina;
            d.EspacioFirmasCm = datos.EspacioFirmasCm;
        }), ct);
        return await EdicionAsync(propuestaId, ct);
    }

    /// <summary>Asigna (o quita, con null) el logo de la propuesta. Devuelve el logo que tenía antes.</summary>
    public async Task<string?> CambiarLogoAsync(string propuestaId, string? archivoId, CancellationToken ct = default)
    {
        string? anterior = null;
        await EditarAsync(propuestaId, edicion => edicion.CambiarDiseno(d =>
        {
            anterior = d.LogoId;
            d.LogoId = archivoId;
        }), ct);
        return anterior;
    }

    // ---------- Internos ----------

    /// <summary>Identidad funcional: la unidad y la combinación de Rubro, Sub Rubro, Tipo Documento y Documento.</summary>
    private sealed record Identidad(string UnidadId, string RubroId, string SubRubroId, string TipoDocumentoId, string ClaseDocumentoId)
    {
        public static Identidad De(Plantilla p) => new(p.UnidadId, p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId);
        public static Identidad De(Propuesta p) => new(p.UnidadId, p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId);
    }

    private sealed record Catalogo(
        Dictionary<string, Rubro> Rubros,
        Dictionary<string, SubRubro> SubRubros,
        Dictionary<string, TipoDocumento> Tipos,
        Dictionary<string, ClaseDocumento> Clases);

    private async Task<Catalogo> CatalogoAsync(CancellationToken ct) => new(
        (await rubros.ListarAsync(ct)).ToDictionary(r => r.Id, StringComparer.Ordinal),
        (await subRubros.ListarAsync(ct)).ToDictionary(s => s.Id, StringComparer.Ordinal),
        (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, StringComparer.Ordinal),
        (await clases.ListarAsync(ct)).ToDictionary(c => c.Id, StringComparer.Ordinal));

    private static string NombrePredeterminado(TipoDocumento tipo, ClaseDocumento clase, SubRubro subRubro)
    {
        var nombre = $"{tipo.Nombre} ({clase.Nombre}) – {subRubro.Nombre}";
        return nombre.Length > 200 ? nombre[..200] : nombre;
    }

    /// <summary>Consolidado que el analista puede modificar: es suyo y está EN REGISTRO.</summary>
    private static async Task<DocumentoConsolidado> EditableAsync(ITransaccion tx, string consolidadoId, Usuario analista, CancellationToken ct)
    {
        var consolidado = (await tx.ListarAsync<DocumentoConsolidado>(ct)).FirstOrDefault(c => c.Id == consolidadoId);
        // Un consolidado ajeno responde como inexistente (D10).
        if (consolidado is null || !ServicioConsolidados.PuedeVer(analista, consolidado) || consolidado.AnalistaId != analista.Id)
            throw new EntidadNoEncontradaException("consolidados", consolidadoId);
        if (consolidado.Estado != EstadoConsolidado.EnRegistro)
            throw new ConflictoException($"El documento {consolidado.Numero} no está EN REGISTRO: no se puede modificar.");
        return consolidado;
    }

    /// <summary>Propuestas no retiradas de consolidados que todavía no se aprobaron.</summary>
    private static Task<List<(Propuesta Propuesta, DocumentoConsolidado Consolidado)>> AbiertasAsync(
        IReadOnlyList<Propuesta> todas, IReadOnlyList<DocumentoConsolidado> todos)
    {
        var porId = todos.ToDictionary(c => c.Id, StringComparer.Ordinal);
        return Task.FromResult(todas
            .Where(p => !p.Retirada && porId.TryGetValue(p.ConsolidadoId, out var c) && c.Estado != EstadoConsolidado.Aprobado)
            .Select(p => (p, porId[p.ConsolidadoId]))
            .ToList());
    }

    private static async Task<List<(Propuesta Propuesta, DocumentoConsolidado Consolidado)>> AbiertasAsync(ITransaccion tx, CancellationToken ct) =>
        await AbiertasAsync(await tx.ListarAsync<Propuesta>(ct), await tx.ListarAsync<DocumentoConsolidado>(ct));

    /// <summary>CREAR: no existe una plantilla con esa identidad ni otra propuesta abierta que la cree (D4).</summary>
    private static async Task ExigirIdentidadLibreAsync(ITransaccion tx, Identidad identidad, CancellationToken ct)
    {
        if ((await tx.ListarAsync<Plantilla>(ct)).FirstOrDefault(p => Identidad.De(p) == identidad) is { } existente)
            throw new ConflictoException($"Ya existe la plantilla «{existente.Nombre}» para esa combinación. Use MODIFICAR.");
        if ((await AbiertasAsync(tx, ct)).FirstOrDefault(a => Identidad.De(a.Propuesta) == identidad) is { Consolidado: { } otro })
            throw new ConflictoException($"Esa plantilla ya se está creando en el documento {otro.Numero}.");
    }

    private async Task<Propuesta> CrearModificacionAsync(ITransaccion tx, DocumentoConsolidado consolidado, Plantilla plantilla, DateTimeOffset ahora, CancellationToken ct)
    {
        if (plantilla.UnidadId != consolidado.UnidadId)
            throw new ConflictoException($"«{plantilla.Nombre}» es de otra unidad: un documento solo contiene plantillas de su unidad.");
        if ((await AbiertasAsync(tx, ct)).FirstOrDefault(a => a.Propuesta.PlantillaId == plantilla.Id) is { Consolidado: { } otro })
        {
            throw new ConflictoException(otro.Id == consolidado.Id
                ? $"«{plantilla.Nombre}» ya está en este documento: todos sus ajustes van en la misma propuesta."
                : $"«{plantilla.Nombre}» ya está en el documento {otro.Numero}. Una plantilla no puede estar en dos documentos abiertos.");
        }
        var vigente = (await tx.ListarAsync<VersionPlantilla>(ct)).FirstOrDefault(v => v.PlantillaId == plantilla.Id && v.Vigencia == Vigencia.Vigente)
            ?? throw new ConflictoException($"«{plantilla.Nombre}» no tiene una versión vigente para modificar.");

        var contenido = FusionDerivada.Clonar(vigente.Contenido);
        foreach (var parrafo in contenido.Bloques.SelectMany(b => b.Parrafos))
            parrafo.Version = 1;
        // Si el formato del tipo documental cambió (por ejemplo, el Dictamen), el texto pasa a sus bloques nuevos.
        ConstructorContenido.AdaptarABloques(contenido, await catalogoBloques.DeTipoAsync(plantilla.TipoDocumentoId, ct));
        return await tx.CrearAsync(new Propuesta
        {
            ConsolidadoId = consolidado.Id,
            Operacion = OperacionPropuesta.Modificar,
            UnidadId = plantilla.UnidadId,
            RubroId = plantilla.RubroId,
            SubRubroId = plantilla.SubRubroId,
            TipoDocumentoId = plantilla.TipoDocumentoId,
            ClaseDocumentoId = plantilla.ClaseDocumentoId,
            Nombre = plantilla.Nombre,
            PlantillaId = plantilla.Id,
            VersionOrigenId = vigente.Id,
            HuellaOrigen = vigente.Huella,
            ContenidoPropuesto = contenido,
            FechaCreacion = ahora,
            FechaModificacion = ahora,
        }, ct);
    }

    /// <summary>
    /// Después de agregar una propuesta: la liga con su Resolución si es una Certificación, crea o liga la Certificación
    /// si es una Resolución (D3), y deja la bitácora y el historial. La nota (por ejemplo, de qué plantilla se clonó) va
    /// en la bitácora junto a la propuesta principal.
    /// </summary>
    private async Task<IReadOnlyList<Propuesta>> DespuesDeAgregarAsync(
        ITransaccion tx,
        DocumentoConsolidado consolidado,
        Propuesta principal,
        Catalogo catalogo,
        Usuario analista,
        DateTimeOffset ahora,
        CancellationToken ct,
        string? nota = null)
    {
        var agregadas = new List<Propuesta> { principal };
        var delConsolidado = (await tx.ListarAsync<Propuesta>(ct)).Where(p => p.ConsolidadoId == consolidado.Id && !p.Retirada).ToList();
        var tipo = catalogo.Tipos[principal.TipoDocumentoId];

        // Es una Certificación y su Resolución ya está en el consolidado: se ligan.
        if (tipo.CopiaDeTipoDocumentoId is { } tipoOrigen &&
            delConsolidado.FirstOrDefault(p => p.TipoDocumentoId == tipoOrigen && MismaCombinacion(p, principal)) is { } resolucion)
        {
            principal.DerivadaDePropuestaId = resolucion.Id;
            await tx.ActualizarAsync(principal, ct);
        }

        // Es una Resolución: su Certificación va con ella.
        foreach (var tipoCopia in catalogo.Tipos.Values.Where(t => t.CopiaDeTipoDocumentoId == principal.TipoDocumentoId && t.Activo))
        {
            var identidad = new Identidad(principal.UnidadId, principal.RubroId, principal.SubRubroId, tipoCopia.Id, principal.ClaseDocumentoId);
            if (delConsolidado.FirstOrDefault(p => p.Id != principal.Id && Identidad.De(p) == identidad) is { } yaIncluida)
            {
                yaIncluida.DerivadaDePropuestaId = principal.Id;
                agregadas.Add(await tx.ActualizarAsync(yaIncluida, ct));
                continue;
            }

            Propuesta derivada;
            if ((await tx.ListarAsync<Plantilla>(ct)).FirstOrDefault(p => Identidad.De(p) == identidad) is { } existente)
            {
                derivada = await CrearModificacionAsync(tx, consolidado, existente, ahora, ct);
            }
            else
            {
                await ExigirIdentidadLibreAsync(tx, identidad, ct);
                var palabra = new CambioPalabra(tipo.Nombre, tipoCopia.Nombre);
                derivada = await tx.CrearAsync(new Propuesta
                {
                    ConsolidadoId = consolidado.Id,
                    Operacion = OperacionPropuesta.Crear,
                    UnidadId = identidad.UnidadId,
                    RubroId = identidad.RubroId,
                    SubRubroId = identidad.SubRubroId,
                    TipoDocumentoId = identidad.TipoDocumentoId,
                    ClaseDocumentoId = identidad.ClaseDocumentoId,
                    Nombre = NombrePredeterminado(tipoCopia, catalogo.Clases[identidad.ClaseDocumentoId], catalogo.SubRubros[identidad.SubRubroId]),
                    ContenidoPropuesto = FusionDerivada.CopiaInicial(principal.ContenidoPropuesto, palabra, NuevoIdParrafo),
                    FechaCreacion = ahora,
                    FechaModificacion = ahora,
                }, ct);
            }
            derivada.DerivadaDePropuestaId = principal.Id;
            agregadas.Add(await tx.ActualizarAsync(derivada, ct));
        }

        await TocarAsync(tx, consolidado, ahora, ct);
        await tx.CrearAsync(new Movimiento
        {
            ConsolidadoId = consolidado.Id,
            Tipo = TipoMovimiento.PropuestaAgregada,
            Usuario = analista.Id,
            Fecha = ahora,
            Detalle = string.Join("; ", agregadas.Select(p =>
                $"{(p.Operacion == OperacionPropuesta.Crear ? "Crear" : "Modificar")}: {p.Nombre}" +
                (p == principal && nota is not null ? $" ({nota})" : "") +
                (p.DerivadaDePropuestaId is null ? "" : " (acompaña a la Resolución)"))),
        }, ct);
        foreach (var p in agregadas)
        {
            await tx.CrearAsync(ServicioHistorial.Construir(
                new CambioHistorial(propuestas.Coleccion, p.Id, AccionHistorial.Creacion, p.Version, null, p), analista.Id, ahora), ct);
        }
        return agregadas;
    }

    private static bool MismaCombinacion(Propuesta a, Propuesta b) =>
        a.UnidadId == b.UnidadId && a.RubroId == b.RubroId && a.SubRubroId == b.SubRubroId && a.ClaseDocumentoId == b.ClaseDocumentoId;

    private static async Task TocarAsync(ITransaccion tx, DocumentoConsolidado consolidado, DateTimeOffset ahora, CancellationToken ct)
    {
        consolidado.FechaActualizacion = ahora;
        await tx.ActualizarAsync(consolidado, ct);
    }

    private async Task<(Propuesta, DocumentoConsolidado)> VisibleAsync(string propuestaId, CancellationToken ct)
    {
        var usuario = await autorizacion.UsuarioAsync(ct);
        var propuesta = await propuestas.ObtenerAsync(propuestaId, ct);
        var consolidado = propuesta is null ? null : await consolidados.ObtenerAsync(propuesta.ConsolidadoId, ct);
        if (propuesta is null || consolidado is null || !ServicioConsolidados.PuedeVer(usuario, consolidado))
            throw new EntidadNoEncontradaException(propuestas.Coleccion, propuestaId);
        return (propuesta, consolidado);
    }

    private static PlantillaEdicion Edicion(Propuesta p, DocumentoConsolidado c, Usuario usuario, IReadOnlyList<BloqueConfigurado> bloques) => new(
        p.Id, p.Version, p.Nombre, p.RubroId, p.SubRubroId, p.TipoDocumentoId, p.ClaseDocumentoId,
        p.ContenidoPropuesto.Diseno.LogoId, p.ContenidoPropuesto.Diseno.AlineacionLogo, p.ContenidoPropuesto.Diseno.PiePagina,
        p.ContenidoPropuesto.Diseno.EspacioFirmasCm, p.FechaModificacion, p.Operacion, c.Id, c.Numero, c.Estado,
        Editable: c.Estado == EstadoConsolidado.EnRegistro && c.AnalistaId == usuario.Id && usuario.Tiene(Rol.Analista) && !p.Retirada,
        p.Retirada, p.DerivadaDePropuestaId, p.PlantillaId, bloques,
        CatalogoBloques.ObligatoriosVacios(bloques, p.ContenidoPropuesto));

    private static List<ParrafoPropuesta> Parrafos(Propuesta propuesta, string? bloque, IReadOnlyList<BloqueConfigurado> configurados) =>
        propuesta.ContenidoPropuesto.Bloques
            .Where(b => bloque is null || b.Codigo == bloque)
            .OrderBy(b => configurados.FirstOrDefault(c => c.Codigo == b.Codigo)?.Orden ?? int.MaxValue)
            .SelectMany(b => b.Parrafos.OrderBy(p => p.Orden).Select(p => Dto(propuesta.Id, b.Codigo, p)))
            .ToList();

    private static ParrafoPropuesta Dto(string propuestaId, string bloque, ParrafoContenido p) =>
        new(p.Id, p.Version, propuestaId, bloque, p.Orden, p.Contenido, p.Activo, p.MostrarSoloSiCumple, p.Condicion, p.CamposUsados, p.UnirConAnterior, p.EsTitulo);

    private static string NuevoIdParrafo() => $"PAR-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    /// <summary>
    /// Valida el párrafo con las mismas reglas del editor y devuelve el contenido normalizado. El bloque debe ser uno
    /// de los del tipo documental; sus campos se validan con el contexto del bloque.
    /// </summary>
    private async Task<ParrafoContenido> ValidarAsync(Propuesta propuesta, DatosParrafo datos, CancellationToken ct)
    {
        var resultado = new ResultadoValidacion();
        var configurados = await catalogoBloques.DeContenidoAsync(propuesta.TipoDocumentoId, propuesta.ContenidoPropuesto, ct);
        var bloque = configurados.FirstOrDefault(c => c.Codigo == datos.Bloque);
        if (bloque is null)
        {
            resultado.Agregar("bloque", $"El bloque '{datos.Bloque}' no forma parte de este tipo de documento.");
            resultado.LanzarSiHayErrores();
        }
        if (datos.Contenido is null)
        {
            resultado.Agregar("contenido", "El contenido es obligatorio.");
            resultado.LanzarSiHayErrores();
        }
        var catalogo = (await campos.ListarAsync(ct)).ToDictionary(c => c.Id, StringComparer.Ordinal);
        ContenidoEditor.Validar(datos.Contenido!, bloque!.ContextoCampos, catalogo, resultado);
        if (datos.EsTitulo && datos.UnirConAnterior)
            resultado.Agregar("esTitulo", "Un título no se puede unir con el párrafo anterior: quite una de las dos marcas.");
        var condicion = datos.Condicion is { Reglas.Count: 0, Grupos.Count: 0 } ? null : datos.Condicion;
        if (condicion is not null)
            ReglasCondiciones.Validar(condicion, OrigenesDatos.Solicitudes, catalogo, resultado);
        resultado.LanzarSiHayErrores();

        return new ParrafoContenido
        {
            Contenido = datos.Contenido!,
            Activo = datos.Activo,
            MostrarSoloSiCumple = datos.MostrarSoloSiCumple,
            UnirConAnterior = datos.UnirConAnterior,
            EsTitulo = datos.EsTitulo,
            Condicion = condicion,
            CamposUsados = ContenidoEditor.CamposUsados(datos.Contenido!),
        };
    }

    /// <summary>
    /// Aplica un cambio al contenido de la propuesta dentro de una transacción. Si la propuesta tiene una Certificación
    /// derivada, el mismo cambio se lleva a ella (D3). Cada cambio queda en el historial con el antes y el después del
    /// párrafo (no la propuesta completa).
    /// </summary>
    private async Task<T> EditarAsync<T>(string propuestaId, Func<EdicionContenido, T> operacion, CancellationToken ct)
    {
        var analista = await autorizacion.ExigirAsync(Rol.Analista, ct);
        var nombresTipo = (await tipos.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nombre, StringComparer.Ordinal);
        var ahora = reloj.GetUtcNow();
        return await unidad.EjecutarAsync(async tx =>
        {
            var todas = await tx.ListarAsync<Propuesta>(ct);
            var propuesta = todas.FirstOrDefault(p => p.Id == propuestaId) ?? throw new EntidadNoEncontradaException(propuestas.Coleccion, propuestaId);
            var consolidado = await EditableAsync(tx, propuesta.ConsolidadoId, analista, ct);
            if (propuesta.Retirada)
                throw new ConflictoException("La propuesta fue retirada del documento: no se puede editar.");

            var edicion = new EdicionContenido(propuesta.Id, propuesta.ContenidoPropuesto);
            var resultado = operacion(edicion);
            if (edicion.Cambios.Count == 0 && edicion.DisenoAnterior is null)
                return resultado;

            propuesta.FechaModificacion = ahora;
            await tx.ActualizarAsync(propuesta, ct);

            foreach (var derivada in todas.Where(p => p.DerivadaDePropuestaId == propuesta.Id && !p.Retirada).ToList())
            {
                var palabra = nombresTipo.TryGetValue(propuesta.TipoDocumentoId, out var deOrigen) && nombresTipo.TryGetValue(derivada.TipoDocumentoId, out var deCopia)
                    ? new CambioPalabra(deOrigen, deCopia)
                    : null;
                FusionDerivada.Aplicar(derivada.ContenidoPropuesto, propuesta.ContenidoPropuesto, edicion.Cambios, palabra, NuevoIdParrafo);
                if (edicion.DisenoAnterior is { } disenoAnterior)
                    FusionDerivada.AplicarDiseno(derivada.ContenidoPropuesto.Diseno, disenoAnterior, propuesta.ContenidoPropuesto.Diseno);
                derivada.FechaModificacion = ahora;
                await tx.ActualizarAsync(derivada, ct);
            }

            await TocarAsync(tx, consolidado, ahora, ct);
            var registro = edicion.Cambios.Select(c => new CambioHistorial(
                propuestas.Coleccion, propuesta.Id, AccionHistorial.Actualizacion, propuesta.Version,
                c.Anterior is null ? null : new { bloque = c.BloqueAnterior, parrafo = c.Anterior },
                c.Actual is null ? null : new { bloque = c.BloqueActual, parrafo = c.Actual })).ToList();
            if (edicion.DisenoAnterior is { } d)
                registro.Add(new CambioHistorial(propuestas.Coleccion, propuesta.Id, AccionHistorial.Actualizacion, propuesta.Version,
                    new { diseno = d }, new { diseno = propuesta.ContenidoPropuesto.Diseno }));
            foreach (var cambio in registro)
                await tx.CrearAsync(ServicioHistorial.Construir(cambio, analista.Id, ahora), ct);
            return resultado;
        }, ct);
    }

    /// <summary>Operaciones del editor sobre el contenido de una propuesta. Registra cada cambio para la Certificación derivada.</summary>
    private sealed class EdicionContenido(string propuestaId, ContenidoPlantilla contenido)
    {
        public List<CambioParrafo> Cambios { get; } = [];
        public DisenoPlantilla? DisenoAnterior { get; private set; }

        public ParrafoPropuesta Crear(string codigo, ParrafoContenido nuevo, int orden)
        {
            var bloque = Bloque(codigo);
            nuevo.Id = NuevoIdParrafo();
            nuevo.Version = 1;
            var lista = bloque.Parrafos.OrderBy(p => p.Orden).ToList();
            var indice = orden <= 0 || orden > lista.Count ? lista.Count : orden - 1;
            lista.Insert(indice, nuevo);
            Renumerar(bloque, lista, except: nuevo);
            Cambios.Add(new CambioParrafo(null, null, bloque.Codigo, FusionDerivada.Clonar(nuevo)));
            return Dto(propuestaId, bloque.Codigo, nuevo);
        }

        public ParrafoPropuesta Actualizar(string id, int version, string codigo, ParrafoContenido datos)
        {
            var (bloque, parrafo) = Buscar(id);
            ExigirVersion(parrafo, version);
            var anterior = FusionDerivada.Clonar(parrafo);
            parrafo.Contenido = datos.Contenido;
            parrafo.Activo = datos.Activo;
            parrafo.MostrarSoloSiCumple = datos.MostrarSoloSiCumple;
            parrafo.UnirConAnterior = datos.UnirConAnterior;
            parrafo.EsTitulo = datos.EsTitulo;
            parrafo.Condicion = datos.Condicion;
            parrafo.CamposUsados = datos.CamposUsados;
            var destino = Bloque(codigo);
            if (destino != bloque)
            {
                bloque.Parrafos.Remove(parrafo);
                parrafo.Orden = destino.Parrafos.Select(p => p.Orden).DefaultIfEmpty(0).Max() + 1;
                destino.Parrafos.Add(parrafo);
            }
            parrafo.Version++;
            Cambios.Add(new CambioParrafo(bloque.Codigo, anterior, destino.Codigo, FusionDerivada.Clonar(parrafo)));
            return Dto(propuestaId, destino.Codigo, parrafo);
        }

        public bool Eliminar(string id, int? version)
        {
            var (bloque, parrafo) = Buscar(id);
            if (version is not null)
                ExigirVersion(parrafo, version.Value);
            bloque.Parrafos.Remove(parrafo);
            Cambios.Add(new CambioParrafo(bloque.Codigo, FusionDerivada.Clonar(parrafo), null, null));
            return true;
        }

        public ParrafoPropuesta Duplicar(string id)
        {
            var (bloque, original) = Buscar(id);
            var copia = FusionDerivada.Clonar(original);
            copia.Id = NuevoIdParrafo();
            copia.Version = 1;
            // En una Certificación, el duplicado es un párrafo propio, no ligado a la Resolución.
            copia.OrigenParrafoId = null;
            var lista = bloque.Parrafos.OrderBy(p => p.Orden).ToList();
            lista.Insert(lista.IndexOf(original) + 1, copia);
            Renumerar(bloque, lista, except: copia);
            Cambios.Add(new CambioParrafo(null, null, bloque.Codigo, FusionDerivada.Clonar(copia)));
            return Dto(propuestaId, bloque.Codigo, copia);
        }

        public IReadOnlyList<ParrafoPropuesta> Reordenar(string codigo, List<ElementoOrden> elementos)
        {
            var bloque = Bloque(codigo);
            var ids = elementos.Select(e => e.Id).ToList();
            if (ids.Distinct().Count() != ids.Count || !ids.ToHashSet().SetEquals(bloque.Parrafos.Select(p => p.Id)))
                throw new ConflictoException("La lista de párrafos cambió mientras la ordenaba. Recargue la sección e intente de nuevo.");
            var porId = bloque.Parrafos.ToDictionary(p => p.Id);
            foreach (var elemento in elementos)
                ExigirVersion(porId[elemento.Id], elemento.Version);

            var lista = elementos.Select(e => porId[e.Id]).ToList();
            Renumerar(bloque, lista, except: null);
            return lista.Select(p => Dto(propuestaId, bloque.Codigo, p)).ToList();
        }

        public bool CambiarDiseno(Action<DisenoPlantilla> cambio)
        {
            var anterior = FusionDerivada.Clonar(contenido.Diseno);
            cambio(contenido.Diseno);
            if (!ComparadorContenido.MismoJson(anterior, contenido.Diseno))
                DisenoAnterior = anterior;
            return true;
        }

        /// <summary>Numera 1, 2, 3… y registra como cambio cada párrafo que cambió de posición.</summary>
        private void Renumerar(BloqueContenido bloque, List<ParrafoContenido> lista, ParrafoContenido? except)
        {
            for (var i = 0; i < lista.Count; i++)
            {
                var parrafo = lista[i];
                if (parrafo == except)
                {
                    parrafo.Orden = i + 1;
                    continue;
                }
                if (parrafo.Orden == i + 1)
                    continue;
                var anterior = FusionDerivada.Clonar(parrafo);
                parrafo.Orden = i + 1;
                parrafo.Version++;
                Cambios.Add(new CambioParrafo(bloque.Codigo, anterior, bloque.Codigo, FusionDerivada.Clonar(parrafo)));
            }
            bloque.Parrafos.Clear();
            bloque.Parrafos.AddRange(lista);
        }

        private BloqueContenido Bloque(string codigo)
        {
            var bloque = contenido.Bloques.FirstOrDefault(b => b.Codigo == codigo);
            if (bloque is null)
            {
                bloque = new BloqueContenido { Codigo = codigo };
                contenido.Bloques.Add(bloque);
            }
            return bloque;
        }

        private (BloqueContenido, ParrafoContenido) Buscar(string id)
        {
            foreach (var bloque in contenido.Bloques)
            {
                if (bloque.Parrafos.FirstOrDefault(p => p.Id == id) is { } parrafo)
                    return (bloque, parrafo);
            }
            throw new EntidadNoEncontradaException("parrafos", id);
        }

        private static void ExigirVersion(ParrafoContenido parrafo, int version)
        {
            if (parrafo.Version != version)
                throw new ConflictoException(ServicioEntidades<Parrafo>.MensajeVersion(parrafo.Version, version));
        }
    }
}
