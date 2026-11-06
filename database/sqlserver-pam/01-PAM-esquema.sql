/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Esquema para SQL Server 2019 con tablas PAM_*
   ---------------------------------------------------------------------------------------------------------------------
   Generado por generar-pam.js a partir de ../sqlserver/01-esquema.sql. No lo edite a mano.
   - Crea 28 tablas en el esquema dbo, todas con el prefijo PAM_.
   - Los identificadores son los mismos de los archivos JSON (por ejemplo, PLA-001, CON-0001).
   - Los valores de las enumeraciones se guardan como texto, igual que en el JSON ('EnRegistro', 'Vigente'…).
   - El contenido de una versión o propuesta (diseño, bloques y párrafos) se guarda como JSON.
   - La columna Version es el control de concurrencia optimista de la aplicación.
   Se ejecuta una vez. Después ejecute 02-PAM-datos.sql.
   ===================================================================================================================== */

-- Base de datos recomendada (ejecutar aparte, con un usuario administrador):
-- CREATE DATABASE PlantillasDGEFFA COLLATE Modern_Spanish_CI_AI;
-- ALTER DATABASE PlantillasDGEFFA SET READ_COMMITTED_SNAPSHOT ON;
-- ALTER DATABASE PlantillasDGEFFA SET ALLOW_SNAPSHOT_ISOLATION ON;

-- Los índices filtrados los exigen; sqlcmd trae QUOTED_IDENTIFIER apagado de forma predeterminada.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------------------------------------------------------
   1. Usuarios y roles
   En producción la identidad viene del proveedor institucional; esta tabla es el espejo que usan la bandeja (D10),
   la separación de funciones y la auditoría.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.PAM_USUARIOS (
    Id              varchar(60)     NOT NULL,
    NombreCompleto  nvarchar(150)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_Usuarios_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_Usuarios_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Usuarios PRIMARY KEY (Id)
);

CREATE TABLE dbo.PAM_USUARIO_ROLES (
    UsuarioId       varchar(60)     NOT NULL,
    Rol             varchar(20)     NOT NULL,
    CONSTRAINT PK_PAM_UsuarioRoles PRIMARY KEY (UsuarioId, Rol),
    CONSTRAINT FK_PAM_UsuarioRoles_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT CK_PAM_UsuarioRoles_Rol CHECK (Rol IN ('Analista', 'OficialJuridico', 'Administrador'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   2. Catálogos
   --------------------------------------------------------------------------------------------------------------------- */
-- Unidades que elaboran sus propias plantillas: Exoneraciones, Dispensas, Registro Exonerados, Notas de Crédito y Devoluciones.
CREATE TABLE dbo.PAM_UNIDADES (
    Id              varchar(40)     NOT NULL,           -- por ejemplo exoneraciones
    Nombre          nvarchar(150)   NOT NULL,
    Orden           int             NOT NULL,           -- posición en el menú
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_Unidades_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_Unidades_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Unidades PRIMARY KEY (Id),
    CONSTRAINT UQ_PAM_Unidades_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_PAM_Unidades_Orden CHECK (Orden BETWEEN 1 AND 100)
);

CREATE TABLE dbo.PAM_RUBROS (
    Id              varchar(20)     NOT NULL,           -- código institucional, por ejemplo 1000
    Nombre          nvarchar(300)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_Rubros_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_Rubros_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Rubros PRIMARY KEY (Id)
);

CREATE TABLE dbo.PAM_SUB_RUBROS (
    Id              varchar(20)     NOT NULL,
    RubroId         varchar(20)     NOT NULL,
    Nombre          nvarchar(300)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_SubRubros_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_SubRubros_Version DEFAULT (1),
    CONSTRAINT PK_PAM_SubRubros PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_SubRubros_Rubro FOREIGN KEY (RubroId) REFERENCES dbo.PAM_RUBROS (Id),
    -- Permite exigir en otras tablas que el sub rubro pertenezca a su rubro.
    CONSTRAINT UQ_PAM_SubRubros_Rubro_Id UNIQUE (RubroId, Id)
);

-- Lista «Documento»: Original, Modificación, Aclaratoria.
CREATE TABLE dbo.PAM_CLASES_DOCUMENTO (
    Id              varchar(20)     NOT NULL,
    Nombre          nvarchar(150)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_ClasesDocumento_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_ClasesDocumento_Version DEFAULT (1),
    CONSTRAINT PK_PAM_ClasesDocumento PRIMARY KEY (Id),
    CONSTRAINT UQ_PAM_ClasesDocumento_Nombre UNIQUE (Nombre)
);

-- Lista «Beneficios»: ISV, ISR, ACPV, Aportación Solidaria.
CREATE TABLE dbo.PAM_BENEFICIOS (
    Id              varchar(20)     NOT NULL,
    Nombre          nvarchar(150)   NOT NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_Beneficios_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_Beneficios_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Beneficios PRIMARY KEY (Id),
    CONSTRAINT UQ_PAM_Beneficios_Nombre UNIQUE (Nombre)
);

-- Catálogo de bloques documentales: CABECERA, VISTOS, CONSIDERANDO, FUNDAMENTACION_LEGAL…
CREATE TABLE dbo.PAM_BLOQUES_DOCUMENTALES (
    Id              varchar(40)     NOT NULL,
    Nombre          nvarchar(80)    NOT NULL,
    Titulo          nvarchar(80)    NULL,
    Numeracion      varchar(15)     NOT NULL CONSTRAINT DF_PAM_BloquesDocumentales_Numeracion DEFAULT ('Ninguna'),
    EsFirmas        bit             NOT NULL CONSTRAINT DF_PAM_BloquesDocumentales_EsFirmas DEFAULT (0),
    ContextoCampos  varchar(20)     NOT NULL,
    Descripcion     nvarchar(300)   NULL,
    Activo          bit             NOT NULL CONSTRAINT DF_PAM_BloquesDocumentales_Activo DEFAULT (1),
    Version         int             NOT NULL CONSTRAINT DF_PAM_BloquesDocumentales_Version DEFAULT (1),
    CONSTRAINT PK_PAM_BloquesDocumentales PRIMARY KEY (Id),
    CONSTRAINT CK_PAM_BloquesDocumentales_Numeracion CHECK (Numeracion IN ('Ninguna', 'Considerando', 'Ordinal')),
    CONSTRAINT CK_PAM_BloquesDocumentales_Contexto CHECK (ContextoCampos IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas'))
);

-- Bloque nuevo que recibe el contenido de uno anterior cuando un tipo documental cambia de formato
-- (por ejemplo, en el Dictamen, Dictamina reemplaza a Resuelve).
CREATE TABLE dbo.PAM_BLOQUE_REEMPLAZOS (
    BloqueId            varchar(40)     NOT NULL,
    ReemplazaBloqueId   varchar(40)     NOT NULL,
    CONSTRAINT PK_PAM_BloqueReemplazos PRIMARY KEY (BloqueId, ReemplazaBloqueId),
    CONSTRAINT FK_PAM_BloqueReemplazos_Bloque FOREIGN KEY (BloqueId) REFERENCES dbo.PAM_BLOQUES_DOCUMENTALES (Id),
    CONSTRAINT FK_PAM_BloqueReemplazos_Anterior FOREIGN KEY (ReemplazaBloqueId) REFERENCES dbo.PAM_BLOQUES_DOCUMENTALES (Id),
    CONSTRAINT CK_PAM_BloqueReemplazos_Distinto CHECK (BloqueId <> ReemplazaBloqueId)
);

-- Resolución, Certificación, Dictamen, Constancia de Obligados Tributarios.
CREATE TABLE dbo.PAM_TIPOS_DOCUMENTO (
    Id                      varchar(20)     NOT NULL,
    Nombre                  nvarchar(150)   NOT NULL,
    TextoCorrido            bit             NOT NULL CONSTRAINT DF_PAM_TiposDocumento_TextoCorrido DEFAULT (0),
    CopiaDeTipoDocumentoId  varchar(20)     NULL,       -- la Certificación es copia de la Resolución (D3)
    Activo                  bit             NOT NULL CONSTRAINT DF_PAM_TiposDocumento_Activo DEFAULT (1),
    Version                 int             NOT NULL CONSTRAINT DF_PAM_TiposDocumento_Version DEFAULT (1),
    CONSTRAINT PK_PAM_TiposDocumento PRIMARY KEY (Id),
    CONSTRAINT UQ_PAM_TiposDocumento_Nombre UNIQUE (Nombre),
    CONSTRAINT FK_PAM_TiposDocumento_CopiaDe FOREIGN KEY (CopiaDeTipoDocumentoId) REFERENCES dbo.PAM_TIPOS_DOCUMENTO (Id),
    CONSTRAINT CK_PAM_TiposDocumento_CopiaDistinta CHECK (CopiaDeTipoDocumentoId IS NULL OR CopiaDeTipoDocumentoId <> Id)
);

-- Bloques de cada tipo documental, con su orden y si son obligatorios (D2).
CREATE TABLE dbo.PAM_TIPO_DOCUMENTO_BLOQUES (
    TipoDocumentoId varchar(20)     NOT NULL,
    BloqueId        varchar(40)     NOT NULL,
    Orden           int             NOT NULL,
    Obligatorio     bit             NOT NULL CONSTRAINT DF_PAM_TipoDocumentoBloques_Obligatorio DEFAULT (0),
    CONSTRAINT PK_PAM_TipoDocumentoBloques PRIMARY KEY (TipoDocumentoId, BloqueId),
    CONSTRAINT FK_PAM_TipoDocumentoBloques_Tipo FOREIGN KEY (TipoDocumentoId) REFERENCES dbo.PAM_TIPOS_DOCUMENTO (Id),
    CONSTRAINT FK_PAM_TipoDocumentoBloques_Bloque FOREIGN KEY (BloqueId) REFERENCES dbo.PAM_BLOQUES_DOCUMENTALES (Id),
    CONSTRAINT UQ_PAM_TipoDocumentoBloques_Orden UNIQUE (TipoDocumentoId, Orden),
    CONSTRAINT CK_PAM_TipoDocumentoBloques_Orden CHECK (Orden >= 1)
);

-- Campos dinámicos. Su origen sale de una lista cerrada de colecciones y propiedades (la valida la aplicación).
CREATE TABLE dbo.PAM_CAMPOS (
    Id                  varchar(60)     NOT NULL,
    Etiqueta            nvarchar(100)   NOT NULL,
    Categoria           nvarchar(100)   NOT NULL,
    TipoDato            varchar(10)     NOT NULL,
    OrigenColeccion     varchar(40)     NOT NULL,
    OrigenPropiedad     varchar(60)     NOT NULL,
    Descripcion         nvarchar(300)   NULL,
    AlertarCambios      bit             NOT NULL CONSTRAINT DF_PAM_Campos_AlertarCambios DEFAULT (0),  -- avisar al aprobador (Operador)
    Version             int             NOT NULL CONSTRAINT DF_PAM_Campos_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Campos PRIMARY KEY (Id),
    CONSTRAINT CK_PAM_Campos_TipoDato CHECK (TipoDato IN ('Texto', 'Numero', 'Moneda', 'Fecha', 'Booleano'))
);

-- Bloques (contextos) en los que se puede usar cada campo.
CREATE TABLE dbo.PAM_CAMPO_CONTEXTOS (
    CampoId         varchar(60)     NOT NULL,
    Contexto        varchar(20)     NOT NULL,
    CONSTRAINT PK_PAM_CampoContextos PRIMARY KEY (CampoId, Contexto),
    CONSTRAINT FK_PAM_CampoContextos_Campo FOREIGN KEY (CampoId) REFERENCES dbo.PAM_CAMPOS (Id) ON DELETE CASCADE,
    CONSTRAINT CK_PAM_CampoContextos_Contexto CHECK (Contexto IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas', 'tabla'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   3. Archivos (logos). Hasta 1 MB, PNG o JPEG (lo valida la aplicación por el contenido real).
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.PAM_ARCHIVOS (
    Id              varchar(40)     NOT NULL,
    NombreOriginal  nvarchar(150)   NOT NULL,
    TipoContenido   varchar(40)     NOT NULL,
    Tamano          int             NOT NULL,
    Sha256          char(64)        NOT NULL,
    FechaCarga      datetimeoffset(7) NOT NULL,
    Contenido       varbinary(max)  NOT NULL,
    Version         int             NOT NULL CONSTRAINT DF_PAM_Archivos_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Archivos PRIMARY KEY (Id),
    CONSTRAINT CK_PAM_Archivos_Tipo CHECK (TipoContenido IN ('image/png', 'image/jpeg')),
    CONSTRAINT CK_PAM_Archivos_Tamano CHECK (Tamano BETWEEN 1 AND 1048576)
);

/* ---------------------------------------------------------------------------------------------------------------------
   4. Plantillas (identidad) y sus versiones publicadas
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.PAM_PLANTILLAS (
    Id                  varchar(40)     NOT NULL,
    Nombre              nvarchar(200)   NOT NULL,
    NombreReferencia    nvarchar(200)   NULL,           -- «Nombre Plantilla» que escribe el analista
    UnidadId            varchar(40)     NOT NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoDocumentoId     varchar(20)     NULL,
    ClaseDocumentoId    varchar(20)     NULL,
    BeneficioId         varchar(20)     NULL,
    Estado              varchar(10)     NOT NULL CONSTRAINT DF_PAM_Plantillas_Estado DEFAULT ('Borrador'),
    Descripcion         nvarchar(500)   NULL,
    LogoId              varchar(40)     NULL,
    AlineacionLogo      varchar(10)     NOT NULL CONSTRAINT DF_PAM_Plantillas_AlineacionLogo DEFAULT ('Centro'),
    PiePagina           nvarchar(500)   NULL,
    EspacioFirmasCm     float           NOT NULL CONSTRAINT DF_PAM_Plantillas_EspacioFirmasCm DEFAULT (1.2),
    FechaModificacion   datetimeoffset(7) NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_PAM_Plantillas_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Plantillas PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Plantillas_Unidad FOREIGN KEY (UnidadId) REFERENCES dbo.PAM_UNIDADES (Id),
    CONSTRAINT FK_PAM_Plantillas_SubRubro FOREIGN KEY (RubroId, SubRubroId) REFERENCES dbo.PAM_SUB_RUBROS (RubroId, Id),
    CONSTRAINT FK_PAM_Plantillas_Tipo FOREIGN KEY (TipoDocumentoId) REFERENCES dbo.PAM_TIPOS_DOCUMENTO (Id),
    CONSTRAINT FK_PAM_Plantillas_Clase FOREIGN KEY (ClaseDocumentoId) REFERENCES dbo.PAM_CLASES_DOCUMENTO (Id),
    CONSTRAINT FK_PAM_Plantillas_Beneficio FOREIGN KEY (BeneficioId) REFERENCES dbo.PAM_BENEFICIOS (Id),
    CONSTRAINT FK_PAM_Plantillas_Logo FOREIGN KEY (LogoId) REFERENCES dbo.PAM_ARCHIVOS (Id),
    CONSTRAINT CK_PAM_Plantillas_Estado CHECK (Estado IN ('Borrador', 'Publicada')),
    CONSTRAINT CK_PAM_Plantillas_AlineacionLogo CHECK (AlineacionLogo IN ('Izquierda', 'Centro', 'Derecha')),
    CONSTRAINT CK_PAM_Plantillas_EspacioFirmas CHECK (EspacioFirmasCm BETWEEN 0 AND 10),
    -- Identidad funcional: Unidad + Rubro + Sub Rubro + Tipo Documento + Documento + Beneficios identifican una única plantilla.
    -- Tipo Documento, Documento y Beneficios son opcionales: NULL significa que sirve para cualquier valor (UNIQUE trata los NULL como iguales).
    CONSTRAINT UQ_PAM_Plantillas_Identidad UNIQUE (UnidadId, RubroId, SubRubroId, TipoDocumentoId, ClaseDocumentoId, BeneficioId)
);

-- Párrafos anteriores a las versiones publicadas: la aplicación los usa para la carga inicial de cada plantilla.
CREATE TABLE dbo.PAM_PARRAFOS (
    Id                  varchar(40)     NOT NULL,
    PlantillaId         varchar(40)     NOT NULL,
    Seccion             varchar(15)     NOT NULL,
    Orden               int             NOT NULL,
    Contenido           nvarchar(max)   NOT NULL,       -- JSON del editor
    Activo              bit             NOT NULL CONSTRAINT DF_PAM_Parrafos_Activo DEFAULT (1),
    MostrarSoloSiCumple bit             NOT NULL CONSTRAINT DF_PAM_Parrafos_MostrarSoloSiCumple DEFAULT (0),
    UnirConAnterior     bit             NOT NULL CONSTRAINT DF_PAM_Parrafos_UnirConAnterior DEFAULT (0),
    EsTitulo            bit             NOT NULL CONSTRAINT DF_PAM_Parrafos_EsTitulo DEFAULT (0),
    Condicion           nvarchar(max)   NULL,           -- JSON del grupo de condiciones
    CamposUsados        nvarchar(max)   NOT NULL CONSTRAINT DF_PAM_Parrafos_CamposUsados DEFAULT (N'[]'),   -- JSON: arreglo de campos
    OrigenParrafoId     varchar(40)     NULL,           -- párrafo del que se copió (puede ser de otra plantilla)
    FechaModificacion   datetimeoffset(7) NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_PAM_Parrafos_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Parrafos PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Parrafos_Plantilla FOREIGN KEY (PlantillaId) REFERENCES dbo.PAM_PLANTILLAS (Id),
    CONSTRAINT CK_PAM_Parrafos_Seccion CHECK (Seccion IN ('Cabecera', 'Considerando', 'PorTanto', 'Resuelve', 'Firmas')),
    CONSTRAINT CK_PAM_Parrafos_Orden CHECK (Orden BETWEEN 0 AND 10000),
    CONSTRAINT CK_PAM_Parrafos_Contenido CHECK (ISJSON(Contenido) = 1),
    CONSTRAINT CK_PAM_Parrafos_Condicion CHECK (Condicion IS NULL OR ISJSON(Condicion) = 1),
    CONSTRAINT CK_PAM_Parrafos_CamposUsados CHECK (ISJSON(CamposUsados) = 1)
);

CREATE INDEX IX_PAM_Parrafos_Plantilla ON dbo.PAM_PARRAFOS (PlantillaId, Seccion, Orden);

CREATE TABLE dbo.PAM_VERSIONES_PLANTILLA (
    Id                  varchar(40)     NOT NULL,
    PlantillaId         varchar(40)     NOT NULL,
    NumeroVersion       int             NOT NULL,
    Vigencia            varchar(10)     NOT NULL,
    Contenido           nvarchar(max)   NOT NULL,       -- JSON: diseño, bloques y párrafos (inmutable)
    Huella              char(64)        NOT NULL,       -- SHA-256 del contenido
    VersionAnteriorId   varchar(40)     NULL,
    ConsolidadoId       varchar(40)     NULL,           -- documento que la originó; nulo en la carga inicial
    CargaInicial        bit             NOT NULL CONSTRAINT DF_PAM_VersionesPlantilla_CargaInicial DEFAULT (0),
    PublicadaPor        varchar(60)     NOT NULL,
    FechaPublicacion    datetimeoffset(7) NOT NULL,
    InicioVigencia      datetimeoffset(7) NOT NULL,
    FinVigencia         datetimeoffset(7) NULL,
    Version             int             NOT NULL CONSTRAINT DF_PAM_VersionesPlantilla_Version DEFAULT (1),
    CONSTRAINT PK_PAM_VersionesPlantilla PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_VersionesPlantilla_Plantilla FOREIGN KEY (PlantillaId) REFERENCES dbo.PAM_PLANTILLAS (Id),
    CONSTRAINT FK_PAM_VersionesPlantilla_Anterior FOREIGN KEY (VersionAnteriorId) REFERENCES dbo.PAM_VERSIONES_PLANTILLA (Id),
    CONSTRAINT UQ_PAM_VersionesPlantilla_Numero UNIQUE (PlantillaId, NumeroVersion),
    CONSTRAINT CK_PAM_VersionesPlantilla_Vigencia CHECK (Vigencia IN ('Vigente', 'NoVigente')),
    CONSTRAINT CK_PAM_VersionesPlantilla_Contenido CHECK (ISJSON(Contenido) = 1),
    CONSTRAINT CK_PAM_VersionesPlantilla_Numero CHECK (NumeroVersion >= 1),
    -- Una versión vigente no tiene fin de vigencia; una no vigente sí.
    CONSTRAINT CK_PAM_VersionesPlantilla_Fin CHECK ((Vigencia = 'Vigente' AND FinVigencia IS NULL) OR (Vigencia = 'NoVigente' AND FinVigencia IS NOT NULL)),
    CONSTRAINT CK_PAM_VersionesPlantilla_Origen CHECK (CargaInicial = 1 OR ConsolidadoId IS NOT NULL)
);

-- Regla 14: como máximo una versión vigente por plantilla, garantizado por el motor.
CREATE UNIQUE INDEX UX_PAM_VersionesPlantilla_Vigente ON dbo.PAM_VERSIONES_PLANTILLA (PlantillaId) WHERE Vigencia = 'Vigente';

/* ---------------------------------------------------------------------------------------------------------------------
   5. Documento Consolidado (PLAN-AAAANNNN)
   --------------------------------------------------------------------------------------------------------------------- */
-- Último número usado por año. Se incrementa con UPDLOCK, HOLDLOCK dentro de la transacción que crea el documento.
CREATE TABLE dbo.PAM_SECUENCIAS_PLAN (
    Anio            int             NOT NULL,
    Ultimo          int             NOT NULL CONSTRAINT DF_PAM_SecuenciasPlan_Ultimo DEFAULT (0),
    CONSTRAINT PK_PAM_SecuenciasPlan PRIMARY KEY (Anio),
    CONSTRAINT CK_PAM_SecuenciasPlan_Ultimo CHECK (Ultimo BETWEEN 0 AND 9999)
);

CREATE TABLE dbo.PAM_CONSOLIDADOS (
    Id                  varchar(40)     NOT NULL,
    Numero              char(13)        NOT NULL,
    Anio                int             NOT NULL,
    Secuencia           int             NOT NULL,
    Estado              varchar(12)     NOT NULL CONSTRAINT DF_PAM_Consolidados_Estado DEFAULT ('EnRegistro'),
    UnidadId            varchar(40)     NOT NULL,       -- solo contiene plantillas de su unidad
    AnalistaId          varchar(60)     NOT NULL,
    FechaCreacion       datetimeoffset(7) NOT NULL,
    FechaActualizacion  datetimeoffset(7) NOT NULL,
    NumeroPresentacion  int             NOT NULL CONSTRAINT DF_PAM_Consolidados_NumeroPresentacion DEFAULT (0),
    FechaEnvio          datetimeoffset(7) NULL,
    EnviadoPor          varchar(60)     NULL,
    RevisorId           varchar(60)     NULL,
    FechaRevision       datetimeoffset(7) NULL,
    AprobadorId         varchar(60)     NULL,
    FechaAprobacion     datetimeoffset(7) NULL,
    Version             int             NOT NULL CONSTRAINT DF_PAM_Consolidados_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Consolidados PRIMARY KEY (Id),
    CONSTRAINT UQ_PAM_Consolidados_Numero UNIQUE (Numero),
    CONSTRAINT UQ_PAM_Consolidados_Secuencia UNIQUE (Anio, Secuencia),
    CONSTRAINT FK_PAM_Consolidados_Unidad FOREIGN KEY (UnidadId) REFERENCES dbo.PAM_UNIDADES (Id),
    CONSTRAINT FK_PAM_Consolidados_Analista FOREIGN KEY (AnalistaId) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT FK_PAM_Consolidados_EnviadoPor FOREIGN KEY (EnviadoPor) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT FK_PAM_Consolidados_Revisor FOREIGN KEY (RevisorId) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT FK_PAM_Consolidados_Aprobador FOREIGN KEY (AprobadorId) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT CK_PAM_Consolidados_Estado CHECK (Estado IN ('EnRegistro', 'Verificado', 'Aprobado', 'Eliminado')),
    CONSTRAINT CK_PAM_Consolidados_Secuencia CHECK (Secuencia BETWEEN 1 AND 9999),
    CONSTRAINT CK_PAM_Consolidados_Anio CHECK (Anio BETWEEN 2000 AND 2999),
    -- El número se deriva del año y la secuencia: PLAN-20260001.
    CONSTRAINT CK_PAM_Consolidados_Numero CHECK (Numero = CONCAT('PLAN-', Anio, RIGHT(CONCAT('000', Secuencia), 4))),
    -- Lo aprobado tiene aprobador y fecha, y nunca lo aprueba quien lo preparó (separación de funciones).
    CONSTRAINT CK_PAM_Consolidados_Aprobacion CHECK (Estado <> 'Aprobado' OR (AprobadorId IS NOT NULL AND FechaAprobacion IS NOT NULL)),
    CONSTRAINT CK_PAM_Consolidados_Separacion CHECK (AprobadorId IS NULL OR (AprobadorId <> AnalistaId AND (EnviadoPor IS NULL OR AprobadorId <> EnviadoPor)))
);

CREATE INDEX IX_PAM_Consolidados_Bandeja ON dbo.PAM_CONSOLIDADOS (UnidadId, Estado, AnalistaId) INCLUDE (Numero, FechaActualizacion);

CREATE TABLE dbo.PAM_PROPUESTAS (
    Id                      varchar(40)     NOT NULL,
    ConsolidadoId           varchar(40)     NOT NULL,
    Operacion               varchar(10)     NOT NULL,
    UnidadId                varchar(40)     NOT NULL,
    RubroId                 varchar(20)     NOT NULL,
    SubRubroId              varchar(20)     NOT NULL,
    TipoDocumentoId         varchar(20)     NULL,
    ClaseDocumentoId        varchar(20)     NULL,
    BeneficioId             varchar(20)     NULL,
    Nombre                  nvarchar(200)   NOT NULL,
    NombreReferencia        nvarchar(200)   NULL,       -- «Nombre Plantilla» que escribe el analista
    PlantillaId             varchar(40)     NULL,       -- obligatorio en MODIFICAR
    VersionOrigenId         varchar(40)     NULL,       -- versión vigente de la que partió (MODIFICAR)
    HuellaOrigen            char(64)        NULL,
    ContenidoPropuesto      nvarchar(max)   NOT NULL,   -- JSON con el mismo formato del contenido de una versión
    DerivadaDePropuestaId   varchar(40)     NULL,       -- Certificación derivada de su Resolución (D3)
    Retirada                bit             NOT NULL CONSTRAINT DF_PAM_Propuestas_Retirada DEFAULT (0),
    MotivoRetiro            nvarchar(500)   NULL,
    FechaCreacion           datetimeoffset(7) NOT NULL,
    FechaModificacion       datetimeoffset(7) NOT NULL,
    Version                 int             NOT NULL CONSTRAINT DF_PAM_Propuestas_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Propuestas PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Propuestas_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES dbo.PAM_CONSOLIDADOS (Id),
    CONSTRAINT FK_PAM_Propuestas_Unidad FOREIGN KEY (UnidadId) REFERENCES dbo.PAM_UNIDADES (Id),
    CONSTRAINT FK_PAM_Propuestas_SubRubro FOREIGN KEY (RubroId, SubRubroId) REFERENCES dbo.PAM_SUB_RUBROS (RubroId, Id),
    CONSTRAINT FK_PAM_Propuestas_Tipo FOREIGN KEY (TipoDocumentoId) REFERENCES dbo.PAM_TIPOS_DOCUMENTO (Id),
    CONSTRAINT FK_PAM_Propuestas_Clase FOREIGN KEY (ClaseDocumentoId) REFERENCES dbo.PAM_CLASES_DOCUMENTO (Id),
    CONSTRAINT FK_PAM_Propuestas_Beneficio FOREIGN KEY (BeneficioId) REFERENCES dbo.PAM_BENEFICIOS (Id),
    CONSTRAINT FK_PAM_Propuestas_Plantilla FOREIGN KEY (PlantillaId) REFERENCES dbo.PAM_PLANTILLAS (Id),
    CONSTRAINT FK_PAM_Propuestas_VersionOrigen FOREIGN KEY (VersionOrigenId) REFERENCES dbo.PAM_VERSIONES_PLANTILLA (Id),
    CONSTRAINT FK_PAM_Propuestas_Derivada FOREIGN KEY (DerivadaDePropuestaId) REFERENCES dbo.PAM_PROPUESTAS (Id),
    CONSTRAINT CK_PAM_Propuestas_Operacion CHECK (Operacion IN ('Crear', 'Modificar')),
    CONSTRAINT CK_PAM_Propuestas_Contenido CHECK (ISJSON(ContenidoPropuesto) = 1),
    CONSTRAINT CK_PAM_Propuestas_Modificar CHECK (Operacion <> 'Modificar' OR (PlantillaId IS NOT NULL AND VersionOrigenId IS NOT NULL)),
    CONSTRAINT CK_PAM_Propuestas_Retiro CHECK (Retirada = 0 OR (MotivoRetiro IS NOT NULL AND LEN(MotivoRetiro) >= 5))
);

CREATE INDEX IX_PAM_Propuestas_Consolidado ON dbo.PAM_PROPUESTAS (ConsolidadoId);
CREATE INDEX IX_PAM_Propuestas_Plantilla ON dbo.PAM_PROPUESTAS (PlantillaId) WHERE PlantillaId IS NOT NULL;

ALTER TABLE dbo.PAM_VERSIONES_PLANTILLA
    ADD CONSTRAINT FK_PAM_VersionesPlantilla_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES dbo.PAM_CONSOLIDADOS (Id);

-- D4: una plantilla (por su identidad funcional) solo puede estar en un documento abierto. La aplicación inserta la
-- fila al agregar la propuesta y la borra al retirarla o al aprobar el documento.
CREATE TABLE dbo.PAM_BLOQUEOS_PLANTILLA (
    UnidadId            varchar(40)     NOT NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoDocumentoId     varchar(20)     NULL,
    ClaseDocumentoId    varchar(20)     NULL,
    BeneficioId         varchar(20)     NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    PropuestaId         varchar(40)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_PAM_BloqueosPlantilla PRIMARY KEY (PropuestaId),
    CONSTRAINT UQ_PAM_BloqueosPlantilla_Identidad UNIQUE (UnidadId, RubroId, SubRubroId, TipoDocumentoId, ClaseDocumentoId, BeneficioId),
    CONSTRAINT FK_PAM_BloqueosPlantilla_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES dbo.PAM_CONSOLIDADOS (Id),
    CONSTRAINT FK_PAM_BloqueosPlantilla_Propuesta FOREIGN KEY (PropuestaId) REFERENCES dbo.PAM_PROPUESTAS (Id)
);

-- Cada envío a verificación, con la copia congelada de lo enviado y su huella de integridad.
CREATE TABLE dbo.PAM_PRESENTACIONES (
    Id                  varchar(40)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    Numero              int             NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    CantidadPlantillas  int             NOT NULL,
    HuellaIntegridad    char(64)        NOT NULL,
    Propuestas          nvarchar(max)   NOT NULL,       -- JSON: arreglo de las propuestas tal como se enviaron
    Version             int             NOT NULL CONSTRAINT DF_PAM_Presentaciones_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Presentaciones PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Presentaciones_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES dbo.PAM_CONSOLIDADOS (Id),
    CONSTRAINT FK_PAM_Presentaciones_Usuario FOREIGN KEY (Usuario) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT UQ_PAM_Presentaciones_Numero UNIQUE (ConsolidadoId, Numero),
    CONSTRAINT CK_PAM_Presentaciones_Numero CHECK (Numero >= 1),
    CONSTRAINT CK_PAM_Presentaciones_Cantidad CHECK (CantidadPlantillas >= 1),
    CONSTRAINT CK_PAM_Presentaciones_Propuestas CHECK (ISJSON(Propuestas) = 1)
);

CREATE TABLE dbo.PAM_DEVOLUCIONES (
    Id                  varchar(40)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    NumeroPresentacion  int             NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    Justificacion       nvarchar(2000)  NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_PAM_Devoluciones_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Devoluciones PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Devoluciones_Presentacion FOREIGN KEY (ConsolidadoId, NumeroPresentacion) REFERENCES dbo.PAM_PRESENTACIONES (ConsolidadoId, Numero),
    CONSTRAINT FK_PAM_Devoluciones_Usuario FOREIGN KEY (Usuario) REFERENCES dbo.PAM_USUARIOS (Id),
    CONSTRAINT UQ_PAM_Devoluciones_Presentacion UNIQUE (ConsolidadoId, NumeroPresentacion),
    CONSTRAINT CK_PAM_Devoluciones_Justificacion CHECK (LEN(LTRIM(RTRIM(Justificacion))) > 0)
);

CREATE TABLE dbo.PAM_OBSERVACIONES (
    Id                  int IDENTITY(1, 1) NOT NULL,
    DevolucionId        varchar(40)     NOT NULL,
    Orden               int             NOT NULL,
    PropuestaId         varchar(40)     NOT NULL,
    BloqueCodigo        varchar(40)     NULL,
    ParrafoId           varchar(40)     NULL,
    Problema            nvarchar(2000)  NOT NULL,
    Recomendacion       nvarchar(2000)  NULL,
    CONSTRAINT PK_PAM_Observaciones PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Observaciones_Devolucion FOREIGN KEY (DevolucionId) REFERENCES dbo.PAM_DEVOLUCIONES (Id),
    CONSTRAINT FK_PAM_Observaciones_Propuesta FOREIGN KEY (PropuestaId) REFERENCES dbo.PAM_PROPUESTAS (Id),
    CONSTRAINT UQ_PAM_Observaciones_Orden UNIQUE (DevolucionId, Orden)
);

/* ---------------------------------------------------------------------------------------------------------------------
   6. Auditoría: bitácora del documento e historial técnico (solo se insertan)
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.PAM_MOVIMIENTOS (
    Id                  varchar(40)     NOT NULL,
    ConsolidadoId       varchar(40)     NOT NULL,
    Tipo                varchar(30)     NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    EstadoOrigen        varchar(12)     NULL,
    EstadoDestino       varchar(12)     NULL,
    NumeroPresentacion  int             NULL,
    Detalle             nvarchar(1000)  NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_PAM_Movimientos_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Movimientos PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_Movimientos_Consolidado FOREIGN KEY (ConsolidadoId) REFERENCES dbo.PAM_CONSOLIDADOS (Id),
    CONSTRAINT CK_PAM_Movimientos_Tipo CHECK (Tipo IN ('Creacion', 'PropuestaAgregada', 'PropuestaModificada', 'PropuestaRetirada',
        'EnvioVerificacion', 'Devolucion', 'Aprobacion', 'Publicacion', 'CambioVigencia', 'ErrorPublicacion', 'Reasignacion', 'Eliminacion')),
    CONSTRAINT CK_PAM_Movimientos_EstadoOrigen CHECK (EstadoOrigen IS NULL OR EstadoOrigen IN ('EnRegistro', 'Verificado', 'Aprobado', 'Eliminado')),
    CONSTRAINT CK_PAM_Movimientos_EstadoDestino CHECK (EstadoDestino IS NULL OR EstadoDestino IN ('EnRegistro', 'Verificado', 'Aprobado', 'Eliminado'))
);

CREATE INDEX IX_PAM_Movimientos_Consolidado ON dbo.PAM_MOVIMIENTOS (ConsolidadoId, Fecha);

CREATE TABLE dbo.PAM_HISTORIAL (
    Id                  varchar(40)     NOT NULL,
    Fecha               datetimeoffset(7) NOT NULL,
    Usuario             varchar(60)     NOT NULL,
    Coleccion           varchar(40)     NOT NULL,
    EntidadId           varchar(60)     NOT NULL,
    Accion              varchar(15)     NOT NULL,
    VersionEntidad      int             NOT NULL,
    Anterior            nvarchar(max)   NULL,           -- JSON del estado anterior
    Nuevo               nvarchar(max)   NULL,           -- JSON del estado nuevo
    Version             int             NOT NULL CONSTRAINT DF_PAM_Historial_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Historial PRIMARY KEY (Id),
    CONSTRAINT CK_PAM_Historial_Accion CHECK (Accion IN ('Creacion', 'Actualizacion', 'Eliminacion')),
    CONSTRAINT CK_PAM_Historial_Anterior CHECK (Anterior IS NULL OR ISJSON(Anterior) = 1),
    CONSTRAINT CK_PAM_Historial_Nuevo CHECK (Nuevo IS NULL OR ISJSON(Nuevo) = 1)
);

CREATE INDEX IX_PAM_Historial_Entidad ON dbo.PAM_HISTORIAL (Coleccion, EntidadId, Fecha);
CREATE INDEX IX_PAM_Historial_Fecha ON dbo.PAM_HISTORIAL (Fecha);

/* ---------------------------------------------------------------------------------------------------------------------
   7. Datos de origen de las solicitudes (para sustituir los campos al generar el documento)
   En producción pueden ser vistas o una réplica del sistema de origen; en pruebas se cargan datos ficticios.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.PAM_SOLICITUDES (
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
    EsDatoPrueba        bit             NOT NULL CONSTRAINT DF_PAM_Solicitudes_EsDatoPrueba DEFAULT (0),
    Version             int             NOT NULL CONSTRAINT DF_PAM_Solicitudes_Version DEFAULT (1),
    CONSTRAINT PK_PAM_Solicitudes PRIMARY KEY (Id),
    CONSTRAINT UQ_PAM_Solicitudes_Numero UNIQUE (NumeroSolicitud),
    CONSTRAINT FK_PAM_Solicitudes_SubRubro FOREIGN KEY (RubroId, SubRubroId) REFERENCES dbo.PAM_SUB_RUBROS (RubroId, Id),
    CONSTRAINT CK_PAM_Solicitudes_Anio CHECK (AnioGestion IS NULL OR AnioGestion BETWEEN 2000 AND 2100),
    CONSTRAINT CK_PAM_Solicitudes_Monto CHECK (MontoExoneracion IS NULL OR MontoExoneracion >= 0)
);

CREATE TABLE dbo.PAM_DETALLES_BIENES (
    Id                      varchar(40)     NOT NULL,
    SolicitudId             varchar(40)     NOT NULL,
    CodigoArancelario       varchar(20)     NOT NULL,
    Descripcion             nvarchar(300)   NOT NULL,
    UnidadMedida            nvarchar(50)    NULL,
    Cantidad                decimal(18, 4)  NOT NULL,
    ValorEstimado           decimal(18, 2)  NULL,
    DescripcionArancelaria  nvarchar(300)   NULL,
    MontoAutorizado         decimal(18, 2)  NULL,
    EsDatoPrueba            bit             NOT NULL CONSTRAINT DF_PAM_DetallesBienes_EsDatoPrueba DEFAULT (0),
    Version                 int             NOT NULL CONSTRAINT DF_PAM_DetallesBienes_Version DEFAULT (1),
    CONSTRAINT PK_PAM_DetallesBienes PRIMARY KEY (Id),
    CONSTRAINT FK_PAM_DetallesBienes_Solicitud FOREIGN KEY (SolicitudId) REFERENCES dbo.PAM_SOLICITUDES (Id),
    CONSTRAINT CK_PAM_DetallesBienes_Cantidad CHECK (Cantidad >= 0),
    CONSTRAINT CK_PAM_DetallesBienes_Valor CHECK (ValorEstimado IS NULL OR ValorEstimado >= 0),
    CONSTRAINT CK_PAM_DetallesBienes_Monto CHECK (MontoAutorizado IS NULL OR MontoAutorizado >= 0)
);

CREATE INDEX IX_PAM_DetallesBienes_Solicitud ON dbo.PAM_DETALLES_BIENES (SolicitudId);

COMMIT TRANSACTION;
GO

/* ---------------------------------------------------------------------------------------------------------------------
   8. Protección de la auditoría (regla 15: nada se borra ni se reescribe)
   --------------------------------------------------------------------------------------------------------------------- */

-- Historial, bitácora, presentaciones, devoluciones y observaciones: solo se insertan.
CREATE OR ALTER TRIGGER dbo.TR_PAM_SoloInsercion_Historial ON dbo.PAM_HISTORIAL AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'El historial no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_PAM_SoloInsercion_Movimientos ON dbo.PAM_MOVIMIENTOS AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'La bitácora del documento no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_PAM_SoloInsercion_Presentaciones ON dbo.PAM_PRESENTACIONES AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una presentación enviada no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_PAM_SoloInsercion_Devoluciones ON dbo.PAM_DEVOLUCIONES AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una devolución no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_PAM_SoloInsercion_Observaciones ON dbo.PAM_OBSERVACIONES AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una observación no se modifica ni se elimina.', 1;
END;
GO

-- Una versión publicada no se elimina, y de ella solo cambian la vigencia, el fin de vigencia y la versión de registro.
CREATE OR ALTER TRIGGER dbo.TR_PAM_VersionesPlantilla_Inmutable ON dbo.PAM_VERSIONES_PLANTILLA AFTER UPDATE, DELETE AS
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
CREATE OR ALTER TRIGGER dbo.TR_PAM_Consolidados_Aprobado ON dbo.PAM_CONSOLIDADOS AFTER UPDATE, DELETE AS
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
IF DATABASE_PRINCIPAL_ID(N'PAM_ROL_EDITOR') IS NULL
    CREATE ROLE PAM_ROL_EDITOR;
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_USUARIOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_USUARIO_ROLES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_UNIDADES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_RUBROS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_SUB_RUBROS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_CLASES_DOCUMENTO TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_BENEFICIOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_BLOQUES_DOCUMENTALES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_BLOQUE_REEMPLAZOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_TIPOS_DOCUMENTO TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_TIPO_DOCUMENTO_BLOQUES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_CAMPOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_CAMPO_CONTEXTOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_ARCHIVOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_PLANTILLAS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_VERSIONES_PLANTILLA TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_SECUENCIAS_PLAN TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_CONSOLIDADOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_PROPUESTAS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_BLOQUEOS_PLANTILLA TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_PRESENTACIONES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_DEVOLUCIONES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_OBSERVACIONES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_MOVIMIENTOS TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_HISTORIAL TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_SOLICITUDES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_DETALLES_BIENES TO PAM_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.PAM_PARRAFOS TO PAM_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.PAM_HISTORIAL TO PAM_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.PAM_MOVIMIENTOS TO PAM_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.PAM_PRESENTACIONES TO PAM_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.PAM_DEVOLUCIONES TO PAM_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.PAM_OBSERVACIONES TO PAM_ROL_EDITOR;
DENY DELETE ON dbo.PAM_VERSIONES_PLANTILLA TO PAM_ROL_EDITOR;
-- ALTER ROLE PAM_ROL_EDITOR ADD MEMBER [usuario_de_la_aplicacion];
GO
