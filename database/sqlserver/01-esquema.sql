/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Esquema para SQL Server 2019
   ---------------------------------------------------------------------------------------------------------------------
   Corresponde al modelo del dominio (backend/EditorResoluciones.Dominio/Modelos) y a la estrategia de la Fase 8
   (docs/CONSOLIDADO-FASE8.md).
   - Los identificadores son los mismos de los archivos JSON (por ejemplo, PLA-001, CON-0001), para importar los datos
     sin traducirlos.
   - Los valores de las enumeraciones se guardan como texto, igual que en el JSON ('EnRegistro', 'Vigente'…).
   - El contenido de una versión o propuesta (diseño, bloques y párrafos) se guarda como JSON: es una fotografía
     inmutable y su huella SHA-256 se calcula sobre ella.
   - La columna Version es el control de concurrencia optimista de la aplicación.
   Se ejecuta sobre una base de datos vacía.
   ===================================================================================================================== */

-- Base de datos recomendada (ejecutar aparte, con un usuario administrador):
-- CREATE DATABASE PlantillasDGEFFA COLLATE Modern_Spanish_CI_AI;
-- ALTER DATABASE PlantillasDGEFFA SET READ_COMMITTED_SNAPSHOT ON;
-- ALTER DATABASE PlantillasDGEFFA SET ALLOW_SNAPSHOT_ISOLATION ON;

SET XACT_ABORT ON;
GO

IF SCHEMA_ID(N'plantillas') IS NULL
    EXEC (N'CREATE SCHEMA plantillas');
GO

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------------------------------------------------------
   1. Usuarios y roles
   En producción la identidad viene del proveedor institucional; esta tabla es el espejo que usan la bandeja (D10),
   la separación de funciones y la auditoría.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE plantillas.Usuarios (
    Id              varchar(60)     NOT NULL,
    NombreCompleto  nvarchar(150)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_Usuarios_Version DEFAULT (1),
    CONSTRAINT PK_Usuarios PRIMARY KEY (Id)
);

CREATE TABLE plantillas.UsuarioRoles (
    UsuarioId       varchar(60)     NOT NULL,
    Rol             varchar(20)     NOT NULL,
    CONSTRAINT PK_UsuarioRoles PRIMARY KEY (UsuarioId, Rol),
    CONSTRAINT FK_UsuarioRoles_Usuario FOREIGN KEY (UsuarioId) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT CK_UsuarioRoles_Rol CHECK (Rol IN ('Analista', 'OficialJuridico', 'Administrador'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   2. Catálogos
   --------------------------------------------------------------------------------------------------------------------- */
-- Unidades que elaboran sus propias plantillas: Exoneraciones, Dispensas, Registro Exonerados, Notas de Crédito y Devoluciones.
CREATE TABLE plantillas.Unidades (
    Id              varchar(40)     NOT NULL,           -- por ejemplo exoneraciones
    Nombre          nvarchar(150)   NOT NULL,
    Orden           int             NOT NULL,           -- posición en el menú
    Activo          bit             NOT NULL CONSTRAINT DF_Unidades_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_Unidades_Version DEFAULT (1),
    CONSTRAINT PK_Unidades PRIMARY KEY (Id),
    CONSTRAINT UQ_Unidades_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_Unidades_Orden CHECK (Orden BETWEEN 1 AND 100)
);

CREATE TABLE plantillas.Rubros (
    Id              varchar(20)     NOT NULL,           -- código institucional, por ejemplo 1000
    Nombre          nvarchar(300)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_Rubros_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_Rubros_Version DEFAULT (1),
    CONSTRAINT PK_Rubros PRIMARY KEY (Id)
);

CREATE TABLE plantillas.SubRubros (
    Id              varchar(20)     NOT NULL,
    RubroId         varchar(20)     NOT NULL,
    Nombre          nvarchar(300)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_SubRubros_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_SubRubros_Version DEFAULT (1),
    CONSTRAINT PK_SubRubros PRIMARY KEY (Id),
    CONSTRAINT FK_SubRubros_Rubro FOREIGN KEY (RubroId) REFERENCES plantillas.Rubros (Id),
    -- Permite exigir en otras tablas que el sub rubro pertenezca a su rubro.
    CONSTRAINT UQ_SubRubros_Rubro_Id UNIQUE (RubroId, Id)
);

-- Lista «Documento»: Original, Modificación, Aclaratoria.
CREATE TABLE plantillas.ClasesDocumento (
    Id              varchar(20)     NOT NULL,
    Nombre          nvarchar(150)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_ClasesDocumento_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_ClasesDocumento_Version DEFAULT (1),
    CONSTRAINT PK_ClasesDocumento PRIMARY KEY (Id),
    CONSTRAINT UQ_ClasesDocumento_Nombre UNIQUE (Nombre)
);

-- Catálogo de bloques documentales: CABECERA, VISTOS, CONSIDERANDO, FUNDAMENTACION_LEGAL…
CREATE TABLE plantillas.BloquesDocumentales (
    Id              varchar(40)     NOT NULL,
    Nombre          nvarchar(80)    NOT NULL,
    Titulo          nvarchar(80)    NULL,
    Numeracion      varchar(15)     NOT NULL CONSTRAINT DF_BloquesDocumentales_Numeracion DEFAULT ('Ninguna'),
    EsFirmas        bit             NOT NULL CONSTRAINT DF_BloquesDocumentales_EsFirmas DEFAULT (0),
    ContextoCampos  varchar(20)     NOT NULL,
    Descripcion     nvarchar(300)   NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_BloquesDocumentales_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_BloquesDocumentales_Version DEFAULT (1),
    CONSTRAINT PK_BloquesDocumentales PRIMARY KEY (Id),
    CONSTRAINT CK_BloquesDocumentales_Numeracion CHECK (Numeracion IN ('Ninguna', 'Considerando', 'Ordinal')),
    CONSTRAINT CK_BloquesDocumentales_Contexto CHECK (ContextoCampos IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas'))
);

-- Bloque nuevo que recibe el contenido de uno anterior cuando un tipo documental cambia de formato
-- (por ejemplo, en el Dictamen, Dictamina reemplaza a Resuelve).
CREATE TABLE plantillas.BloqueReemplazos (
    BloqueId            varchar(40)     NOT NULL,
    ReemplazaBloqueId   varchar(40)     NOT NULL,
    CONSTRAINT PK_BloqueReemplazos PRIMARY KEY (BloqueId, ReemplazaBloqueId),
    CONSTRAINT FK_BloqueReemplazos_Bloque FOREIGN KEY (BloqueId) REFERENCES plantillas.BloquesDocumentales (Id),
    CONSTRAINT FK_BloqueReemplazos_Anterior FOREIGN KEY (ReemplazaBloqueId) REFERENCES plantillas.BloquesDocumentales (Id),
    CONSTRAINT CK_BloqueReemplazos_Distinto CHECK (BloqueId <> ReemplazaBloqueId)
);

-- Resolución, Certificación, Dictamen, Constancia de Obligados Tributarios.
CREATE TABLE plantillas.TiposDocumento (
    Id                      varchar(20)     NOT NULL,
    Nombre                  nvarchar(150)   NOT NULL,
    TextoCorrido            bit             NOT NULL CONSTRAINT DF_TiposDocumento_TextoCorrido DEFAULT (0),
    CopiaDeTipoDocumentoId  varchar(20)     NULL,       -- la Certificación es copia de la Resolución (D3)
    Activo                  bit             NOT NULL CONSTRAINT DF_TiposDocumento_Activo DEFAULT (1),
    Version                 int             NOT NULL CONSTRAINT DF_TiposDocumento_Version DEFAULT (1),
    CONSTRAINT PK_TiposDocumento PRIMARY KEY (Id),
    CONSTRAINT UQ_TiposDocumento_Nombre UNIQUE (Nombre),
    CONSTRAINT FK_TiposDocumento_CopiaDe FOREIGN KEY (CopiaDeTipoDocumentoId) REFERENCES plantillas.TiposDocumento (Id),
    CONSTRAINT CK_TiposDocumento_CopiaDistinta CHECK (CopiaDeTipoDocumentoId IS NULL OR CopiaDeTipoDocumentoId <> Id)
);

-- Bloques de cada tipo documental, con su orden y si son obligatorios (D2).
CREATE TABLE plantillas.TipoDocumentoBloques (
    TipoDocumentoId varchar(20)     NOT NULL,
    BloqueId        varchar(40)     NOT NULL,
    Orden           int             NOT NULL,
    Obligatorio     bit             NOT NULL CONSTRAINT DF_TipoDocumentoBloques_Obligatorio DEFAULT (0),
    CONSTRAINT PK_TipoDocumentoBloques PRIMARY KEY (TipoDocumentoId, BloqueId),
    CONSTRAINT FK_TipoDocumentoBloques_Tipo FOREIGN KEY (TipoDocumentoId) REFERENCES plantillas.TiposDocumento (Id),
    CONSTRAINT FK_TipoDocumentoBloques_Bloque FOREIGN KEY (BloqueId) REFERENCES plantillas.BloquesDocumentales (Id),
    CONSTRAINT UQ_TipoDocumentoBloques_Orden UNIQUE (TipoDocumentoId, Orden),
    CONSTRAINT CK_TipoDocumentoBloques_Orden CHECK (Orden >= 1)
);

-- Campos dinámicos. Su origen sale de una lista cerrada de colecciones y propiedades (la valida la aplicación).
CREATE TABLE plantillas.Campos (
    Id                  varchar(60)     NOT NULL,
    Etiqueta            nvarchar(100)   NOT NULL,
    Categoria           nvarchar(100)   NOT NULL,
    TipoDato            varchar(10)     NOT NULL,
    OrigenColeccion     varchar(40)     NOT NULL,
    OrigenPropiedad     varchar(60)     NOT NULL,
    Descripcion         nvarchar(300)   NULL,
    AlertarCambios      bit             NOT NULL CONSTRAINT DF_Campos_AlertarCambios DEFAULT (0),  -- avisar al aprobador (Operador)
    Version             int             NOT NULL CONSTRAINT DF_Campos_Version DEFAULT (1),
    CONSTRAINT PK_Campos PRIMARY KEY (Id),
    CONSTRAINT CK_Campos_TipoDato CHECK (TipoDato IN ('Texto', 'Numero', 'Moneda', 'Fecha', 'Booleano'))
);

-- Bloques (contextos) en los que se puede usar cada campo.
CREATE TABLE plantillas.CampoContextos (
    CampoId         varchar(60)     NOT NULL,
    Contexto        varchar(20)     NOT NULL,
    CONSTRAINT PK_CampoContextos PRIMARY KEY (CampoId, Contexto),
    CONSTRAINT FK_CampoContextos_Campo FOREIGN KEY (CampoId) REFERENCES plantillas.Campos (Id) ON DELETE CASCADE,
    CONSTRAINT CK_CampoContextos_Contexto CHECK (Contexto IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas', 'tabla'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   3. Archivos (logos). Hasta 1 MB, PNG o JPEG (lo valida la aplicación por el contenido real).
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE plantillas.Archivos (
    Id              varchar(40)     NOT NULL,
    NombreOriginal  nvarchar(150)   NOT NULL,
    TipoContenido   varchar(40)     NOT NULL,
    Tamano          int             NOT NULL,
    Sha256          char(64)        NOT NULL,
    FechaCarga      datetimeoffset(7) NOT NULL,
    Contenido       varbinary(max)  NOT NULL,
    Version         int             NOT NULL CONSTRAINT DF_Archivos_Version DEFAULT (1),
    CONSTRAINT PK_Archivos PRIMARY KEY (Id),
    CONSTRAINT CK_Archivos_Tipo CHECK (TipoContenido IN ('image/png', 'image/jpeg')),
    CONSTRAINT CK_Archivos_Tamano CHECK (Tamano BETWEEN 1 AND 1048576)
);

/* ---------------------------------------------------------------------------------------------------------------------
   4. Plantillas (identidad) y sus versiones publicadas
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE plantillas.Plantillas (
    Id                  varchar(40)     NOT NULL,
    Nombre              nvarchar(200)   NOT NULL,
    UnidadId            varchar(40)     NOT NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoDocumentoId     varchar(20)     NOT NULL,
    ClaseDocumentoId    varchar(20)     NOT NULL,
    FechaModificacion   datetimeoffset(7) NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_Plantillas_Version DEFAULT (1),
    CONSTRAINT PK_Plantillas PRIMARY KEY (Id),
    CONSTRAINT FK_Plantillas_Unidad FOREIGN KEY (UnidadId) REFERENCES plantillas.Unidades (Id),
    CONSTRAINT FK_Plantillas_SubRubro FOREIGN KEY (RubroId, SubRubroId) REFERENCES plantillas.SubRubros (RubroId, Id),
    CONSTRAINT FK_Plantillas_Tipo FOREIGN KEY (TipoDocumentoId) REFERENCES plantillas.TiposDocumento (Id),
    CONSTRAINT FK_Plantillas_Clase FOREIGN KEY (ClaseDocumentoId) REFERENCES plantillas.ClasesDocumento (Id),
    -- Identidad funcional: Unidad + Rubro + Sub Rubro + Tipo Documento + Documento identifican una única plantilla.
    CONSTRAINT UQ_Plantillas_Identidad UNIQUE (UnidadId, RubroId, SubRubroId, TipoDocumentoId, ClaseDocumentoId)
);

CREATE TABLE plantillas.VersionesPlantilla (
    Id                  varchar(40)     NOT NULL,
    PlantillaId         varchar(40)     NOT NULL,
    NumeroVersion       int             NOT NULL,
    Vigencia            varchar(10)     NOT NULL,
    Contenido           nvarchar(max)   NOT NULL,       -- JSON: diseño, bloques y párrafos (inmutable)
    Huella              char(64)        NOT NULL,       -- SHA-256 del contenido
    VersionAnteriorId   varchar(40)     NULL,
    ConsolidadoId       varchar(40)     NULL,           -- documento que la originó; nulo en la carga inicial
    CargaInicial        bit             NOT NULL CONSTRAINT DF_VersionesPlantilla_CargaInicial DEFAULT (0),
    PublicadaPor        varchar(60)     NOT NULL,
    FechaPublicacion    datetimeoffset(7) NOT NULL,
    InicioVigencia      datetimeoffset(7) NOT NULL,
    FinVigencia         datetimeoffset(7) NULL,
    Version             int             NOT NULL CONSTRAINT DF_VersionesPlantilla_Version DEFAULT (1),
    CONSTRAINT PK_VersionesPlantilla PRIMARY KEY (Id),
    CONSTRAINT FK_VersionesPlantilla_Plantilla FOREIGN KEY (PlantillaId) REFERENCES plantillas.Plantillas (Id),
    CONSTRAINT FK_VersionesPlantilla_Anterior FOREIGN KEY (VersionAnteriorId) REFERENCES plantillas.VersionesPlantilla (Id),
    CONSTRAINT UQ_VersionesPlantilla_Numero UNIQUE (PlantillaId, NumeroVersion),
    CONSTRAINT CK_VersionesPlantilla_Vigencia CHECK (Vigencia IN ('Vigente', 'NoVigente')),
    CONSTRAINT CK_VersionesPlantilla_Contenido CHECK (ISJSON(Contenido) = 1),
    CONSTRAINT CK_VersionesPlantilla_Numero CHECK (NumeroVersion >= 1),
    -- Una versión vigente no tiene fin de vigencia; una no vigente sí.
    CONSTRAINT CK_VersionesPlantilla_Fin CHECK ((Vigencia = 'Vigente' AND FinVigencia IS NULL) OR (Vigencia = 'NoVigente' AND FinVigencia IS NOT NULL)),
    CONSTRAINT CK_VersionesPlantilla_Origen CHECK (CargaInicial = 1 OR ConsolidadoId IS NOT NULL)
);

-- Regla 14: como máximo una versión vigente por plantilla, garantizado por el motor.
CREATE UNIQUE INDEX UX_VersionesPlantilla_Vigente ON plantillas.VersionesPlantilla (PlantillaId) WHERE Vigencia = 'Vigente';

/* ---------------------------------------------------------------------------------------------------------------------
   5. Documento Consolidado (PLAN-AAAANNNN)
   --------------------------------------------------------------------------------------------------------------------- */
-- Último número usado por año. Se incrementa con UPDLOCK, HOLDLOCK dentro de la transacción que crea el documento.
CREATE TABLE plantillas.SecuenciasPlan (
    Anio            int             NOT NULL,
    Ultimo          int             NOT NULL CONSTRAINT DF_SecuenciasPlan_Ultimo DEFAULT (0),
    CONSTRAINT PK_SecuenciasPlan PRIMARY KEY (Anio),
    CONSTRAINT CK_SecuenciasPlan_Ultimo CHECK (Ultimo BETWEEN 0 AND 9999)
);

CREATE TABLE plantillas.Consolidados (
    Id                  varchar(40)     NOT NULL,
    Numero              char(13)        NOT NULL,
    Anio                int             NOT NULL,
    Secuencia           int             NOT NULL,
    Estado              varchar(12)     NOT NULL CONSTRAINT DF_Consolidados_Estado DEFAULT ('EnRegistro'),
    UnidadId            varchar(40)     NOT NULL,       -- solo contiene plantillas de su unidad
    AnalistaId          varchar(60)     NOT NULL,
    FechaCreacion       datetimeoffset(7) NOT NULL,
    FechaActualizacion  datetimeoffset(7) NOT NULL,
    NumeroPresentacion  int             NOT NULL CONSTRAINT DF_Consolidados_NumeroPresentacion DEFAULT (0),
    FechaEnvio          datetimeoffset(7) NULL,
    EnviadoPor          varchar(60)     NULL,
    RevisorId           varchar(60)     NULL,
    FechaRevision       datetimeoffset(7) NULL,
    AprobadorId         varchar(60)     NULL,
    FechaAprobacion     datetimeoffset(7) NULL,
    Version             int             NOT NULL CONSTRAINT DF_Consolidados_Version DEFAULT (1),
    CONSTRAINT PK_Consolidados PRIMARY KEY (Id),
    CONSTRAINT UQ_Consolidados_Numero UNIQUE (Numero),
    CONSTRAINT UQ_Consolidados_Secuencia UNIQUE (Anio, Secuencia),
    CONSTRAINT FK_Consolidados_Unidad FOREIGN KEY (UnidadId) REFERENCES plantillas.Unidades (Id),
    CONSTRAINT FK_Consolidados_Analista FOREIGN KEY (AnalistaId) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT FK_Consolidados_EnviadoPor FOREIGN KEY (EnviadoPor) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT FK_Consolidados_Revisor FOREIGN KEY (RevisorId) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT FK_Consolidados_Aprobador FOREIGN KEY (AprobadorId) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT CK_Consolidados_Estado CHECK (Estado IN ('EnRegistro', 'Verificado', 'Aprobado')),
    CONSTRAINT CK_Consolidados_Secuencia CHECK (Secuencia BETWEEN 1 AND 9999),
    CONSTRAINT CK_Consolidados_Anio CHECK (Anio BETWEEN 2000 AND 2999),
    -- El número se deriva del año y la secuencia: PLAN-20260001.
    CONSTRAINT CK_Consolidados_Numero CHECK (Numero = CONCAT('PLAN-', Anio, RIGHT(CONCAT('000', Secuencia), 4))),
    -- Lo aprobado tiene aprobador y fecha, y nunca lo aprueba quien lo preparó (separación de funciones).
    CONSTRAINT CK_Consolidados_Aprobacion CHECK (Estado <> 'Aprobado' OR (AprobadorId IS NOT NULL AND FechaAprobacion IS NOT NULL)),
    CONSTRAINT CK_Consolidados_Separacion CHECK (AprobadorId IS NULL OR (AprobadorId <> AnalistaId AND (EnviadoPor IS NULL OR AprobadorId <> EnviadoPor)))
);

CREATE INDEX IX_Consolidados_Bandeja ON plantillas.Consolidados (UnidadId, Estado, AnalistaId) INCLUDE (Numero, FechaActualizacion);

CREATE TABLE plantillas.Propuestas (
    Id                      varchar(40)     NOT NULL,
    ConsolidadoId           varchar(40)     NOT NULL,
    Operacion               varchar(10)     NOT NULL,
    UnidadId                varchar(40)     NOT NULL,
    RubroId                 varchar(20)     NOT NULL,
    SubRubroId              varchar(20)     NOT NULL,
    TipoDocumentoId         varchar(20)     NOT NULL,
    ClaseDocumentoId        varchar(20)     NOT NULL,
    Nombre                  nvarchar(200)   NOT NULL,
    PlantillaId             varchar(40)     NULL,       -- obligatorio en MODIFICAR
    VersionOrigenId         varchar(40)     NULL,       -- versión vigente de la que partió (MODIFICAR)
    HuellaOrigen            char(64)        NULL,
    ContenidoPropuesto      nvarchar(max)   NOT NULL,   -- JSON con el mismo formato del contenido de una versión
    DerivadaDePropuestaId   varchar(40)     NULL,       -- Certificación derivada de su Resolución (D3)
    Retirada                bit             NOT NULL CONSTRAINT DF_Propuestas_Retirada DEFAULT (0),
    MotivoRetiro            nvarchar(500)   NULL,
    FechaCreacion           datetimeoffset(7) NOT NULL,
    FechaModificacion       datetimeoffset(7) NOT NULL,
    Version                 int             NOT NULL CONSTRAINT DF_Propuestas_Version DEFAULT (1),
    CONSTRAINT PK_Propuestas PRIMARY KEY (Id),
    CONSTRAINT FK_Propuestas_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES plantillas.Consolidados (Id),
    CONSTRAINT FK_Propuestas_Unidad FOREIGN KEY (UnidadId) REFERENCES plantillas.Unidades (Id),
    CONSTRAINT FK_Propuestas_SubRubro FOREIGN KEY (RubroId, SubRubroId) REFERENCES plantillas.SubRubros (RubroId, Id),
    CONSTRAINT FK_Propuestas_Tipo FOREIGN KEY (TipoDocumentoId) REFERENCES plantillas.TiposDocumento (Id),
    CONSTRAINT FK_Propuestas_Clase FOREIGN KEY (ClaseDocumentoId) REFERENCES plantillas.ClasesDocumento (Id),
    CONSTRAINT FK_Propuestas_Plantilla FOREIGN KEY (PlantillaId) REFERENCES plantillas.Plantillas (Id),
    CONSTRAINT FK_Propuestas_VersionOrigen FOREIGN KEY (VersionOrigenId) REFERENCES plantillas.VersionesPlantilla (Id),
    CONSTRAINT FK_Propuestas_Derivada FOREIGN KEY (DerivadaDePropuestaId) REFERENCES plantillas.Propuestas (Id),
    CONSTRAINT CK_Propuestas_Operacion CHECK (Operacion IN ('Crear', 'Modificar')),
    CONSTRAINT CK_Propuestas_Contenido CHECK (ISJSON(ContenidoPropuesto) = 1),
    CONSTRAINT CK_Propuestas_Modificar CHECK (Operacion <> 'Modificar' OR (PlantillaId IS NOT NULL AND VersionOrigenId IS NOT NULL)),
    CONSTRAINT CK_Propuestas_Retiro CHECK (Retirada = 0 OR (MotivoRetiro IS NOT NULL AND LEN(MotivoRetiro) >= 5))
);

CREATE INDEX IX_Propuestas_Consolidado ON plantillas.Propuestas (ConsolidadoId);
CREATE INDEX IX_Propuestas_Plantilla ON plantillas.Propuestas (PlantillaId) WHERE PlantillaId IS NOT NULL;

ALTER TABLE plantillas.VersionesPlantilla
    ADD CONSTRAINT FK_VersionesPlantilla_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES plantillas.Consolidados (Id);

-- D4: una plantilla (por su identidad funcional) solo puede estar en un documento abierto. La aplicación inserta la
-- fila al agregar la propuesta y la borra al retirarla o al aprobar el documento.
CREATE TABLE plantillas.BloqueosPlantilla (
    UnidadId            varchar(40)     NOT NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoDocumentoId     varchar(20)     NOT NULL,
    ClaseDocumentoId    varchar(20)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    PropuestaId         varchar(40)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_BloqueosPlantilla PRIMARY KEY (UnidadId, RubroId, SubRubroId, TipoDocumentoId, ClaseDocumentoId),
    CONSTRAINT FK_BloqueosPlantilla_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES plantillas.Consolidados (Id),
    CONSTRAINT FK_BloqueosPlantilla_Propuesta FOREIGN KEY (PropuestaId) REFERENCES plantillas.Propuestas (Id),
    CONSTRAINT UQ_BloqueosPlantilla_Propuesta UNIQUE (PropuestaId)
);

-- Cada envío a verificación, con la copia congelada de lo enviado y su huella de integridad.
CREATE TABLE plantillas.Presentaciones (
    Id                  varchar(40)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    Numero              int             NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    CantidadPlantillas  int             NOT NULL,
    HuellaIntegridad    char(64)        NOT NULL,
    Propuestas          nvarchar(max)   NOT NULL,       -- JSON: arreglo de las propuestas tal como se enviaron
    Version             int             NOT NULL CONSTRAINT DF_Presentaciones_Version DEFAULT (1),
    CONSTRAINT PK_Presentaciones PRIMARY KEY (Id),
    CONSTRAINT FK_Presentaciones_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES plantillas.Consolidados (Id),
    CONSTRAINT FK_Presentaciones_Usuario FOREIGN KEY (Usuario) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT UQ_Presentaciones_Numero UNIQUE (ConsolidadoId, Numero),
    CONSTRAINT CK_Presentaciones_Numero CHECK (Numero >= 1),
    CONSTRAINT CK_Presentaciones_Cantidad CHECK (CantidadPlantillas >= 1),
    CONSTRAINT CK_Presentaciones_Propuestas CHECK (ISJSON(Propuestas) = 1)
);

CREATE TABLE plantillas.Devoluciones (
    Id                  varchar(40)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    NumeroPresentacion  int             NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    Justificacion       nvarchar(2000)  NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_Devoluciones_Version DEFAULT (1),
    CONSTRAINT PK_Devoluciones PRIMARY KEY (Id),
    CONSTRAINT FK_Devoluciones_Presentacion FOREIGN KEY (ConsolidadoId, NumeroPresentacion) REFERENCES plantillas.Presentaciones (ConsolidadoId, Numero),
    CONSTRAINT FK_Devoluciones_Usuario FOREIGN KEY (Usuario) REFERENCES plantillas.Usuarios (Id),
    CONSTRAINT UQ_Devoluciones_Presentacion UNIQUE (ConsolidadoId, NumeroPresentacion),
    CONSTRAINT CK_Devoluciones_Justificacion CHECK (LEN(LTRIM(RTRIM(Justificacion))) > 0)
);

CREATE TABLE plantillas.Observaciones (
    Id                  int IDENTITY(1, 1) NOT NULL,
    DevolucionId        varchar(40)     NOT NULL,
    Orden               int             NOT NULL,
    PropuestaId         varchar(40)     NOT NULL,
    BloqueCodigo        varchar(40)     NULL,
    ParrafoId           varchar(40)     NULL,
    Problema            nvarchar(2000)  NOT NULL,
    Recomendacion       nvarchar(2000)  NULL,
    CONSTRAINT PK_Observaciones PRIMARY KEY (Id),
    CONSTRAINT FK_Observaciones_Devolucion FOREIGN KEY (DevolucionId) REFERENCES plantillas.Devoluciones (Id),
    CONSTRAINT FK_Observaciones_Propuesta FOREIGN KEY (PropuestaId) REFERENCES plantillas.Propuestas (Id),
    CONSTRAINT UQ_Observaciones_Orden UNIQUE (DevolucionId, Orden)
);

/* ---------------------------------------------------------------------------------------------------------------------
   6. Auditoría: bitácora del documento e historial técnico (solo se insertan)
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE plantillas.Movimientos (
    Id                  varchar(40)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    Tipo                varchar(30)     NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    EstadoOrigen        varchar(12)     NULL,
    EstadoDestino       varchar(12)     NULL,
    NumeroPresentacion  int             NULL,
    Detalle             nvarchar(1000)  NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_Movimientos_Version DEFAULT (1),
    CONSTRAINT PK_Movimientos PRIMARY KEY (Id),
    CONSTRAINT FK_Movimientos_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES plantillas.Consolidados (Id),
    CONSTRAINT CK_Movimientos_Tipo CHECK (Tipo IN ('Creacion', 'PropuestaAgregada', 'PropuestaModificada', 'PropuestaRetirada',
        'EnvioVerificacion', 'Devolucion', 'Aprobacion', 'Publicacion', 'CambioVigencia', 'ErrorPublicacion', 'Reasignacion')),
    CONSTRAINT CK_Movimientos_EstadoOrigen CHECK (EstadoOrigen IS NULL OR EstadoOrigen IN ('EnRegistro', 'Verificado', 'Aprobado')),
    CONSTRAINT CK_Movimientos_EstadoDestino CHECK (EstadoDestino IS NULL OR EstadoDestino IN ('EnRegistro', 'Verificado', 'Aprobado'))
);

CREATE INDEX IX_Movimientos_Consolidado ON plantillas.Movimientos (ConsolidadoId, Fecha);

CREATE TABLE plantillas.Historial (
    Id                  varchar(40)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Coleccion           varchar(40)     NOT NULL,
    EntidadId           varchar(60)     NOT NULL,
    Accion              varchar(15)     NOT NULL,
    VersionEntidad      int             NOT NULL,
    Anterior            nvarchar(max)   NULL,           -- JSON del estado anterior
    Nuevo               nvarchar(max)   NULL,           -- JSON del estado nuevo
    CONSTRAINT PK_Historial PRIMARY KEY (Id),
    CONSTRAINT CK_Historial_Accion CHECK (Accion IN ('Creacion', 'Actualizacion', 'Eliminacion')),
    CONSTRAINT CK_Historial_Anterior CHECK (Anterior IS NULL OR ISJSON(Anterior) = 1),
    CONSTRAINT CK_Historial_Nuevo CHECK (Nuevo IS NULL OR ISJSON(Nuevo) = 1)
);

CREATE INDEX IX_Historial_Entidad ON plantillas.Historial (Coleccion, EntidadId, Fecha);
CREATE INDEX IX_Historial_Fecha ON plantillas.Historial (Fecha);

/* ---------------------------------------------------------------------------------------------------------------------
   7. Datos de origen de las solicitudes (para sustituir los campos al generar el documento)
   En producción pueden ser vistas o una réplica del sistema de origen; en pruebas se cargan datos ficticios.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE plantillas.Solicitudes (
    Id                  varchar(40)     NOT NULL,
    NumeroSolicitud     nvarchar(50)    NOT NULL,
    Operador            nvarchar(200)   NULL,
    Beneficiario        nvarchar(200)   NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoBeneficio       nvarchar(150)   NULL,
    Estado              nvarchar(50)    NULL,
    FechaResolucion     date            NULL,
    AnioGestion         int             NULL,
    MontoExoneracion    decimal(18, 2)  NULL,
    UnidadEjecutora     nvarchar(150)   NULL,
    Departamento        nvarchar(100)   NULL,
    Municipio           nvarchar(100)   NULL,
    EsDatoPrueba        bit             NOT NULL CONSTRAINT DF_Solicitudes_EsDatoPrueba DEFAULT (0),
    Version             int             NOT NULL CONSTRAINT DF_Solicitudes_Version DEFAULT (1),
    CONSTRAINT PK_Solicitudes PRIMARY KEY (Id),
    CONSTRAINT UQ_Solicitudes_Numero UNIQUE (NumeroSolicitud),
    CONSTRAINT FK_Solicitudes_SubRubro FOREIGN KEY (RubroId, SubRubroId) REFERENCES plantillas.SubRubros (RubroId, Id),
    CONSTRAINT CK_Solicitudes_Anio CHECK (AnioGestion IS NULL OR AnioGestion BETWEEN 2000 AND 2100),
    CONSTRAINT CK_Solicitudes_Monto CHECK (MontoExoneracion IS NULL OR MontoExoneracion >= 0)
);

CREATE TABLE plantillas.DetallesBienes (
    Id                      varchar(40)     NOT NULL,
    SolicitudId             varchar(40)     NOT NULL,
    CodigoArancelario       varchar(20)     NOT NULL,
    Descripcion             nvarchar(300)   NOT NULL,
    UnidadMedida            nvarchar(50)    NULL,
    Cantidad                decimal(18, 4)  NOT NULL,
    ValorEstimado           decimal(18, 2)  NULL,
    DescripcionArancelaria  nvarchar(300)   NULL,
    MontoAutorizado         decimal(18, 2)  NULL,
    EsDatoPrueba            bit             NOT NULL CONSTRAINT DF_DetallesBienes_EsDatoPrueba DEFAULT (0),
    Version                 int             NOT NULL CONSTRAINT DF_DetallesBienes_Version DEFAULT (1),
    CONSTRAINT PK_DetallesBienes PRIMARY KEY (Id),
    CONSTRAINT FK_DetallesBienes_Solicitud FOREIGN KEY (SolicitudId) REFERENCES plantillas.Solicitudes (Id),
    CONSTRAINT CK_DetallesBienes_Cantidad CHECK (Cantidad >= 0),
    CONSTRAINT CK_DetallesBienes_Valor CHECK (ValorEstimado IS NULL OR ValorEstimado >= 0),
    CONSTRAINT CK_DetallesBienes_Monto CHECK (MontoAutorizado IS NULL OR MontoAutorizado >= 0)
);

CREATE INDEX IX_DetallesBienes_Solicitud ON plantillas.DetallesBienes (SolicitudId);

COMMIT TRANSACTION;
GO

/* ---------------------------------------------------------------------------------------------------------------------
   8. Protección de la auditoría (regla 15: nada se borra ni se reescribe)
   --------------------------------------------------------------------------------------------------------------------- */

-- Historial, bitácora, presentaciones, devoluciones y observaciones: solo se insertan.
CREATE OR ALTER TRIGGER plantillas.TR_SoloInsercion_Historial ON plantillas.Historial AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'El historial no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER plantillas.TR_SoloInsercion_Movimientos ON plantillas.Movimientos AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'La bitácora del documento no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER plantillas.TR_SoloInsercion_Presentaciones ON plantillas.Presentaciones AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una presentación enviada no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER plantillas.TR_SoloInsercion_Devoluciones ON plantillas.Devoluciones AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una devolución no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER plantillas.TR_SoloInsercion_Observaciones ON plantillas.Observaciones AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una observación no se modifica ni se elimina.', 1;
END;
GO

-- Una versión publicada no se elimina, y de ella solo cambian la vigencia, el fin de vigencia y la versión de registro.
CREATE OR ALTER TRIGGER plantillas.TR_VersionesPlantilla_Inmutable ON plantillas.VersionesPlantilla AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted)
        THROW 50015, N'Las versiones publicadas no se eliminan.', 1;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE i.PlantillaId <> d.PlantillaId
           OR i.NumeroVersion <> d.NumeroVersion
           OR i.Contenido <> d.Contenido
           OR i.Huella <> d.Huella
           OR ISNULL(i.VersionAnteriorId, '') <> ISNULL(d.VersionAnteriorId, '')
           OR ISNULL(i.ConsolidadoId, '') <> ISNULL(d.ConsolidadoId, '')
           OR i.CargaInicial <> d.CargaInicial
           OR i.PublicadaPor <> d.PublicadaPor
           OR i.FechaPublicacion <> d.FechaPublicacion
           OR i.InicioVigencia <> d.InicioVigencia
           OR (d.Vigencia = 'NoVigente' AND i.Vigencia = 'Vigente'))
        THROW 50015, N'Una versión publicada es inmutable: solo puede pasar de VIGENTE a NO VIGENTE.', 1;
END;
GO

-- Un documento aprobado no cambia de estado ni se elimina; un documento solo se elimina si nunca se envió.
CREATE OR ALTER TRIGGER plantillas.TR_Consolidados_Aprobado ON plantillas.Consolidados AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id
               WHERE d.Estado = 'Aprobado' AND (i.Id IS NULL OR i.Estado <> 'Aprobado'))
        THROW 50017, N'Un Documento Consolidado aprobado no cambia de estado ni se elimina.', 1;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.Id = d.Id
               WHERE i.Id IS NULL AND d.NumeroPresentacion > 0)
        THROW 50017, N'Un Documento Consolidado que ya se envió no se elimina.', 1;
END;
GO

/* ---------------------------------------------------------------------------------------------------------------------
   9. Permisos del usuario de la aplicación (ajustar el nombre del usuario o rol institucional)
   --------------------------------------------------------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'rol_editor_plantillas') IS NULL
    CREATE ROLE rol_editor_plantillas;
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::plantillas TO rol_editor_plantillas;
DENY UPDATE, DELETE ON plantillas.Historial TO rol_editor_plantillas;
DENY UPDATE, DELETE ON plantillas.Movimientos TO rol_editor_plantillas;
DENY UPDATE, DELETE ON plantillas.Presentaciones TO rol_editor_plantillas;
DENY UPDATE, DELETE ON plantillas.Devoluciones TO rol_editor_plantillas;
DENY UPDATE, DELETE ON plantillas.Observaciones TO rol_editor_plantillas;
DENY DELETE ON plantillas.VersionesPlantilla TO rol_editor_plantillas;
-- ALTER ROLE rol_editor_plantillas ADD MEMBER [usuario_de_la_aplicacion];
GO
