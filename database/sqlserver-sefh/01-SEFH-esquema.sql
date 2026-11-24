/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Esquema para SQL Server 2019 con tablas SEFH_*
   ---------------------------------------------------------------------------------------------------------------------
   Generado por generar-sefh.js a partir de ../sqlserver/01-esquema.sql. No lo edite a mano.
   - Crea 28 tablas en el esquema dbo, todas con el prefijo SEFH_.
   - La llave primaria se llama ID_<siglas>_SEQ (ID_UNI_SEQ, ID_PLA_SEQ…) y guarda los mismos identificadores de los
     archivos JSON (por ejemplo, PLA-001, CON-0001). Las columnas que apuntan a otra tabla llevan el nombre de esa llave.
   - Todas las tablas llevan la auditoría ESTADO ('ACTIVO'/'INACTIVO'), USU_CRE, FEC_CRE, USU_MOD y FEC_MOD.
   - Los valores de las enumeraciones se guardan como texto, igual que en el JSON ('EnRegistro', 'Vigente'…).
   - El contenido de una versión o propuesta (diseño, bloques y párrafos) se guarda como JSON.
   - La columna Version es el control de concurrencia optimista de la aplicación.
   Se ejecuta una vez. Después ejecute 02-SEFH-datos.sql.
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
CREATE TABLE dbo.SEFH_USUARIOS (
    OrdenRegistro     int IDENTITY(1, 1) NOT NULL,
    ID_USU_SEQ        varchar(60)     NOT NULL,
    NombreCompleto    nvarchar(150)   NOT NULL,
    Version           int             NOT NULL CONSTRAINT DF_SEFH_Usuarios_Version DEFAULT (1),
    ESTADO            varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Usuarios_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE           varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Usuarios_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE           datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Usuarios_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD           varchar(60)     NULL,
    FEC_MOD           datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Usuarios PRIMARY KEY (ID_USU_SEQ),
    CONSTRAINT CK_SEFH_Usuarios_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_USUARIO_ROLES (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_USU_SEQ       varchar(60)     NOT NULL,
    Rol              varchar(20)     NOT NULL,
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_UsuarioRoles_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_UsuarioRoles_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_UsuarioRoles_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_UsuarioRoles PRIMARY KEY (ID_USU_SEQ, Rol),
    CONSTRAINT FK_SEFH_UsuarioRoles_Usuario FOREIGN KEY (ID_USU_SEQ) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT CK_SEFH_UsuarioRoles_Rol CHECK (Rol IN ('Analista', 'OficialJuridico', 'Administrador')),
    CONSTRAINT CK_SEFH_UsuarioRoles_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   2. Catálogos
   --------------------------------------------------------------------------------------------------------------------- */
-- Unidades que elaboran sus propias plantillas: Exoneraciones, Dispensas, Registro Exonerados, Notas de Crédito y Devoluciones.
CREATE TABLE dbo.SEFH_UNIDADES (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_UNI_SEQ       varchar(40)     NOT NULL,           -- por ejemplo exoneraciones
    Nombre           nvarchar(150)   NOT NULL,
    Orden            int             NOT NULL,           -- posición en el menú
    Version          int             NOT NULL CONSTRAINT DF_SEFH_Unidades_Version DEFAULT (1),
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Unidades_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Unidades_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Unidades_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Unidades PRIMARY KEY (ID_UNI_SEQ),
    CONSTRAINT UQ_SEFH_Unidades_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_SEFH_Unidades_Orden CHECK (Orden BETWEEN 1 AND 100),
    CONSTRAINT CK_SEFH_Unidades_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_RUBROS (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_RUBRO_SEQ     varchar(20)     NOT NULL,           -- código institucional, por ejemplo 1000
    Nombre           nvarchar(300)   NOT NULL,
    Version          int             NOT NULL CONSTRAINT DF_SEFH_Rubros_Version DEFAULT (1),
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Rubros_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Rubros_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Rubros_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Rubros PRIMARY KEY (ID_RUBRO_SEQ),
    CONSTRAINT CK_SEFH_Rubros_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_SUB_RUBROS (
    OrdenRegistro      int IDENTITY(1, 1) NOT NULL,
    ID_SUBRUBRO_SEQ    varchar(20)     NOT NULL,
    ID_RUBRO_SEQ       varchar(20)     NOT NULL,
    Nombre             nvarchar(300)   NOT NULL,
    Version            int             NOT NULL CONSTRAINT DF_SEFH_SubRubros_Version DEFAULT (1),
    ESTADO             varchar(10)     NOT NULL CONSTRAINT DF_SEFH_SubRubros_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE            varchar(60)     NOT NULL CONSTRAINT DF_SEFH_SubRubros_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE            datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_SubRubros_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD            varchar(60)     NULL,
    FEC_MOD            datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_SubRubros PRIMARY KEY (ID_SUBRUBRO_SEQ),
    CONSTRAINT FK_SEFH_SubRubros_Rubro FOREIGN KEY (ID_RUBRO_SEQ) REFERENCES dbo.SEFH_RUBROS (ID_RUBRO_SEQ),
    -- Permite exigir en otras tablas que el sub rubro pertenezca a su rubro.
    CONSTRAINT UQ_SEFH_SubRubros_Rubro_Id UNIQUE (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ),
    CONSTRAINT CK_SEFH_SubRubros_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Lista «Documento»: Original, Modificación, Aclaratoria.
CREATE TABLE dbo.SEFH_CLASES_DOCUMENTO (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_CLADOC_SEQ    varchar(20)     NOT NULL,
    Nombre           nvarchar(150)   NOT NULL,
    Version          int             NOT NULL CONSTRAINT DF_SEFH_ClasesDocumento_Version DEFAULT (1),
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_ClasesDocumento_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_ClasesDocumento_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_ClasesDocumento_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_ClasesDocumento PRIMARY KEY (ID_CLADOC_SEQ),
    CONSTRAINT UQ_SEFH_ClasesDocumento_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_SEFH_ClasesDocumento_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Lista «Beneficios»: ISV, ISR, ACPV, Aportación Solidaria.
CREATE TABLE dbo.SEFH_BENEFICIOS (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_BEN_SEQ       varchar(20)     NOT NULL,
    Nombre           nvarchar(150)   NOT NULL,
    Version          int             NOT NULL CONSTRAINT DF_SEFH_Beneficios_Version DEFAULT (1),
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Beneficios_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Beneficios_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Beneficios_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Beneficios PRIMARY KEY (ID_BEN_SEQ),
    CONSTRAINT UQ_SEFH_Beneficios_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_SEFH_Beneficios_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Catálogo de bloques documentales: CABECERA, VISTOS, CONSIDERANDO, FUNDAMENTACION_LEGAL…
CREATE TABLE dbo.SEFH_BLOQUES_DOCUMENTALES (
    OrdenRegistro     int IDENTITY(1, 1) NOT NULL,
    ID_BLODOC_SEQ     varchar(40)     NOT NULL,
    Nombre            nvarchar(80)    NOT NULL,
    Titulo            nvarchar(80)    NULL,
    Numeracion        varchar(15)     NOT NULL CONSTRAINT DF_SEFH_BloquesDocumentales_Numeracion DEFAULT ('Ninguna'),
    EsFirmas          bit             NOT NULL CONSTRAINT DF_SEFH_BloquesDocumentales_EsFirmas DEFAULT (0),
    ContextoCampos    varchar(20)     NOT NULL,
    Descripcion       nvarchar(300)   NULL,
    Version           int             NOT NULL CONSTRAINT DF_SEFH_BloquesDocumentales_Version DEFAULT (1),
    ESTADO            varchar(10)     NOT NULL CONSTRAINT DF_SEFH_BloquesDocumentales_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE           varchar(60)     NOT NULL CONSTRAINT DF_SEFH_BloquesDocumentales_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE           datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_BloquesDocumentales_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD           varchar(60)     NULL,
    FEC_MOD           datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_BloquesDocumentales PRIMARY KEY (ID_BLODOC_SEQ),
    CONSTRAINT CK_SEFH_BloquesDocumentales_Numeracion CHECK (Numeracion IN ('Ninguna', 'Considerando', 'Ordinal')),
    CONSTRAINT CK_SEFH_BloquesDocumentales_Contexto CHECK (ContextoCampos IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas')),
    CONSTRAINT CK_SEFH_BloquesDocumentales_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Bloque nuevo que recibe el contenido de uno anterior cuando un tipo documental cambia de formato
-- (por ejemplo, en el Dictamen, Dictamina reemplaza a Resuelve).
CREATE TABLE dbo.SEFH_BLOQUE_REEMPLAZOS (
    OrdenRegistro        int IDENTITY(1, 1) NOT NULL,
    ID_BLODOC_SEQ        varchar(40)     NOT NULL,
    ReemplazaBloqueId    varchar(40)     NOT NULL,
    ESTADO               varchar(10)     NOT NULL CONSTRAINT DF_SEFH_BloqueReemplazos_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE              varchar(60)     NOT NULL CONSTRAINT DF_SEFH_BloqueReemplazos_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE              datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_BloqueReemplazos_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD              varchar(60)     NULL,
    FEC_MOD              datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_BloqueReemplazos PRIMARY KEY (ID_BLODOC_SEQ, ReemplazaBloqueId),
    CONSTRAINT FK_SEFH_BloqueReemplazos_Bloque FOREIGN KEY (ID_BLODOC_SEQ) REFERENCES dbo.SEFH_BLOQUES_DOCUMENTALES (ID_BLODOC_SEQ),
    CONSTRAINT FK_SEFH_BloqueReemplazos_Anterior FOREIGN KEY (ReemplazaBloqueId) REFERENCES dbo.SEFH_BLOQUES_DOCUMENTALES (ID_BLODOC_SEQ),
    CONSTRAINT CK_SEFH_BloqueReemplazos_Distinto CHECK (ID_BLODOC_SEQ <> ReemplazaBloqueId),
    CONSTRAINT CK_SEFH_BloqueReemplazos_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Resolución, Certificación, Dictamen, Constancia de Obligados Tributarios.
CREATE TABLE dbo.SEFH_TIPOS_DOCUMENTO (
    OrdenRegistro             int IDENTITY(1, 1) NOT NULL,
    ID_TIPDOC_SEQ             varchar(20)     NOT NULL,
    Nombre                    nvarchar(150)   NOT NULL,
    TextoCorrido              bit             NOT NULL CONSTRAINT DF_SEFH_TiposDocumento_TextoCorrido DEFAULT (0),
    CopiaDeTipoDocumentoId    varchar(20)     NULL,       -- la Certificación es copia de la Resolución (D3)
    Version                   int             NOT NULL CONSTRAINT DF_SEFH_TiposDocumento_Version DEFAULT (1),
    ESTADO                    varchar(10)     NOT NULL CONSTRAINT DF_SEFH_TiposDocumento_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE                   varchar(60)     NOT NULL CONSTRAINT DF_SEFH_TiposDocumento_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE                   datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_TiposDocumento_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD                   varchar(60)     NULL,
    FEC_MOD                   datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_TiposDocumento PRIMARY KEY (ID_TIPDOC_SEQ),
    CONSTRAINT UQ_SEFH_TiposDocumento_Nombre UNIQUE (Nombre),
    CONSTRAINT FK_SEFH_TiposDocumento_CopiaDe FOREIGN KEY (CopiaDeTipoDocumentoId) REFERENCES dbo.SEFH_TIPOS_DOCUMENTO (ID_TIPDOC_SEQ),
    CONSTRAINT CK_SEFH_TiposDocumento_CopiaDistinta CHECK (CopiaDeTipoDocumentoId IS NULL OR CopiaDeTipoDocumentoId <> ID_TIPDOC_SEQ),
    CONSTRAINT CK_SEFH_TiposDocumento_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Bloques de cada tipo documental, con su orden y si son obligatorios (D2).
CREATE TABLE dbo.SEFH_TIPO_DOCUMENTO_BLOQUES (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_TIPDOC_SEQ    varchar(20)     NOT NULL,
    ID_BLODOC_SEQ    varchar(40)     NOT NULL,
    Orden            int             NOT NULL,
    Obligatorio      bit             NOT NULL CONSTRAINT DF_SEFH_TipoDocumentoBloques_Obligatorio DEFAULT (0),
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_TipoDocumentoBloques_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_TipoDocumentoBloques_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_TipoDocumentoBloques_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_TipoDocumentoBloques PRIMARY KEY (ID_TIPDOC_SEQ, ID_BLODOC_SEQ),
    CONSTRAINT FK_SEFH_TipoDocumentoBloques_Tipo FOREIGN KEY (ID_TIPDOC_SEQ) REFERENCES dbo.SEFH_TIPOS_DOCUMENTO (ID_TIPDOC_SEQ),
    CONSTRAINT FK_SEFH_TipoDocumentoBloques_Bloque FOREIGN KEY (ID_BLODOC_SEQ) REFERENCES dbo.SEFH_BLOQUES_DOCUMENTALES (ID_BLODOC_SEQ),
    CONSTRAINT UQ_SEFH_TipoDocumentoBloques_Orden UNIQUE (ID_TIPDOC_SEQ, Orden),
    CONSTRAINT CK_SEFH_TipoDocumentoBloques_Orden CHECK (Orden >= 1),
    CONSTRAINT CK_SEFH_TipoDocumentoBloques_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Campos dinámicos. Su origen sale de una lista cerrada de colecciones y propiedades (la valida la aplicación).
CREATE TABLE dbo.SEFH_CAMPOS (
    OrdenRegistro      int IDENTITY(1, 1) NOT NULL,
    ID_CAMPO_SEQ       varchar(60)     NOT NULL,
    Etiqueta           nvarchar(100)   NOT NULL,
    Categoria          nvarchar(100)   NOT NULL,
    TipoDato           varchar(10)     NOT NULL,
    OrigenColeccion    varchar(40)     NOT NULL,
    OrigenPropiedad    varchar(60)     NOT NULL,
    Descripcion        nvarchar(300)   NULL,
    AlertarCambios     bit             NOT NULL CONSTRAINT DF_SEFH_Campos_AlertarCambios DEFAULT (0),  -- avisar al aprobador (Operador)
    Version            int             NOT NULL CONSTRAINT DF_SEFH_Campos_Version DEFAULT (1),
    ESTADO             varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Campos_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE            varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Campos_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE            datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Campos_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD            varchar(60)     NULL,
    FEC_MOD            datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Campos PRIMARY KEY (ID_CAMPO_SEQ),
    CONSTRAINT CK_SEFH_Campos_TipoDato CHECK (TipoDato IN ('Texto', 'Numero', 'Moneda', 'Fecha', 'Booleano')),
    CONSTRAINT CK_SEFH_Campos_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Bloques (contextos) en los que se puede usar cada campo.
CREATE TABLE dbo.SEFH_CAMPO_CONTEXTOS (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    ID_CAMPO_SEQ     varchar(60)     NOT NULL,
    Contexto         varchar(20)     NOT NULL,
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_CampoContextos_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_CampoContextos_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_CampoContextos_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_CampoContextos PRIMARY KEY (ID_CAMPO_SEQ, Contexto),
    CONSTRAINT FK_SEFH_CampoContextos_Campo FOREIGN KEY (ID_CAMPO_SEQ) REFERENCES dbo.SEFH_CAMPOS (ID_CAMPO_SEQ) ON DELETE CASCADE,
    CONSTRAINT CK_SEFH_CampoContextos_Contexto CHECK (Contexto IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas', 'tabla')),
    CONSTRAINT CK_SEFH_CampoContextos_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   3. Archivos (logos). Hasta 1 MB, PNG o JPEG (lo valida la aplicación por el contenido real).
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.SEFH_ARCHIVOS (
    OrdenRegistro     int IDENTITY(1, 1) NOT NULL,
    ID_ARCH_SEQ       varchar(40)     NOT NULL,
    NombreOriginal    nvarchar(150)   NOT NULL,
    TipoContenido     varchar(40)     NOT NULL,
    Tamano            int             NOT NULL,
    Sha256            char(64)        NOT NULL,
    FechaCarga        datetimeoffset(7) NOT NULL,
    Contenido         varbinary(max)  NOT NULL,
    Version           int             NOT NULL CONSTRAINT DF_SEFH_Archivos_Version DEFAULT (1),
    ESTADO            varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Archivos_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE           varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Archivos_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE           datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Archivos_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD           varchar(60)     NULL,
    FEC_MOD           datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Archivos PRIMARY KEY (ID_ARCH_SEQ),
    CONSTRAINT CK_SEFH_Archivos_Tipo CHECK (TipoContenido IN ('image/png', 'image/jpeg')),
    CONSTRAINT CK_SEFH_Archivos_Tamano CHECK (Tamano BETWEEN 1 AND 1048576),
    CONSTRAINT CK_SEFH_Archivos_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   4. Plantillas (identidad) y sus versiones publicadas
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.SEFH_PLANTILLAS (
    OrdenRegistro        int IDENTITY(1, 1) NOT NULL,
    ID_PLA_SEQ           varchar(40)     NOT NULL,
    Nombre               nvarchar(200)   NOT NULL,
    NombreReferencia     nvarchar(200)   NULL,           -- «Nombre Plantilla» que escribe el analista
    ID_UNI_SEQ           varchar(40)     NOT NULL,
    ID_RUBRO_SEQ         varchar(20)     NOT NULL,
    ID_SUBRUBRO_SEQ      varchar(20)     NOT NULL,
    ID_TIPDOC_SEQ        varchar(20)     NULL,
    ID_CLADOC_SEQ        varchar(20)     NULL,
    ID_BEN_SEQ           varchar(20)     NULL,
    ESTADO_PLANTILLA     varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Plantillas_EstadoPlantilla DEFAULT ('Borrador'),
    Descripcion          nvarchar(500)   NULL,
    ID_ARCH_SEQ          varchar(40)     NULL,
    AlineacionLogo       varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Plantillas_AlineacionLogo DEFAULT ('Centro'),
    PiePagina            nvarchar(500)   NULL,
    EspacioFirmasCm      float           NOT NULL CONSTRAINT DF_SEFH_Plantillas_EspacioFirmasCm DEFAULT (1.2),
    FechaModificacion    datetimeoffset(7) NOT NULL,
    Version              int             NOT NULL CONSTRAINT DF_SEFH_Plantillas_Version DEFAULT (1),
    ESTADO               varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Plantillas_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE              varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Plantillas_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE              datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Plantillas_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD              varchar(60)     NULL,
    FEC_MOD              datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Plantillas PRIMARY KEY (ID_PLA_SEQ),
    CONSTRAINT FK_SEFH_Plantillas_Unidad FOREIGN KEY (ID_UNI_SEQ) REFERENCES dbo.SEFH_UNIDADES (ID_UNI_SEQ),
    CONSTRAINT FK_SEFH_Plantillas_SubRubro FOREIGN KEY (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ) REFERENCES dbo.SEFH_SUB_RUBROS (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ),
    CONSTRAINT FK_SEFH_Plantillas_Tipo FOREIGN KEY (ID_TIPDOC_SEQ) REFERENCES dbo.SEFH_TIPOS_DOCUMENTO (ID_TIPDOC_SEQ),
    CONSTRAINT FK_SEFH_Plantillas_Clase FOREIGN KEY (ID_CLADOC_SEQ) REFERENCES dbo.SEFH_CLASES_DOCUMENTO (ID_CLADOC_SEQ),
    CONSTRAINT FK_SEFH_Plantillas_Beneficio FOREIGN KEY (ID_BEN_SEQ) REFERENCES dbo.SEFH_BENEFICIOS (ID_BEN_SEQ),
    CONSTRAINT FK_SEFH_Plantillas_Logo FOREIGN KEY (ID_ARCH_SEQ) REFERENCES dbo.SEFH_ARCHIVOS (ID_ARCH_SEQ),
    CONSTRAINT CK_SEFH_Plantillas_EstadoPlantilla CHECK (ESTADO_PLANTILLA IN ('Borrador', 'Publicada')),
    CONSTRAINT CK_SEFH_Plantillas_AlineacionLogo CHECK (AlineacionLogo IN ('Izquierda', 'Centro', 'Derecha')),
    CONSTRAINT CK_SEFH_Plantillas_EspacioFirmas CHECK (EspacioFirmasCm BETWEEN 0 AND 10),
    -- Identidad funcional: Unidad + Rubro + Sub Rubro + Tipo Documento + Documento + Beneficios identifican una única plantilla.
    -- Tipo Documento, Documento y Beneficios son opcionales: NULL significa que sirve para cualquier valor (UNIQUE trata los NULL como iguales).
    CONSTRAINT UQ_SEFH_Plantillas_Identidad UNIQUE (ID_UNI_SEQ, ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ, ID_TIPDOC_SEQ, ID_CLADOC_SEQ, ID_BEN_SEQ),
    CONSTRAINT CK_SEFH_Plantillas_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Párrafos anteriores a las versiones publicadas: la aplicación los usa para la carga inicial de cada plantilla.
CREATE TABLE dbo.SEFH_PARRAFOS (
    OrdenRegistro          int IDENTITY(1, 1) NOT NULL,
    ID_PARR_SEQ            varchar(40)     NOT NULL,
    ID_PLA_SEQ             varchar(40)     NOT NULL,
    Seccion                varchar(15)     NOT NULL,
    Orden                  int             NOT NULL,
    Contenido              nvarchar(max)   NOT NULL,       -- JSON del editor
    MostrarSoloSiCumple    bit             NOT NULL CONSTRAINT DF_SEFH_Parrafos_MostrarSoloSiCumple DEFAULT (0),
    UnirConAnterior        bit             NOT NULL CONSTRAINT DF_SEFH_Parrafos_UnirConAnterior DEFAULT (0),
    EsTitulo               bit             NOT NULL CONSTRAINT DF_SEFH_Parrafos_EsTitulo DEFAULT (0),
    Condicion              nvarchar(max)   NULL,           -- JSON del grupo de condiciones
    CamposUsados           nvarchar(max)   NOT NULL CONSTRAINT DF_SEFH_Parrafos_CamposUsados DEFAULT (N'[]'),   -- JSON: arreglo de campos
    OrigenParrafoId        varchar(40)     NULL,           -- párrafo del que se copió (puede ser de otra plantilla)
    FechaModificacion      datetimeoffset(7) NOT NULL,
    Version                int             NOT NULL CONSTRAINT DF_SEFH_Parrafos_Version DEFAULT (1),
    ESTADO                 varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Parrafos_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE                varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Parrafos_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE                datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Parrafos_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD                varchar(60)     NULL,
    FEC_MOD                datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Parrafos PRIMARY KEY (ID_PARR_SEQ),
    CONSTRAINT FK_SEFH_Parrafos_Plantilla FOREIGN KEY (ID_PLA_SEQ) REFERENCES dbo.SEFH_PLANTILLAS (ID_PLA_SEQ),
    CONSTRAINT CK_SEFH_Parrafos_Seccion CHECK (Seccion IN ('Cabecera', 'Considerando', 'PorTanto', 'Resuelve', 'Firmas')),
    CONSTRAINT CK_SEFH_Parrafos_Orden CHECK (Orden BETWEEN 0 AND 10000),
    CONSTRAINT CK_SEFH_Parrafos_Contenido CHECK (ISJSON(Contenido) = 1),
    CONSTRAINT CK_SEFH_Parrafos_Condicion CHECK (Condicion IS NULL OR ISJSON(Condicion) = 1),
    CONSTRAINT CK_SEFH_Parrafos_CamposUsados CHECK (ISJSON(CamposUsados) = 1),
    CONSTRAINT CK_SEFH_Parrafos_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE INDEX IX_SEFH_Parrafos_Plantilla ON dbo.SEFH_PARRAFOS (ID_PLA_SEQ, Seccion, Orden);

CREATE TABLE dbo.SEFH_PLANTILLAS_CONSOLIDADO (
    OrdenRegistro        int IDENTITY(1, 1) NOT NULL,
    ID_VERPLA_SEQ        varchar(40)     NOT NULL,
    ID_PLA_SEQ           varchar(40)     NOT NULL,
    NumeroVersion        int             NOT NULL,
    Vigencia             varchar(10)     NOT NULL,
    Contenido            nvarchar(max)   NOT NULL,       -- JSON: diseño, bloques y párrafos (inmutable)
    Huella               char(64)        NOT NULL,       -- SHA-256 del contenido
    VersionAnteriorId    varchar(40)     NULL,
    ID_CONS_SEQ          varchar(40)     NULL,           -- documento que la originó; nulo en la carga inicial
    CargaInicial         bit             NOT NULL CONSTRAINT DF_SEFH_PlantillasConsolidado_CargaInicial DEFAULT (0),
    PublicadaPor         varchar(60)     NOT NULL,
    FechaPublicacion     datetimeoffset(7) NOT NULL,
    InicioVigencia       datetimeoffset(7) NOT NULL,
    FinVigencia          datetimeoffset(7) NULL,
    Version              int             NOT NULL CONSTRAINT DF_SEFH_PlantillasConsolidado_Version DEFAULT (1),
    ESTADO               varchar(10)     NOT NULL CONSTRAINT DF_SEFH_PlantillasConsolidado_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE              varchar(60)     NOT NULL CONSTRAINT DF_SEFH_PlantillasConsolidado_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE              datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_PlantillasConsolidado_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD              varchar(60)     NULL,
    FEC_MOD              datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_PlantillasConsolidado PRIMARY KEY (ID_VERPLA_SEQ),
    CONSTRAINT FK_SEFH_PlantillasConsolidado_Plantilla FOREIGN KEY (ID_PLA_SEQ) REFERENCES dbo.SEFH_PLANTILLAS (ID_PLA_SEQ),
    CONSTRAINT FK_SEFH_PlantillasConsolidado_Anterior FOREIGN KEY (VersionAnteriorId) REFERENCES dbo.SEFH_PLANTILLAS_CONSOLIDADO (ID_VERPLA_SEQ),
    CONSTRAINT UQ_SEFH_PlantillasConsolidado_Numero UNIQUE (ID_PLA_SEQ, NumeroVersion),
    CONSTRAINT CK_SEFH_PlantillasConsolidado_Vigencia CHECK (Vigencia IN ('Vigente', 'NoVigente')),
    CONSTRAINT CK_SEFH_PlantillasConsolidado_Contenido CHECK (ISJSON(Contenido) = 1),
    CONSTRAINT CK_SEFH_PlantillasConsolidado_Numero CHECK (NumeroVersion >= 1),
    -- Una versión vigente no tiene fin de vigencia; una no vigente sí.
    CONSTRAINT CK_SEFH_PlantillasConsolidado_Fin CHECK ((Vigencia = 'Vigente' AND FinVigencia IS NULL) OR (Vigencia = 'NoVigente' AND FinVigencia IS NOT NULL)),
    CONSTRAINT CK_SEFH_PlantillasConsolidado_Origen CHECK (CargaInicial = 1 OR ID_CONS_SEQ IS NOT NULL),
    CONSTRAINT CK_SEFH_PlantillasConsolidado_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Regla 14: como máximo una versión vigente por plantilla, garantizado por el motor.
CREATE UNIQUE INDEX UX_SEFH_PlantillasConsolidado_Vigente ON dbo.SEFH_PLANTILLAS_CONSOLIDADO (ID_PLA_SEQ) WHERE Vigencia = 'Vigente';

/* ---------------------------------------------------------------------------------------------------------------------
   5. Documento Consolidado (PLAN-AAAANNNN)
   --------------------------------------------------------------------------------------------------------------------- */
-- Último número usado por año. Se incrementa con UPDLOCK, HOLDLOCK dentro de la transacción que crea el documento.
CREATE TABLE dbo.SEFH_SECUENCIAS_PLAN (
    OrdenRegistro    int IDENTITY(1, 1) NOT NULL,
    Anio             int             NOT NULL,
    Ultimo           int             NOT NULL CONSTRAINT DF_SEFH_SecuenciasPlan_Ultimo DEFAULT (0),
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_SecuenciasPlan_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_SecuenciasPlan_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_SecuenciasPlan_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_SecuenciasPlan PRIMARY KEY (Anio),
    CONSTRAINT CK_SEFH_SecuenciasPlan_Ultimo CHECK (Ultimo BETWEEN 0 AND 9999),
    CONSTRAINT CK_SEFH_SecuenciasPlan_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_PLANTILLAS_DOC_CAB (
    OrdenRegistro         int IDENTITY(1, 1) NOT NULL,
    ID_CONS_SEQ           varchar(40)     NOT NULL,
    Numero                char(13)        NOT NULL,
    Anio                  int             NOT NULL,
    Secuencia             int             NOT NULL,
    ESTADO_DOCUMENTO      varchar(12)     NOT NULL CONSTRAINT DF_SEFH_PlantillasDocCab_EstadoDocumento DEFAULT ('EnRegistro'),
    ID_UNI_SEQ            varchar(40)     NOT NULL,       -- solo contiene plantillas de su unidad
    AnalistaId            varchar(60)     NOT NULL,
    FechaCreacion         datetimeoffset(7) NOT NULL,
    FechaActualizacion    datetimeoffset(7) NOT NULL,
    NumeroPresentacion    int             NOT NULL CONSTRAINT DF_SEFH_PlantillasDocCab_NumeroPresentacion DEFAULT (0),
    FechaEnvio            datetimeoffset(7) NULL,
    EnviadoPor            varchar(60)     NULL,
    RevisorId             varchar(60)     NULL,
    FechaRevision         datetimeoffset(7) NULL,
    AprobadorId           varchar(60)     NULL,
    FechaAprobacion       datetimeoffset(7) NULL,
    Version               int             NOT NULL CONSTRAINT DF_SEFH_PlantillasDocCab_Version DEFAULT (1),
    ESTADO                varchar(10)     NOT NULL CONSTRAINT DF_SEFH_PlantillasDocCab_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE               varchar(60)     NOT NULL CONSTRAINT DF_SEFH_PlantillasDocCab_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE               datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_PlantillasDocCab_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD               varchar(60)     NULL,
    FEC_MOD               datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_PlantillasDocCab PRIMARY KEY (ID_CONS_SEQ),
    CONSTRAINT UQ_SEFH_PlantillasDocCab_Numero UNIQUE (Numero),
    CONSTRAINT UQ_SEFH_PlantillasDocCab_Secuencia UNIQUE (Anio, Secuencia),
    CONSTRAINT FK_SEFH_PlantillasDocCab_Unidad FOREIGN KEY (ID_UNI_SEQ) REFERENCES dbo.SEFH_UNIDADES (ID_UNI_SEQ),
    CONSTRAINT FK_SEFH_PlantillasDocCab_Analista FOREIGN KEY (AnalistaId) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT FK_SEFH_PlantillasDocCab_EnviadoPor FOREIGN KEY (EnviadoPor) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT FK_SEFH_PlantillasDocCab_Revisor FOREIGN KEY (RevisorId) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT FK_SEFH_PlantillasDocCab_Aprobador FOREIGN KEY (AprobadorId) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT CK_SEFH_PlantillasDocCab_EstadoDocumento CHECK (ESTADO_DOCUMENTO IN ('EnRegistro', 'Verificado', 'Aprobado', 'Eliminado')),
    CONSTRAINT CK_SEFH_PlantillasDocCab_Secuencia CHECK (Secuencia BETWEEN 1 AND 9999),
    CONSTRAINT CK_SEFH_PlantillasDocCab_Anio CHECK (Anio BETWEEN 2000 AND 2999),
    -- El número se deriva del año y la secuencia: PLAN-20260001.
    CONSTRAINT CK_SEFH_PlantillasDocCab_Numero CHECK (Numero = CONCAT('PLAN-', Anio, RIGHT(CONCAT('000', Secuencia), 4))),
    -- Lo aprobado tiene aprobador y fecha, y nunca lo aprueba quien lo preparó (separación de funciones).
    CONSTRAINT CK_SEFH_PlantillasDocCab_Aprobacion CHECK (ESTADO_DOCUMENTO <> 'Aprobado' OR (AprobadorId IS NOT NULL AND FechaAprobacion IS NOT NULL)),
    CONSTRAINT CK_SEFH_PlantillasDocCab_Separacion CHECK (AprobadorId IS NULL OR (AprobadorId <> AnalistaId AND (EnviadoPor IS NULL OR AprobadorId <> EnviadoPor))),
    CONSTRAINT CK_SEFH_PlantillasDocCab_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE INDEX IX_SEFH_PlantillasDocCab_Bandeja ON dbo.SEFH_PLANTILLAS_DOC_CAB (ID_UNI_SEQ, ESTADO_DOCUMENTO, AnalistaId) INCLUDE (Numero, FechaActualizacion);

CREATE TABLE dbo.SEFH_PLANTILLA_DOC (
    OrdenRegistro            int IDENTITY(1, 1) NOT NULL,
    ID_PLADOC_SEQ            varchar(40)     NOT NULL,
    ID_CONS_SEQ              varchar(40)     NOT NULL,
    Operacion                varchar(10)     NOT NULL,
    ID_UNI_SEQ               varchar(40)     NOT NULL,
    ID_RUBRO_SEQ             varchar(20)     NOT NULL,
    ID_SUBRUBRO_SEQ          varchar(20)     NOT NULL,
    ID_TIPDOC_SEQ            varchar(20)     NULL,
    ID_CLADOC_SEQ            varchar(20)     NULL,
    ID_BEN_SEQ               varchar(20)     NULL,
    Nombre                   nvarchar(200)   NOT NULL,
    NombreReferencia         nvarchar(200)   NULL,       -- «Nombre Plantilla» que escribe el analista
    ID_PLA_SEQ               varchar(40)     NULL,       -- obligatorio en MODIFICAR
    ID_VERPLA_SEQ            varchar(40)     NULL,       -- versión vigente de la que partió (MODIFICAR)
    HuellaOrigen             char(64)        NULL,
    ContenidoPropuesto       nvarchar(max)   NOT NULL,   -- JSON con el mismo formato del contenido de una versión
    DerivadaDePropuestaId    varchar(40)     NULL,       -- Certificación derivada de su Resolución (D3)
    Retirada                 bit             NOT NULL CONSTRAINT DF_SEFH_PlantillaDoc_Retirada DEFAULT (0),
    MotivoRetiro             nvarchar(500)   NULL,
    FechaCreacion            datetimeoffset(7) NOT NULL,
    FechaModificacion        datetimeoffset(7) NOT NULL,
    Version                  int             NOT NULL CONSTRAINT DF_SEFH_PlantillaDoc_Version DEFAULT (1),
    ESTADO                   varchar(10)     NOT NULL CONSTRAINT DF_SEFH_PlantillaDoc_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE                  varchar(60)     NOT NULL CONSTRAINT DF_SEFH_PlantillaDoc_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE                  datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_PlantillaDoc_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD                  varchar(60)     NULL,
    FEC_MOD                  datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_PlantillaDoc PRIMARY KEY (ID_PLADOC_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Consolidado FOREIGN KEY (ID_CONS_SEQ) REFERENCES dbo.SEFH_PLANTILLAS_DOC_CAB (ID_CONS_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Unidad FOREIGN KEY (ID_UNI_SEQ) REFERENCES dbo.SEFH_UNIDADES (ID_UNI_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_SubRubro FOREIGN KEY (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ) REFERENCES dbo.SEFH_SUB_RUBROS (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Tipo FOREIGN KEY (ID_TIPDOC_SEQ) REFERENCES dbo.SEFH_TIPOS_DOCUMENTO (ID_TIPDOC_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Clase FOREIGN KEY (ID_CLADOC_SEQ) REFERENCES dbo.SEFH_CLASES_DOCUMENTO (ID_CLADOC_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Beneficio FOREIGN KEY (ID_BEN_SEQ) REFERENCES dbo.SEFH_BENEFICIOS (ID_BEN_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Plantilla FOREIGN KEY (ID_PLA_SEQ) REFERENCES dbo.SEFH_PLANTILLAS (ID_PLA_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_VersionOrigen FOREIGN KEY (ID_VERPLA_SEQ) REFERENCES dbo.SEFH_PLANTILLAS_CONSOLIDADO (ID_VERPLA_SEQ),
    CONSTRAINT FK_SEFH_PlantillaDoc_Derivada FOREIGN KEY (DerivadaDePropuestaId) REFERENCES dbo.SEFH_PLANTILLA_DOC (ID_PLADOC_SEQ),
    CONSTRAINT CK_SEFH_PlantillaDoc_Operacion CHECK (Operacion IN ('Crear', 'Modificar')),
    CONSTRAINT CK_SEFH_PlantillaDoc_Contenido CHECK (ISJSON(ContenidoPropuesto) = 1),
    CONSTRAINT CK_SEFH_PlantillaDoc_Modificar CHECK (Operacion <> 'Modificar' OR (ID_PLA_SEQ IS NOT NULL AND ID_VERPLA_SEQ IS NOT NULL)),
    CONSTRAINT CK_SEFH_PlantillaDoc_Retiro CHECK (Retirada = 0 OR (MotivoRetiro IS NOT NULL AND LEN(MotivoRetiro) >= 5)),
    CONSTRAINT CK_SEFH_PlantillaDoc_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE INDEX IX_SEFH_PlantillaDoc_Consolidado ON dbo.SEFH_PLANTILLA_DOC (ID_CONS_SEQ);
CREATE INDEX IX_SEFH_PlantillaDoc_Plantilla ON dbo.SEFH_PLANTILLA_DOC (ID_PLA_SEQ) WHERE ID_PLA_SEQ IS NOT NULL;

ALTER TABLE dbo.SEFH_PLANTILLAS_CONSOLIDADO
    ADD CONSTRAINT FK_SEFH_PlantillasConsolidado_Consolidado FOREIGN KEY (ID_CONS_SEQ) REFERENCES dbo.SEFH_PLANTILLAS_DOC_CAB (ID_CONS_SEQ);

-- D4: una plantilla (por su identidad funcional) solo puede estar en un documento abierto. La aplicación inserta la
-- fila al agregar la propuesta y la borra al retirarla o al aprobar el documento.
CREATE TABLE dbo.SEFH_BLOQUEOS_PLANTILLA (
    OrdenRegistro      int IDENTITY(1, 1) NOT NULL,
    ID_UNI_SEQ         varchar(40)     NOT NULL,
    ID_RUBRO_SEQ       varchar(20)     NOT NULL,
    ID_SUBRUBRO_SEQ    varchar(20)     NOT NULL,
    ID_TIPDOC_SEQ      varchar(20)     NULL,
    ID_CLADOC_SEQ      varchar(20)     NULL,
    ID_BEN_SEQ         varchar(20)     NULL,
    ID_CONS_SEQ        varchar(40)     NOT NULL,
    ID_PLADOC_SEQ      varchar(40)     NOT NULL,
    Fecha              datetimeoffset(7) NOT NULL,
    ESTADO             varchar(10)     NOT NULL CONSTRAINT DF_SEFH_BloqueosPlantilla_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE            varchar(60)     NOT NULL CONSTRAINT DF_SEFH_BloqueosPlantilla_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE            datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_BloqueosPlantilla_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD            varchar(60)     NULL,
    FEC_MOD            datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_BloqueosPlantilla PRIMARY KEY (ID_PLADOC_SEQ),
    CONSTRAINT UQ_SEFH_BloqueosPlantilla_Identidad UNIQUE (ID_UNI_SEQ, ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ, ID_TIPDOC_SEQ, ID_CLADOC_SEQ, ID_BEN_SEQ),
    CONSTRAINT FK_SEFH_BloqueosPlantilla_Consolidado FOREIGN KEY (ID_CONS_SEQ) REFERENCES dbo.SEFH_PLANTILLAS_DOC_CAB (ID_CONS_SEQ),
    CONSTRAINT FK_SEFH_BloqueosPlantilla_Propuesta FOREIGN KEY (ID_PLADOC_SEQ) REFERENCES dbo.SEFH_PLANTILLA_DOC (ID_PLADOC_SEQ),
    CONSTRAINT CK_SEFH_BloqueosPlantilla_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

-- Cada envío a verificación, con la copia congelada de lo enviado y su huella de integridad.
CREATE TABLE dbo.SEFH_PRESENTACIONES (
    OrdenRegistro         int IDENTITY(1, 1) NOT NULL,
    ID_PRES_SEQ           varchar(40)     NOT NULL,
    ID_CONS_SEQ           varchar(40)     NOT NULL,
    Numero                int             NOT NULL,
    ID_USU_SEQ            varchar(60)     NOT NULL,
    Fecha                 datetimeoffset(7) NOT NULL,
    CantidadPlantillas    int             NOT NULL,
    HuellaIntegridad      char(64)        NOT NULL,
    Propuestas            nvarchar(max)   NOT NULL,       -- JSON: arreglo de las propuestas tal como se enviaron
    Version               int             NOT NULL CONSTRAINT DF_SEFH_Presentaciones_Version DEFAULT (1),
    ESTADO                varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Presentaciones_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE               varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Presentaciones_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE               datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Presentaciones_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD               varchar(60)     NULL,
    FEC_MOD               datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Presentaciones PRIMARY KEY (ID_PRES_SEQ),
    CONSTRAINT FK_SEFH_Presentaciones_Consolidado FOREIGN KEY (ID_CONS_SEQ) REFERENCES dbo.SEFH_PLANTILLAS_DOC_CAB (ID_CONS_SEQ),
    CONSTRAINT FK_SEFH_Presentaciones_Usuario FOREIGN KEY (ID_USU_SEQ) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT UQ_SEFH_Presentaciones_Numero UNIQUE (ID_CONS_SEQ, Numero),
    CONSTRAINT CK_SEFH_Presentaciones_Numero CHECK (Numero >= 1),
    CONSTRAINT CK_SEFH_Presentaciones_Cantidad CHECK (CantidadPlantillas >= 1),
    CONSTRAINT CK_SEFH_Presentaciones_Propuestas CHECK (ISJSON(Propuestas) = 1),
    CONSTRAINT CK_SEFH_Presentaciones_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_DEVOLUCIONES (
    OrdenRegistro         int IDENTITY(1, 1) NOT NULL,
    ID_DEV_SEQ            varchar(40)     NOT NULL,
    ID_CONS_SEQ           varchar(40)     NOT NULL,
    NumeroPresentacion    int             NOT NULL,
    ID_USU_SEQ            varchar(60)     NOT NULL,
    Fecha                 datetimeoffset(7) NOT NULL,
    Justificacion         nvarchar(2000)  NOT NULL,
    Version               int             NOT NULL CONSTRAINT DF_SEFH_Devoluciones_Version DEFAULT (1),
    ESTADO                varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Devoluciones_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE               varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Devoluciones_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE               datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Devoluciones_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD               varchar(60)     NULL,
    FEC_MOD               datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Devoluciones PRIMARY KEY (ID_DEV_SEQ),
    CONSTRAINT FK_SEFH_Devoluciones_Presentacion FOREIGN KEY (ID_CONS_SEQ, NumeroPresentacion) REFERENCES dbo.SEFH_PRESENTACIONES (ID_CONS_SEQ, Numero),
    CONSTRAINT FK_SEFH_Devoluciones_Usuario FOREIGN KEY (ID_USU_SEQ) REFERENCES dbo.SEFH_USUARIOS (ID_USU_SEQ),
    CONSTRAINT UQ_SEFH_Devoluciones_Presentacion UNIQUE (ID_CONS_SEQ, NumeroPresentacion),
    CONSTRAINT CK_SEFH_Devoluciones_Justificacion CHECK (LEN(LTRIM(RTRIM(Justificacion))) > 0),
    CONSTRAINT CK_SEFH_Devoluciones_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_OBSERVACIONES (
    ID_OBS_SEQ       int IDENTITY(1, 1) NOT NULL,
    ID_DEV_SEQ       varchar(40)     NOT NULL,
    Orden            int             NOT NULL,
    ID_PLADOC_SEQ    varchar(40)     NOT NULL,
    BloqueCodigo     varchar(40)     NULL,
    ParrafoId        varchar(40)     NULL,
    Problema         nvarchar(2000)  NOT NULL,
    Recomendacion    nvarchar(2000)  NULL,
    ESTADO           varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Observaciones_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE          varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Observaciones_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE          datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Observaciones_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD          varchar(60)     NULL,
    FEC_MOD          datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Observaciones PRIMARY KEY (ID_OBS_SEQ),
    CONSTRAINT FK_SEFH_Observaciones_Devolucion FOREIGN KEY (ID_DEV_SEQ) REFERENCES dbo.SEFH_DEVOLUCIONES (ID_DEV_SEQ),
    CONSTRAINT FK_SEFH_Observaciones_Propuesta FOREIGN KEY (ID_PLADOC_SEQ) REFERENCES dbo.SEFH_PLANTILLA_DOC (ID_PLADOC_SEQ),
    CONSTRAINT UQ_SEFH_Observaciones_Orden UNIQUE (ID_DEV_SEQ, Orden),
    CONSTRAINT CK_SEFH_Observaciones_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   6. Auditoría: bitácora del documento e historial técnico (solo se insertan)
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.SEFH_MOVIMIENTOS (
    OrdenRegistro         int IDENTITY(1, 1) NOT NULL,
    ID_MOV_SEQ            varchar(40)     NOT NULL,
    ID_CONS_SEQ           varchar(40)     NOT NULL,
    Tipo                  varchar(30)     NOT NULL,
    Usuario               varchar(60)     NOT NULL,
    Fecha                 datetimeoffset(7) NOT NULL,
    EstadoOrigen          varchar(12)     NULL,
    EstadoDestino         varchar(12)     NULL,
    NumeroPresentacion    int             NULL,
    Detalle               nvarchar(1000)  NOT NULL,
    Version               int             NOT NULL CONSTRAINT DF_SEFH_Movimientos_Version DEFAULT (1),
    ESTADO                varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Movimientos_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE               varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Movimientos_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE               datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Movimientos_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD               varchar(60)     NULL,
    FEC_MOD               datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Movimientos PRIMARY KEY (ID_MOV_SEQ),
    CONSTRAINT FK_SEFH_Movimientos_Consolidado FOREIGN KEY (ID_CONS_SEQ) REFERENCES dbo.SEFH_PLANTILLAS_DOC_CAB (ID_CONS_SEQ),
    CONSTRAINT CK_SEFH_Movimientos_Tipo CHECK (Tipo IN ('Creacion', 'PropuestaAgregada', 'PropuestaModificada', 'PropuestaRetirada',
        'EnvioVerificacion', 'Devolucion', 'Aprobacion', 'Publicacion', 'CambioVigencia', 'ErrorPublicacion', 'Reasignacion', 'Eliminacion')),
    CONSTRAINT CK_SEFH_Movimientos_EstadoOrigen CHECK (EstadoOrigen IS NULL OR EstadoOrigen IN ('EnRegistro', 'Verificado', 'Aprobado', 'Eliminado')),
    CONSTRAINT CK_SEFH_Movimientos_EstadoDestino CHECK (EstadoDestino IS NULL OR EstadoDestino IN ('EnRegistro', 'Verificado', 'Aprobado', 'Eliminado')),
    CONSTRAINT CK_SEFH_Movimientos_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE INDEX IX_SEFH_Movimientos_Consolidado ON dbo.SEFH_MOVIMIENTOS (ID_CONS_SEQ, Fecha);

CREATE TABLE dbo.SEFH_HISTORIAL (
    OrdenRegistro     int IDENTITY(1, 1) NOT NULL,
    ID_HIST_SEQ       varchar(40)     NOT NULL,
    Fecha             datetimeoffset(7) NOT NULL,
    Usuario           varchar(60)     NOT NULL,
    Coleccion         varchar(40)     NOT NULL,
    EntidadId         varchar(60)     NOT NULL,
    Accion            varchar(15)     NOT NULL,
    VersionEntidad    int             NOT NULL,
    Anterior          nvarchar(max)   NULL,           -- JSON del estado anterior
    Nuevo             nvarchar(max)   NULL,           -- JSON del estado nuevo
    Version           int             NOT NULL CONSTRAINT DF_SEFH_Historial_Version DEFAULT (1),
    ESTADO            varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Historial_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE           varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Historial_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE           datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Historial_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD           varchar(60)     NULL,
    FEC_MOD           datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Historial PRIMARY KEY (ID_HIST_SEQ),
    CONSTRAINT CK_SEFH_Historial_Accion CHECK (Accion IN ('Creacion', 'Actualizacion', 'Eliminacion')),
    CONSTRAINT CK_SEFH_Historial_Anterior CHECK (Anterior IS NULL OR ISJSON(Anterior) = 1),
    CONSTRAINT CK_SEFH_Historial_Nuevo CHECK (Nuevo IS NULL OR ISJSON(Nuevo) = 1),
    CONSTRAINT CK_SEFH_Historial_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE INDEX IX_SEFH_Historial_Entidad ON dbo.SEFH_HISTORIAL (Coleccion, EntidadId, Fecha);
CREATE INDEX IX_SEFH_Historial_Fecha ON dbo.SEFH_HISTORIAL (Fecha);

/* ---------------------------------------------------------------------------------------------------------------------
   7. Datos de origen de las solicitudes (para sustituir los campos al generar el documento)
   En producción pueden ser vistas o una réplica del sistema de origen; en pruebas se cargan datos ficticios.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE dbo.SEFH_SOLICITUDES (
    OrdenRegistro       int IDENTITY(1, 1) NOT NULL,
    ID_SOL_SEQ          varchar(40)     NOT NULL,
    NumeroSolicitud     nvarchar(50)    NOT NULL,
    Operador            nvarchar(200)   NULL,
    Beneficiario        nvarchar(200)   NULL,
    ID_RUBRO_SEQ        varchar(20)     NOT NULL,
    ID_SUBRUBRO_SEQ     varchar(20)     NOT NULL,
    TipoBeneficio       nvarchar(150)   NULL,
    ESTADO_SOLICITUD    nvarchar(50)    NULL,
    FechaResolucion     date            NULL,
    AnioGestion         int             NULL,
    MontoExoneracion    decimal(18, 2)  NULL,
    UnidadEjecutora     nvarchar(150)   NULL,
    Departamento        nvarchar(100)   NULL,
    Municipio           nvarchar(100)   NULL,
    EsDatoPrueba        bit             NOT NULL CONSTRAINT DF_SEFH_Solicitudes_EsDatoPrueba DEFAULT (0),
    Version             int             NOT NULL CONSTRAINT DF_SEFH_Solicitudes_Version DEFAULT (1),
    ESTADO              varchar(10)     NOT NULL CONSTRAINT DF_SEFH_Solicitudes_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE             varchar(60)     NOT NULL CONSTRAINT DF_SEFH_Solicitudes_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE             datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_Solicitudes_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD             varchar(60)     NULL,
    FEC_MOD             datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_Solicitudes PRIMARY KEY (ID_SOL_SEQ),
    CONSTRAINT UQ_SEFH_Solicitudes_Numero UNIQUE (NumeroSolicitud),
    CONSTRAINT FK_SEFH_Solicitudes_SubRubro FOREIGN KEY (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ) REFERENCES dbo.SEFH_SUB_RUBROS (ID_RUBRO_SEQ, ID_SUBRUBRO_SEQ),
    CONSTRAINT CK_SEFH_Solicitudes_Anio CHECK (AnioGestion IS NULL OR AnioGestion BETWEEN 2000 AND 2100),
    CONSTRAINT CK_SEFH_Solicitudes_Monto CHECK (MontoExoneracion IS NULL OR MontoExoneracion >= 0),
    CONSTRAINT CK_SEFH_Solicitudes_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE TABLE dbo.SEFH_DETALLES_BIENES (
    OrdenRegistro             int IDENTITY(1, 1) NOT NULL,
    ID_DETBIEN_SEQ            varchar(40)     NOT NULL,
    ID_SOL_SEQ                varchar(40)     NOT NULL,
    CodigoArancelario         varchar(20)     NOT NULL,
    Descripcion               nvarchar(300)   NOT NULL,
    UnidadMedida              nvarchar(50)    NULL,
    Cantidad                  decimal(18, 4)  NOT NULL,
    ValorEstimado             decimal(18, 2)  NULL,
    DescripcionArancelaria    nvarchar(300)   NULL,
    MontoAutorizado           decimal(18, 2)  NULL,
    EsDatoPrueba              bit             NOT NULL CONSTRAINT DF_SEFH_DetallesBienes_EsDatoPrueba DEFAULT (0),
    Version                   int             NOT NULL CONSTRAINT DF_SEFH_DetallesBienes_Version DEFAULT (1),
    ESTADO                    varchar(10)     NOT NULL CONSTRAINT DF_SEFH_DetallesBienes_ESTADO DEFAULT ('ACTIVO'),
    USU_CRE                   varchar(60)     NOT NULL CONSTRAINT DF_SEFH_DetallesBienes_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),
    FEC_CRE                   datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_DetallesBienes_FEC_CRE DEFAULT (SYSDATETIME()),
    USU_MOD                   varchar(60)     NULL,
    FEC_MOD                   datetime2(7)    NULL,
    CONSTRAINT PK_SEFH_DetallesBienes PRIMARY KEY (ID_DETBIEN_SEQ),
    CONSTRAINT FK_SEFH_DetallesBienes_Solicitud FOREIGN KEY (ID_SOL_SEQ) REFERENCES dbo.SEFH_SOLICITUDES (ID_SOL_SEQ),
    CONSTRAINT CK_SEFH_DetallesBienes_Cantidad CHECK (Cantidad >= 0),
    CONSTRAINT CK_SEFH_DetallesBienes_Valor CHECK (ValorEstimado IS NULL OR ValorEstimado >= 0),
    CONSTRAINT CK_SEFH_DetallesBienes_Monto CHECK (MontoAutorizado IS NULL OR MontoAutorizado >= 0),
    CONSTRAINT CK_SEFH_DetallesBienes_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))
);

CREATE INDEX IX_SEFH_DetallesBienes_Solicitud ON dbo.SEFH_DETALLES_BIENES (ID_SOL_SEQ);

COMMIT TRANSACTION;
GO

/* ---------------------------------------------------------------------------------------------------------------------
   8. Protección de la auditoría (regla 15: nada se borra ni se reescribe)
   --------------------------------------------------------------------------------------------------------------------- */

-- Historial, bitácora, presentaciones, devoluciones y observaciones: solo se insertan.
CREATE OR ALTER TRIGGER dbo.TR_SEFH_SoloInsercion_Historial ON dbo.SEFH_HISTORIAL AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'El historial no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_SEFH_SoloInsercion_Movimientos ON dbo.SEFH_MOVIMIENTOS AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'La bitácora del documento no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_SEFH_SoloInsercion_Presentaciones ON dbo.SEFH_PRESENTACIONES AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una presentación enviada no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_SEFH_SoloInsercion_Devoluciones ON dbo.SEFH_DEVOLUCIONES AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una devolución no se modifica ni se elimina.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_SEFH_SoloInsercion_Observaciones ON dbo.SEFH_OBSERVACIONES AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50015, N'Una observación no se modifica ni se elimina.', 1;
END;
GO

-- Una versión publicada no se elimina, y de ella solo cambian la vigencia, el fin de vigencia y la versión de registro.
CREATE OR ALTER TRIGGER dbo.TR_SEFH_PlantillasConsolidado_Inmutable ON dbo.SEFH_PLANTILLAS_CONSOLIDADO AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted)
        THROW 50015, N'Las versiones publicadas no se eliminan.', 1;
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted d ON d.ID_VERPLA_SEQ = i.ID_VERPLA_SEQ
        WHERE i.ID_PLA_SEQ <> d.ID_PLA_SEQ
           OR i.NumeroVersion <> d.NumeroVersion
           OR i.Contenido <> d.Contenido
           OR i.Huella <> d.Huella
           OR ISNULL(i.VersionAnteriorId, '') <> ISNULL(d.VersionAnteriorId, '')
           OR ISNULL(i.ID_CONS_SEQ, '') <> ISNULL(d.ID_CONS_SEQ, '')
           OR i.CargaInicial <> d.CargaInicial
           OR i.PublicadaPor <> d.PublicadaPor
           OR i.FechaPublicacion <> d.FechaPublicacion
           OR i.InicioVigencia <> d.InicioVigencia
           OR (d.Vigencia = 'NoVigente' AND i.Vigencia = 'Vigente'))
        THROW 50015, N'Una versión publicada es inmutable: solo puede pasar de VIGENTE a NO VIGENTE.', 1;
END;
GO

-- Un documento aprobado no cambia de estado ni se elimina; un documento solo se elimina si nunca se envió.
CREATE OR ALTER TRIGGER dbo.TR_SEFH_PlantillasDocCab_Aprobado ON dbo.SEFH_PLANTILLAS_DOC_CAB AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.ID_CONS_SEQ = d.ID_CONS_SEQ
               WHERE d.ESTADO_DOCUMENTO = 'Aprobado' AND (i.ID_CONS_SEQ IS NULL OR i.ESTADO_DOCUMENTO <> 'Aprobado'))
        THROW 50017, N'Un Documento Consolidado aprobado no cambia de estado ni se elimina.', 1;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.ID_CONS_SEQ = d.ID_CONS_SEQ
               WHERE i.ID_CONS_SEQ IS NULL AND d.NumeroPresentacion > 0)
        THROW 50017, N'Un Documento Consolidado que ya se envió no se elimina.', 1;
END;
GO

/* ---------------------------------------------------------------------------------------------------------------------
   9. Permisos del usuario de la aplicación (ajustar el nombre del usuario o rol institucional)
   --------------------------------------------------------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'SEFH_ROL_EDITOR') IS NULL
    CREATE ROLE SEFH_ROL_EDITOR;
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_USUARIOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_USUARIO_ROLES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_UNIDADES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_RUBROS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_SUB_RUBROS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_CLASES_DOCUMENTO TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_BENEFICIOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_BLOQUES_DOCUMENTALES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_BLOQUE_REEMPLAZOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_TIPOS_DOCUMENTO TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_TIPO_DOCUMENTO_BLOQUES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_CAMPOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_CAMPO_CONTEXTOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_ARCHIVOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_PLANTILLAS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_PLANTILLAS_CONSOLIDADO TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_SECUENCIAS_PLAN TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_PLANTILLAS_DOC_CAB TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_PLANTILLA_DOC TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_BLOQUEOS_PLANTILLA TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_PRESENTACIONES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_DEVOLUCIONES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_OBSERVACIONES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_MOVIMIENTOS TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_HISTORIAL TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_SOLICITUDES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_DETALLES_BIENES TO SEFH_ROL_EDITOR;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.SEFH_PARRAFOS TO SEFH_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.SEFH_HISTORIAL TO SEFH_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.SEFH_MOVIMIENTOS TO SEFH_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.SEFH_PRESENTACIONES TO SEFH_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.SEFH_DEVOLUCIONES TO SEFH_ROL_EDITOR;
DENY UPDATE, DELETE ON dbo.SEFH_OBSERVACIONES TO SEFH_ROL_EDITOR;
DENY DELETE ON dbo.SEFH_PLANTILLAS_CONSOLIDADO TO SEFH_ROL_EDITOR;
-- ALTER ROLE SEFH_ROL_EDITOR ADD MEMBER [usuario_de_la_aplicacion];
GO
