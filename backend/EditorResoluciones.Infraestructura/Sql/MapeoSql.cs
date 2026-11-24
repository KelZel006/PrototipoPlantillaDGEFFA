using EditorResoluciones.Infraestructura.Json;

namespace EditorResoluciones.Infraestructura.Sql;

/// <summary>Cómo se guarda el valor de una propiedad en su columna.</summary>
internal enum TipoColumna
{
    /// <summary>Texto, fecha o identificador. NULL equivale a que la propiedad no viene.</summary>
    Texto,

    /// <summary>Lista opcional de la identidad (Tipo Documento, Documento, Beneficios): el vacío del modelo es NULL.</summary>
    Opcional,
    Numero,
    Logico,

    /// <summary>Objeto o arreglo guardado como JSON (contenido de versiones y propuestas, condiciones…).</summary>
    Json,

    /// <summary>Lógico del modelo (activo) guardado en la columna de auditoría ESTADO: 'ACTIVO' o 'INACTIVO'.</summary>
    Estado,
}

/// <summary>Columna de una tabla y la propiedad del modelo que guarda (en camelCase; con punto, una propiedad anidada).</summary>
internal sealed record Columna(string Nombre, string[] Ruta, TipoColumna Tipo);

/// <summary>
/// Lista del modelo que vive en otra tabla, enlazada por <see cref="ColumnaPadre"/>. Con <see cref="Escalar"/>, cada
/// elemento es un valor simple (por ejemplo, los roles de un usuario); si no, un objeto con <see cref="Columnas"/>.
/// </summary>
internal sealed record TablaHija(string Propiedad, string Tabla, string ColumnaPadre, string? Escalar, Columna[] Columnas, string? ColumnaOrden = null);

/// <summary>Tabla SEFH_ de una colección del sistema.</summary>
internal sealed record TablaColeccion(DefinicionColeccion Coleccion, string Tabla, Columna[] Columnas, TablaHija[] Hijas)
{
    /// <summary>Llave primaria de la tabla (ID_siglas_SEQ): la columna que guarda el <c>id</c> del modelo.</summary>
    public string ColumnaId { get; } = Columnas.Single(c => c.Ruta is ["id"]).Nombre;

    /// <summary>Valores fijos al insertar en columnas que el modelo no tiene (por ejemplo, el contenido de un archivo).</summary>
    public IReadOnlyDictionary<string, string> AlInsertar { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Correspondencia entre las colecciones del sistema y las tablas SEFH_ creadas por
/// <c>database/sqlserver-sefh/01-SEFH-esquema.sql</c>. Es la misma que usa <c>generar-sefh.js</c> para cargar los datos.
/// Cada tabla lleva además la auditoría ESTADO, USU_CRE, FEC_CRE, USU_MOD y FEC_MOD: ESTADO guarda el <c>activo</c> del
/// modelo cuando lo tiene (si no, queda en 'ACTIVO'); las demás las escribe <see cref="AlmacenSql"/>.
/// </summary>
internal static class MapeoSql
{
    private static readonly TablaColeccion[] Tablas =
    [
        Tabla("unidades", "SEFH_UNIDADES", "ID_UNI_SEQ=id Nombre Orden:n ESTADO=activo:e Version:n"),
        Tabla("rubros", "SEFH_RUBROS", "ID_RUBRO_SEQ=id Nombre ESTADO=activo:e Version:n"),
        Tabla("subRubros", "SEFH_SUB_RUBROS", "ID_SUBRUBRO_SEQ=id ID_RUBRO_SEQ=rubroId Nombre ESTADO=activo:e Version:n"),
        Tabla("tiposDocumento", "SEFH_TIPOS_DOCUMENTO", "ID_TIPDOC_SEQ=id Nombre TextoCorrido:b CopiaDeTipoDocumentoId ESTADO=activo:e Version:n",
            new TablaHija("bloques", "SEFH_TIPO_DOCUMENTO_BLOQUES", "ID_TIPDOC_SEQ", null, Columnas("ID_BLODOC_SEQ=bloqueId Orden:n Obligatorio:b"), "Orden")),
        Tabla("clasesDocumento", "SEFH_CLASES_DOCUMENTO", "ID_CLADOC_SEQ=id Nombre ESTADO=activo:e Version:n"),
        Tabla("beneficios", "SEFH_BENEFICIOS", "ID_BEN_SEQ=id Nombre ESTADO=activo:e Version:n"),
        Tabla("plantillas", "SEFH_PLANTILLAS",
            "ID_PLA_SEQ=id Nombre NombreReferencia ID_UNI_SEQ=unidadId ID_RUBRO_SEQ=rubroId ID_SUBRUBRO_SEQ=subRubroId " +
            "ID_TIPDOC_SEQ=tipoDocumentoId:o ID_CLADOC_SEQ=claseDocumentoId:o ID_BEN_SEQ=beneficioId:o ESTADO_PLANTILLA=estado " +
            "Descripcion ID_ARCH_SEQ=logoId AlineacionLogo PiePagina EspacioFirmasCm:n FechaModificacion Version:n"),
        Tabla("parrafos", "SEFH_PARRAFOS",
            "ID_PARR_SEQ=id ID_PLA_SEQ=plantillaId Seccion Orden:n Contenido:j ESTADO=activo:e MostrarSoloSiCumple:b UnirConAnterior:b " +
            "EsTitulo:b Condicion:j CamposUsados:j OrigenParrafoId FechaModificacion Version:n"),
        Tabla("campos", "SEFH_CAMPOS",
            "ID_CAMPO_SEQ=id Etiqueta Categoria TipoDato OrigenColeccion=origen.coleccion OrigenPropiedad=origen.propiedad Descripcion AlertarCambios:b Version:n",
            new TablaHija("contextos", "SEFH_CAMPO_CONTEXTOS", "ID_CAMPO_SEQ", "Contexto", [])),
        Tabla("solicitudes", "SEFH_SOLICITUDES",
            "ID_SOL_SEQ=id NumeroSolicitud Operador Beneficiario ID_RUBRO_SEQ=rubroId ID_SUBRUBRO_SEQ=subRubroId TipoBeneficio " +
            "ESTADO_SOLICITUD=estado FechaResolucion AnioGestion:n MontoExoneracion:n UnidadEjecutora Departamento Municipio EsDatoPrueba:b Version:n"),
        Tabla("detallesBienes", "SEFH_DETALLES_BIENES",
            "ID_DETBIEN_SEQ=id ID_SOL_SEQ=solicitudId CodigoArancelario Descripcion UnidadMedida Cantidad:n ValorEstimado:n " +
            "DescripcionArancelaria MontoAutorizado:n EsDatoPrueba:b Version:n"),
        Tabla("historial", "SEFH_HISTORIAL", "ID_HIST_SEQ=id Fecha Usuario Coleccion EntidadId Accion VersionEntidad:n Anterior:j Nuevo:j Version:n"),
        // El contenido del archivo lo guarda AlmacenArchivosSql después de crear el registro.
        Tabla("archivos", "SEFH_ARCHIVOS", "ID_ARCH_SEQ=id NombreOriginal TipoContenido Tamano:n Sha256 FechaCarga Version:n") with
        {
            AlInsertar = new Dictionary<string, string> { ["Contenido"] = "0x" },
        },
        Tabla("usuarios", "SEFH_USUARIOS", "ID_USU_SEQ=id NombreCompleto ESTADO=activo:e Version:n",
            new TablaHija("roles", "SEFH_USUARIO_ROLES", "ID_USU_SEQ", "Rol", [])),
        Tabla("bloques", "SEFH_BLOQUES_DOCUMENTALES",
            "ID_BLODOC_SEQ=id Nombre Titulo Numeracion EsFirmas:b ContextoCampos Descripcion ESTADO=activo:e Version:n",
            new TablaHija("reemplaza", "SEFH_BLOQUE_REEMPLAZOS", "ID_BLODOC_SEQ", "ReemplazaBloqueId", [])),
        Tabla("versionesPlantilla", "SEFH_PLANTILLAS_CONSOLIDADO",
            "ID_VERPLA_SEQ=id ID_PLA_SEQ=plantillaId NumeroVersion:n Vigencia Contenido:j Huella VersionAnteriorId ID_CONS_SEQ=consolidadoId " +
            "CargaInicial:b PublicadaPor FechaPublicacion InicioVigencia FinVigencia Version:n"),
        Tabla("consolidados", "SEFH_PLANTILLAS_DOC_CAB",
            "ID_CONS_SEQ=id Numero Anio:n Secuencia:n ESTADO_DOCUMENTO=estado ID_UNI_SEQ=unidadId AnalistaId FechaCreacion FechaActualizacion " +
            "NumeroPresentacion:n FechaEnvio EnviadoPor RevisorId FechaRevision AprobadorId FechaAprobacion Version:n"),
        Tabla("propuestas", "SEFH_PLANTILLA_DOC",
            "ID_PLADOC_SEQ=id ID_CONS_SEQ=consolidadoId Operacion ID_UNI_SEQ=unidadId ID_RUBRO_SEQ=rubroId ID_SUBRUBRO_SEQ=subRubroId " +
            "ID_TIPDOC_SEQ=tipoDocumentoId:o ID_CLADOC_SEQ=claseDocumentoId:o ID_BEN_SEQ=beneficioId:o Nombre NombreReferencia " +
            "ID_PLA_SEQ=plantillaId ID_VERPLA_SEQ=versionOrigenId HuellaOrigen ContenidoPropuesto:j DerivadaDePropuestaId Retirada:b " +
            "MotivoRetiro FechaCreacion FechaModificacion Version:n"),
        Tabla("presentaciones", "SEFH_PRESENTACIONES",
            "ID_PRES_SEQ=id ID_CONS_SEQ=consolidadoId Numero:n ID_USU_SEQ=usuario Fecha CantidadPlantillas:n HuellaIntegridad Propuestas:j Version:n"),
        Tabla("devoluciones", "SEFH_DEVOLUCIONES",
            "ID_DEV_SEQ=id ID_CONS_SEQ=consolidadoId NumeroPresentacion:n ID_USU_SEQ=usuario Fecha Justificacion Version:n",
            new TablaHija("observaciones", "SEFH_OBSERVACIONES", "ID_DEV_SEQ", null,
                Columnas("ID_PLADOC_SEQ=propuestaId BloqueCodigo ParrafoId Problema Recomendacion"), "Orden")),
        Tabla("movimientos", "SEFH_MOVIMIENTOS",
            "ID_MOV_SEQ=id ID_CONS_SEQ=consolidadoId Tipo Usuario Fecha EstadoOrigen EstadoDestino NumeroPresentacion:n Detalle Version:n"),
    ];

    private static readonly Dictionary<Type, TablaColeccion> PorTipo = Tablas.ToDictionary(t => t.Coleccion.TipoEntidad);

    public static IReadOnlyList<TablaColeccion> Todas => Tablas;

    public static TablaColeccion De<T>() =>
        PorTipo.TryGetValue(typeof(T), out var tabla)
            ? tabla
            : throw new InvalidOperationException($"El tipo {typeof(T).Name} no tiene una tabla SEFH_ registrada.");

    public static TablaColeccion De(DefinicionColeccion coleccion) => Tablas.First(t => t.Coleccion == coleccion);

    private static TablaColeccion Tabla(string coleccion, string tabla, string columnas, params TablaHija[] hijas) =>
        new(ColeccionesJson.PorNombreColeccion(coleccion), tabla, Columnas(columnas), hijas);

    /// <summary>
    /// «Columna», «Columna:tipo» o «Columna=ruta.de.propiedad:tipo». Tipos: o (opcional), n (número), b (lógico), j (JSON),
    /// e (lógico guardado en ESTADO);
    /// sin tipo, texto. La propiedad predeterminada es el nombre de la columna en camelCase.
    /// </summary>
    private static Columna[] Columnas(string definicion) =>
        definicion.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(parte =>
        {
            var (nombreYRuta, tipo) = parte.Split(':') is [var n, var t] ? (n, t) : (parte, "");
            var (nombre, ruta) = nombreYRuta.Split('=') is [var c, var r] ? (c, r) : (nombreYRuta, char.ToLowerInvariant(nombreYRuta[0]) + nombreYRuta[1..]);
            return new Columna(nombre, ruta.Split('.'), tipo switch
            {
                "o" => TipoColumna.Opcional,
                "n" => TipoColumna.Numero,
                "b" => TipoColumna.Logico,
                "j" => TipoColumna.Json,
                "e" => TipoColumna.Estado,
                "" => TipoColumna.Texto,
                _ => throw new InvalidOperationException($"Tipo de columna desconocido: {tipo}"),
            });
        }).ToArray();
}
