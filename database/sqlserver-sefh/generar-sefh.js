// Genera los scripts de SQL Server con las tablas SEFH_*:
//   01-SEFH-esquema.sql  a partir de ../sqlserver/01-esquema.sql, más lo que ese esquema no guardaba
//   02-SEFH-datos.sql    a partir de backend/EditorResoluciones.Api/Data (inicie la API una vez antes)
// Uso, desde la raíz del repositorio: node database/sqlserver-sefh/generar-sefh.js
// Falla si una colección trae una propiedad sin columna: así ningún dato se pierde sin avisar.
const fs = require('fs');
const path = require('path');

const raiz = path.resolve(__dirname, '..', '..');
const DATA = path.join(raiz, 'backend', 'EditorResoluciones.Api', 'Data');
const leer = (nombre) => JSON.parse(fs.readFileSync(path.join(DATA, `${nombre}.json`), 'utf8'));
// UTF-8 con BOM y CRLF: SSMS y sqlcmd conservan así las tildes.
const escribir = (nombre, texto) => fs.writeFileSync(path.join(__dirname, nombre), `﻿${texto.replace(/\r?\n/g, '\r\n')}`);

const TABLAS = {
  Usuarios: 'SEFH_USUARIOS', UsuarioRoles: 'SEFH_USUARIO_ROLES', Unidades: 'SEFH_UNIDADES', Rubros: 'SEFH_RUBROS',
  SubRubros: 'SEFH_SUB_RUBROS', ClasesDocumento: 'SEFH_CLASES_DOCUMENTO', Beneficios: 'SEFH_BENEFICIOS',
  BloquesDocumentales: 'SEFH_BLOQUES_DOCUMENTALES', BloqueReemplazos: 'SEFH_BLOQUE_REEMPLAZOS',
  TiposDocumento: 'SEFH_TIPOS_DOCUMENTO', TipoDocumentoBloques: 'SEFH_TIPO_DOCUMENTO_BLOQUES', Campos: 'SEFH_CAMPOS',
  CampoContextos: 'SEFH_CAMPO_CONTEXTOS', Archivos: 'SEFH_ARCHIVOS', Plantillas: 'SEFH_PLANTILLAS',
  PlantillasConsolidado: 'SEFH_PLANTILLAS_CONSOLIDADO', SecuenciasPlan: 'SEFH_SECUENCIAS_PLAN', PlantillasDocCab: 'SEFH_PLANTILLAS_DOC_CAB',
  PlantillaDoc: 'SEFH_PLANTILLA_DOC', BloqueosPlantilla: 'SEFH_BLOQUEOS_PLANTILLA', Presentaciones: 'SEFH_PRESENTACIONES',
  Devoluciones: 'SEFH_DEVOLUCIONES', Observaciones: 'SEFH_OBSERVACIONES', Movimientos: 'SEFH_MOVIMIENTOS',
  Historial: 'SEFH_HISTORIAL', Solicitudes: 'SEFH_SOLICITUDES', DetallesBienes: 'SEFH_DETALLES_BIENES',
};

// Llave primaria de cada tabla que tenía la columna Id: ID_<siglas de la tabla>_SEQ (el valor sigue siendo el mismo código).
// Las tablas con llave compuesta (roles, reemplazos, bloques de un tipo, contextos, secuencias y bloqueos) no tienen Id.
const LLAVES = {
  SEFH_USUARIOS: 'ID_USU_SEQ', SEFH_UNIDADES: 'ID_UNI_SEQ', SEFH_RUBROS: 'ID_RUBRO_SEQ', SEFH_SUB_RUBROS: 'ID_SUBRUBRO_SEQ',
  SEFH_CLASES_DOCUMENTO: 'ID_CLADOC_SEQ', SEFH_BENEFICIOS: 'ID_BEN_SEQ', SEFH_BLOQUES_DOCUMENTALES: 'ID_BLODOC_SEQ',
  SEFH_TIPOS_DOCUMENTO: 'ID_TIPDOC_SEQ', SEFH_CAMPOS: 'ID_CAMPO_SEQ', SEFH_ARCHIVOS: 'ID_ARCH_SEQ', SEFH_PLANTILLAS: 'ID_PLA_SEQ',
  SEFH_PARRAFOS: 'ID_PARR_SEQ', SEFH_PLANTILLAS_CONSOLIDADO: 'ID_VERPLA_SEQ', SEFH_PLANTILLAS_DOC_CAB: 'ID_CONS_SEQ',
  SEFH_PLANTILLA_DOC: 'ID_PLADOC_SEQ', SEFH_PRESENTACIONES: 'ID_PRES_SEQ', SEFH_DEVOLUCIONES: 'ID_DEV_SEQ', SEFH_OBSERVACIONES: 'ID_OBS_SEQ',
  SEFH_MOVIMIENTOS: 'ID_MOV_SEQ', SEFH_HISTORIAL: 'ID_HIST_SEQ', SEFH_SOLICITUDES: 'ID_SOL_SEQ', SEFH_DETALLES_BIENES: 'ID_DETBIEN_SEQ',
};

// Columnas que apuntan a otra tabla: llevan el nombre de la llave a la que apuntan. Las que apuntan a su propia tabla
// (CopiaDeTipoDocumentoId, VersionAnteriorId, DerivadaDePropuestaId) o a una tabla a la que la fila ya apunta con otra
// columna (ReemplazaBloqueId, AnalistaId, EnviadoPor, RevisorId, AprobadorId) conservan su nombre.
const FORANEAS = {
  UsuarioId: 'ID_USU_SEQ', UnidadId: 'ID_UNI_SEQ', RubroId: 'ID_RUBRO_SEQ', SubRubroId: 'ID_SUBRUBRO_SEQ',
  TipoDocumentoId: 'ID_TIPDOC_SEQ', ClaseDocumentoId: 'ID_CLADOC_SEQ', BeneficioId: 'ID_BEN_SEQ', BloqueId: 'ID_BLODOC_SEQ',
  CampoId: 'ID_CAMPO_SEQ', LogoId: 'ID_ARCH_SEQ', PlantillaId: 'ID_PLA_SEQ', VersionOrigenId: 'ID_VERPLA_SEQ',
  ConsolidadoId: 'ID_CONS_SEQ', PropuestaId: 'ID_PLADOC_SEQ', DevolucionId: 'ID_DEV_SEQ', SolicitudId: 'ID_SOL_SEQ',
};

// Columnas propias que cambian de nombre en una tabla: el Estado del negocio deja el nombre ESTADO a la auditoría, y el
// Usuario de presentaciones y devoluciones es una llave foránea a los usuarios. [columna nueva, sufijo de sus restricciones]
const PROPIAS = {
  SEFH_PLANTILLAS: { Estado: ['ESTADO_PLANTILLA', 'EstadoPlantilla'] },
  SEFH_PLANTILLAS_DOC_CAB: { Estado: ['ESTADO_DOCUMENTO', 'EstadoDocumento'] },
  SEFH_SOLICITUDES: { Estado: ['ESTADO_SOLICITUD', 'EstadoSolicitud'] },
  SEFH_PRESENTACIONES: { Usuario: ['ID_USU_SEQ', 'Usuario'] },
  SEFH_DEVOLUCIONES: { Usuario: ['ID_USU_SEQ', 'Usuario'] },
};

// ---------- Esquema ----------

function reemplazar(texto, buscado, nuevo) {
  if (!texto.includes(buscado)) {
    throw new Error(`No se encontró en el esquema: ${buscado}`);
  }
  return texto.replace(buscado, () => nuevo);
}

function generarEsquema() {
  let sql = fs.readFileSync(path.join(raiz, 'database', 'sqlserver', '01-esquema.sql'), 'utf8').replace(/^﻿/, '').replace(/\r\n/g, '\n');

  // Encabezado, esquema propio y permisos: se reescriben para las tablas SEFH_* en dbo.
  sql = sql.slice(sql.indexOf('BEGIN TRANSACTION;'));
  sql = sql.slice(0, sql.indexOf('/* ---------------------------------------------------------------------------------------------------------------------\n   9. Permisos'));

  // Lo que el esquema base no guardaba y el sistema sí usa.
  sql = reemplazar(sql, `    Nombre              nvarchar(200)   NOT NULL,
    UnidadId            varchar(40)     NOT NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoDocumentoId     varchar(20)     NULL,
    ClaseDocumentoId    varchar(20)     NULL,
    BeneficioId         varchar(20)     NULL,
    FechaModificacion   datetimeoffset(7) NOT NULL,`, `    Nombre              nvarchar(200)   NOT NULL,
    NombreReferencia    nvarchar(200)   NULL,           -- «Nombre Plantilla» que escribe el analista
    UnidadId            varchar(40)     NOT NULL,
    RubroId             varchar(20)     NOT NULL,
    SubRubroId          varchar(20)     NOT NULL,
    TipoDocumentoId     varchar(20)     NULL,
    ClaseDocumentoId    varchar(20)     NULL,
    BeneficioId         varchar(20)     NULL,
    Estado              varchar(10)     NOT NULL CONSTRAINT DF_Plantillas_Estado DEFAULT ('Borrador'),
    Descripcion         nvarchar(500)   NULL,
    LogoId              varchar(40)     NULL,
    AlineacionLogo      varchar(10)     NOT NULL CONSTRAINT DF_Plantillas_AlineacionLogo DEFAULT ('Centro'),
    PiePagina           nvarchar(500)   NULL,
    EspacioFirmasCm     float           NOT NULL CONSTRAINT DF_Plantillas_EspacioFirmasCm DEFAULT (1.2),
    FechaModificacion   datetimeoffset(7) NOT NULL,`);
  sql = reemplazar(sql, `    CONSTRAINT FK_Plantillas_Beneficio FOREIGN KEY (BeneficioId) REFERENCES plantillas.Beneficios (Id),
`, `    CONSTRAINT FK_Plantillas_Beneficio FOREIGN KEY (BeneficioId) REFERENCES plantillas.Beneficios (Id),
    CONSTRAINT FK_Plantillas_Logo FOREIGN KEY (LogoId) REFERENCES plantillas.Archivos (Id),
    CONSTRAINT CK_Plantillas_Estado CHECK (Estado IN ('Borrador', 'Publicada')),
    CONSTRAINT CK_Plantillas_AlineacionLogo CHECK (AlineacionLogo IN ('Izquierda', 'Centro', 'Derecha')),
    CONSTRAINT CK_Plantillas_EspacioFirmas CHECK (EspacioFirmasCm BETWEEN 0 AND 10),
`);
  sql = reemplazar(sql, `    Nombre                  nvarchar(200)   NOT NULL,
    PlantillaId             varchar(40)     NULL,       -- obligatorio en MODIFICAR`, `    Nombre                  nvarchar(200)   NOT NULL,
    NombreReferencia        nvarchar(200)   NULL,       -- «Nombre Plantilla» que escribe el analista
    PlantillaId             varchar(40)     NULL,       -- obligatorio en MODIFICAR`);
  sql = reemplazar(sql, `    VersionEntidad      int             NOT NULL,
    Anterior            nvarchar(max)   NULL,           -- JSON del estado anterior
    Nuevo               nvarchar(max)   NULL,           -- JSON del estado nuevo
`, `    VersionEntidad      int             NOT NULL,
    Anterior            nvarchar(max)   NULL,           -- JSON del estado anterior
    Nuevo               nvarchar(max)   NULL,           -- JSON del estado nuevo
    Version             int             NOT NULL CONSTRAINT DF_Historial_Version DEFAULT (1),
`);
  sql = reemplazar(sql, `CREATE TABLE plantillas.PlantillasConsolidado (`, `-- Párrafos anteriores a las versiones publicadas: la aplicación los usa para la carga inicial de cada plantilla.
CREATE TABLE plantillas.Parrafos (
    Id                  varchar(40)     NOT NULL,
    PlantillaId         varchar(40)     NOT NULL,
    Seccion             varchar(15)     NOT NULL,
    Orden               int             NOT NULL,
    Contenido           nvarchar(max)   NOT NULL,       -- JSON del editor
    Activo              bit             NOT NULL CONSTRAINT DF_Parrafos_Activo DEFAULT (1),
    MostrarSoloSiCumple bit             NOT NULL CONSTRAINT DF_Parrafos_MostrarSoloSiCumple DEFAULT (0),
    UnirConAnterior     bit             NOT NULL CONSTRAINT DF_Parrafos_UnirConAnterior DEFAULT (0),
    EsTitulo            bit             NOT NULL CONSTRAINT DF_Parrafos_EsTitulo DEFAULT (0),
    Condicion           nvarchar(max)   NULL,           -- JSON del grupo de condiciones
    CamposUsados        nvarchar(max)   NOT NULL CONSTRAINT DF_Parrafos_CamposUsados DEFAULT (N'[]'),   -- JSON: arreglo de campos
    OrigenParrafoId     varchar(40)     NULL,           -- párrafo del que se copió (puede ser de otra plantilla)
    FechaModificacion   datetimeoffset(7) NOT NULL,
    Version             int             NOT NULL CONSTRAINT DF_Parrafos_Version DEFAULT (1),
    CONSTRAINT PK_Parrafos PRIMARY KEY (Id),
    CONSTRAINT FK_Parrafos_Plantilla FOREIGN KEY (PlantillaId) REFERENCES plantillas.Plantillas (Id),
    CONSTRAINT CK_Parrafos_Seccion CHECK (Seccion IN ('Cabecera', 'Considerando', 'PorTanto', 'Resuelve', 'Firmas')),
    CONSTRAINT CK_Parrafos_Orden CHECK (Orden BETWEEN 0 AND 10000),
    CONSTRAINT CK_Parrafos_Contenido CHECK (ISJSON(Contenido) = 1),
    CONSTRAINT CK_Parrafos_Condicion CHECK (Condicion IS NULL OR ISJSON(Condicion) = 1),
    CONSTRAINT CK_Parrafos_CamposUsados CHECK (ISJSON(CamposUsados) = 1)
);

CREATE INDEX IX_Parrafos_Plantilla ON plantillas.Parrafos (PlantillaId, Seccion, Orden);

CREATE TABLE plantillas.PlantillasConsolidado (`);

  // Nombres: tablas SEFH_* en dbo; restricciones, índices y triggers con el mismo prefijo.
  const tablas = { ...TABLAS, Parrafos: 'SEFH_PARRAFOS' };
  sql = sql.replace(/plantillas\.([A-Za-z]+)/g, (_, tabla) => {
    if (tablas[tabla]) {
      return `dbo.${tablas[tabla]}`;
    }
    return `dbo.${tabla.replace(/^TR_/, 'TR_SEFH_')}`;
  });
  sql = sql.replace(/\b(PK|FK|UQ|CK|DF|IX|UX|TR)_(?!SEFH_)/g, '$1_SEFH_');

  // OrdenRegistro: la aplicación lista los registros en el orden en que se agregaron, como en los archivos JSON.
  // SEFH_OBSERVACIONES no la lleva: ya tiene su columna Orden (y SQL Server admite una sola columna IDENTITY por tabla).
  sql = sql.replace(/CREATE TABLE dbo\.(SEFH_\w+) \(\n/g, (linea, tabla) =>
    tabla === 'SEFH_OBSERVACIONES' ? linea : `${linea}    OrdenRegistro       int IDENTITY(1, 1) NOT NULL,\n`);

  sql = renombrarColumnas(sql);
  sql = agregarAuditoria(sql);

  const nombres = Object.values(tablas);
  const soloInsercion = ['SEFH_HISTORIAL', 'SEFH_MOVIMIENTOS', 'SEFH_PRESENTACIONES', 'SEFH_DEVOLUCIONES', 'SEFH_OBSERVACIONES'];
  const permisos = [
    ...nombres.map((t) => `GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.${t} TO SEFH_ROL_EDITOR;`),
    ...soloInsercion.map((t) => `DENY UPDATE, DELETE ON dbo.${t} TO SEFH_ROL_EDITOR;`),
    'DENY DELETE ON dbo.SEFH_PLANTILLAS_CONSOLIDADO TO SEFH_ROL_EDITOR;',
  ].join('\n');

  return `/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Esquema para SQL Server 2019 con tablas SEFH_*
   ---------------------------------------------------------------------------------------------------------------------
   Generado por generar-sefh.js a partir de ../sqlserver/01-esquema.sql. No lo edite a mano.
   - Crea ${nombres.length} tablas en el esquema dbo, todas con el prefijo SEFH_.
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

${sql}/* ---------------------------------------------------------------------------------------------------------------------
   9. Permisos del usuario de la aplicación (ajustar el nombre del usuario o rol institucional)
   --------------------------------------------------------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'SEFH_ROL_EDITOR') IS NULL
    CREATE ROLE SEFH_ROL_EDITOR;
GO
${permisos}
-- ALTER ROLE SEFH_ROL_EDITOR ADD MEMBER [usuario_de_la_aplicacion];
GO
`;
}

/** Aplica a un fragmento los nombres nuevos de las columnas de una tabla: su llave Id y sus columnas propias. */
function renombrarEnTabla(tabla, fragmento) {
  let resultado = fragmento;
  if (LLAVES[tabla]) {
    resultado = resultado.replace(/\bId\b/g, LLAVES[tabla]);
  }
  for (const [columna, [nueva, sufijo]] of Object.entries(PROPIAS[tabla] ?? {})) {
    resultado = resultado.replace(new RegExp(`\\b${columna}\\b`, 'g'), nueva);
    // Restricciones con el nombre de la columna (DF_SEFH_Plantillas_Estado): así no chocan con las de la auditoría.
    resultado = resultado.replace(new RegExp(`_${columna}\\b`, 'g'), `_${sufijo}`);
  }
  return resultado;
}

function renombrarColumnas(texto) {
  let sql = texto;
  // 1. La lista de columnas de cada REFERENCES es de la tabla referida.
  sql = sql.replace(/REFERENCES dbo\.(\w+) \(([^)]*)\)/g,
    (_, tabla, columnas) => `REFERENCES dbo.${tabla} (${renombrarEnTabla(tabla, columnas)})`);
  // 2. Cada tabla, cada índice y cada trigger, con las columnas de su tabla.
  sql = sql.replace(/CREATE TABLE dbo\.(\w+) \(\n[\s\S]*?\n\);/g, (bloque, tabla) => renombrarEnTabla(tabla, bloque));
  sql = sql.replace(/CREATE (?:UNIQUE )?INDEX \w+ ON dbo\.(\w+) .*/g, (linea, tabla) => renombrarEnTabla(tabla, linea));
  sql = sql.replace(/CREATE OR ALTER TRIGGER dbo\.\w+ ON dbo\.(\w+)[\s\S]*?\nGO/g, (bloque, tabla) => renombrarEnTabla(tabla, bloque));
  // 3. Las llaves foráneas, en todo el script.
  sql = sql.replace(new RegExp(`\\b(${Object.keys(FORANEAS).join('|')})\\b`, 'g'), (columna) => FORANEAS[columna]);
  // 4. Activo pasa a ser la columna ESTADO de la auditoría.
  sql = sql.replace(/^ {4}Activo +bit .*\n/gm, '');
  const sinComentarios = sql.replace(/--.*$/gm, '').replace(/\/\*[\s\S]*?\*\//g, '');
  if (/\b(Id|Activo)\b/.test(sinComentarios)) {
    throw new Error('Quedó una columna Id o Activo sin renombrar.');
  }
  return sql;
}

/** Agrega a cada tabla las columnas de auditoría, con su regla de ESTADO, y alinea las definiciones de columnas. */
function agregarAuditoria(texto) {
  return texto.replace(/CREATE TABLE dbo\.(\w+) \(\n([\s\S]*?)\n\);/g, (_, tabla, cuerpo) => {
    const nombre = /CONSTRAINT PK_SEFH_(\w+) PRIMARY KEY/.exec(cuerpo)?.[1];
    if (!nombre) {
      throw new Error(`La tabla ${tabla} no tiene llave primaria.`);
    }
    const lineas = cuerpo.split('\n');
    lineas.splice(lineas.findIndex((l) => l.startsWith('    CONSTRAINT ')), 0,
      `    ESTADO varchar(10)     NOT NULL CONSTRAINT DF_SEFH_${nombre}_ESTADO DEFAULT ('ACTIVO'),`,
      `    USU_CRE varchar(60)     NOT NULL CONSTRAINT DF_SEFH_${nombre}_USU_CRE DEFAULT (LEFT(SUSER_SNAME(), 60)),`,
      `    FEC_CRE datetime2(7)    NOT NULL CONSTRAINT DF_SEFH_${nombre}_FEC_CRE DEFAULT (SYSDATETIME()),`,
      '    USU_MOD varchar(60)     NULL,',
      '    FEC_MOD datetime2(7)    NULL,');
    // La regla de ESTADO va al final: la línea anterior (sin contar comentarios) necesita su coma.
    let ultima = lineas.length - 1;
    while (/^\s*--/.test(lineas[ultima])) {
      ultima -= 1;
    }
    lineas[ultima] = lineas[ultima].replace(/^(.*?)(\s+--.*)?$/, (__, codigo, comentario = '') => `${codigo},${comentario}`);
    lineas.push(`    CONSTRAINT CK_SEFH_${nombre}_ESTADO CHECK (ESTADO IN ('ACTIVO', 'INACTIVO'))`);
    // Columnas alineadas: los nombres nuevos son más largos que los anteriores.
    const columnas = lineas.map((l) => /^ {4}(?!CONSTRAINT )(\w+) +(\S.*)$/.exec(l));
    const ancho = Math.max(...columnas.filter(Boolean).map((m) => m[1].length)) + 4;
    const alineadas = lineas.map((l, i) => (columnas[i] ? `    ${columnas[i][1].padEnd(ancho)}${columnas[i][2]}` : l));
    return `CREATE TABLE dbo.${tabla} (\n${alineadas.join('\n')}\n);`;
  });
}

// ---------- Datos ----------

const texto = (v) => `N'${String(v).replace(/'/g, "''")}'`;
const valor = {
  /** Texto; ausente es NULL. */
  t: (v) => (v === undefined || v === null ? 'NULL' : texto(v)),
  /** Lista opcional: vacía es NULL. */
  o: (v) => (v === undefined || v === null || v === '' ? 'NULL' : texto(v)),
  n: (v) => (v === undefined || v === null ? 'NULL' : String(Number(v))),
  b: (v) => (v ? '1' : '0'),
  j: (v) => (v === undefined || v === null ? 'NULL' : texto(JSON.stringify(v))),
  /** Lógico guardado en la columna ESTADO de la auditoría. */
  e: (v) => (v ? "'ACTIVO'" : "'INACTIVO'"),
};

/** Nombre de la columna en las tablas SEFH_ (las definiciones de abajo usan los nombres de las propiedades). */
function columnaNueva(tabla, [columna, propiedad, tipo, predeterminado]) {
  if (columna === 'Activo') {
    return ['ESTADO', propiedad, 'e', predeterminado];
  }
  const nueva = (columna === 'Id' && LLAVES[tabla]) || PROPIAS[tabla]?.[columna]?.[0] || FORANEAS[columna] || columna;
  return [nueva, propiedad, tipo, predeterminado];
}

/** Una columna: [columna, propiedad del JSON, tipo, valor si falta]. */
function insertar(tabla, definiciones, filas, omitir = []) {
  const columnas = definiciones.map((d) => columnaNueva(tabla, d));
  const conocidas = new Set([...columnas.map(([, propiedad]) => propiedad), ...omitir]);
  const lineas = filas.map((fila) => {
    for (const clave of Object.keys(fila)) {
      if (!conocidas.has(clave)) {
        throw new Error(`${tabla}: la propiedad «${clave}» no tiene columna.`);
      }
    }
    const valores = columnas.map(([, propiedad, tipo, predeterminado]) => valor[tipo](fila[propiedad] ?? predeterminado));
    return `INSERT INTO dbo.${tabla} (${columnas.map(([c]) => c).join(', ')})\nVALUES (${valores.join(', ')});`;
  });
  conteos.push([tabla, filas.length]);
  return `-- ${tabla}: ${filas.length} ${filas.length === 1 ? 'fila' : 'filas'}\n${lineas.join('\n')}\n`;
}

/** Ordena de modo que cada fila quede después de la que referencia en la misma tabla. */
function padresPrimero(filas, referencia) {
  const pendientes = [...filas];
  const orden = [];
  const listos = new Set();
  while (pendientes.length) {
    const i = pendientes.findIndex((f) => !referencia(f) || listos.has(referencia(f)));
    if (i < 0) {
      throw new Error(`Referencia circular o inexistente: ${pendientes[0].id}`);
    }
    const [fila] = pendientes.splice(i, 1);
    listos.add(fila.id);
    orden.push(fila);
  }
  return orden;
}

const col = (texto2) =>
  texto2.split(/\s+/).filter(Boolean).map((definicion) => {
    const [columna, tipo, predeterminado] = definicion.split(':');
    const propiedad = columna[0].toLowerCase() + columna.slice(1);
    return [columna, propiedad, tipo ?? 't', predeterminado === 'true' ? true : predeterminado === 'false' ? false : predeterminado];
  });

const conteos = [];

function generarDatos() {
  const usuarios = leer('usuarios');
  const bloques = leer('bloques');
  const tipos = padresPrimero(leer('tiposDocumento'), (t) => t.copiaDeTipoDocumentoId);
  const campos = leer('campos');
  const consolidados = leer('consolidados');
  const versiones = padresPrimero(leer('versionesPlantilla'), (v) => v.versionAnteriorId);
  const propuestas = padresPrimero(leer('propuestas'), (p) => p.derivadaDePropuestaId);
  const devoluciones = leer('devoluciones');
  const version = ['Version', 'version', 'n', 1];

  // Último número por año, para que el siguiente documento siga la numeración.
  const secuencias = [...consolidados.reduce((m, c) => m.set(c.anio, Math.max(m.get(c.anio) ?? 0, c.secuencia)), new Map())]
    .map(([anio, ultimo]) => ({ anio, ultimo }));

  // D4: en un documento abierto, cada plantilla queda bloqueada para los demás.
  const abiertos = new Set(consolidados.filter((c) => c.estado === 'EnRegistro' || c.estado === 'Verificado').map((c) => c.id));
  const bloqueos = propuestas
    .filter((p) => abiertos.has(p.consolidadoId) && !p.retirada)
    .map(({ unidadId, rubroId, subRubroId, tipoDocumentoId, claseDocumentoId, beneficioId, consolidadoId, id, fechaCreacion }) => (
      { unidadId, rubroId, subRubroId, tipoDocumentoId, claseDocumentoId, beneficioId, consolidadoId, propuestaId: id, fecha: fechaCreacion }));

  const partes = [
    insertar('SEFH_USUARIOS', [...col('Id NombreCompleto Activo:b'), version], usuarios, ['roles']),
    insertar('SEFH_USUARIO_ROLES', col('UsuarioId Rol'), usuarios.flatMap((u) => u.roles.map((rol) => ({ usuarioId: u.id, rol })))),
    insertar('SEFH_UNIDADES', [...col('Id Nombre Orden:n Activo:b'), version], leer('unidades')),
    insertar('SEFH_RUBROS', [...col('Id Nombre Activo:b'), version], leer('rubros')),
    insertar('SEFH_SUB_RUBROS', [...col('Id RubroId Nombre Activo:b'), version], leer('subRubros')),
    insertar('SEFH_CLASES_DOCUMENTO', [...col('Id Nombre Activo:b'), version], leer('clasesDocumento')),
    insertar('SEFH_BENEFICIOS', [...col('Id Nombre Activo:b'), version], leer('beneficios')),
    insertar('SEFH_BLOQUES_DOCUMENTALES', [...col('Id Nombre Titulo Numeracion EsFirmas:b ContextoCampos Descripcion Activo:b'), version], bloques, ['reemplaza']),
    insertar('SEFH_BLOQUE_REEMPLAZOS', col('BloqueId ReemplazaBloqueId'),
      bloques.flatMap((b) => (b.reemplaza ?? []).map((r) => ({ bloqueId: b.id, reemplazaBloqueId: r })))),
    insertar('SEFH_TIPOS_DOCUMENTO', [...col('Id Nombre TextoCorrido:b CopiaDeTipoDocumentoId Activo:b'), version], tipos, ['bloques']),
    insertar('SEFH_TIPO_DOCUMENTO_BLOQUES', col('TipoDocumentoId BloqueId Orden:n Obligatorio:b'),
      tipos.flatMap((t) => (t.bloques ?? []).map((b) => ({ tipoDocumentoId: t.id, ...b })))),
    insertar('SEFH_CAMPOS', [...col('Id Etiqueta Categoria TipoDato OrigenColeccion OrigenPropiedad Descripcion AlertarCambios:b'), version],
      campos.map(({ origen, ...c }) => ({ ...c, origenColeccion: origen.coleccion, origenPropiedad: origen.propiedad })), ['contextos']),
    insertar('SEFH_CAMPO_CONTEXTOS', col('CampoId Contexto'), campos.flatMap((c) => c.contextos.map((contexto) => ({ campoId: c.id, contexto })))),
    insertar('SEFH_ARCHIVOS', [...col('Id NombreOriginal TipoContenido Tamano:n Sha256 FechaCarga'), version], leer('archivos')),
    insertar('SEFH_PLANTILLAS',
      [...col('Id Nombre NombreReferencia UnidadId RubroId SubRubroId TipoDocumentoId:o ClaseDocumentoId:o BeneficioId:o Estado:t:Borrador Descripcion LogoId AlineacionLogo:t:Centro PiePagina EspacioFirmasCm:n:1.2 FechaModificacion'), version],
      leer('plantillas')),
    insertar('SEFH_PARRAFOS',
      [...col('Id PlantillaId Seccion Orden:n Contenido:j Activo:b MostrarSoloSiCumple:b UnirConAnterior:b EsTitulo:b Condicion:j OrigenParrafoId FechaModificacion'),
        ['CamposUsados', 'camposUsados', 'j', []], version],
      leer('parrafos')),
    insertar('SEFH_SECUENCIAS_PLAN', col('Anio:n Ultimo:n'), secuencias),
    insertar('SEFH_PLANTILLAS_DOC_CAB',
      [...col('Id Numero Anio:n Secuencia:n Estado UnidadId AnalistaId FechaCreacion FechaActualizacion NumeroPresentacion:n FechaEnvio EnviadoPor RevisorId FechaRevision AprobadorId FechaAprobacion'), version],
      consolidados),
    insertar('SEFH_PLANTILLAS_CONSOLIDADO',
      [...col('Id PlantillaId NumeroVersion:n Vigencia Contenido:j Huella VersionAnteriorId ConsolidadoId CargaInicial:b PublicadaPor FechaPublicacion InicioVigencia FinVigencia'), version],
      versiones),
    insertar('SEFH_PLANTILLA_DOC',
      [...col('Id ConsolidadoId Operacion UnidadId RubroId SubRubroId TipoDocumentoId:o ClaseDocumentoId:o BeneficioId:o Nombre NombreReferencia PlantillaId VersionOrigenId HuellaOrigen ContenidoPropuesto:j DerivadaDePropuestaId Retirada:b MotivoRetiro FechaCreacion FechaModificacion'), version],
      propuestas),
    insertar('SEFH_BLOQUEOS_PLANTILLA', col('UnidadId RubroId SubRubroId TipoDocumentoId:o ClaseDocumentoId:o BeneficioId:o ConsolidadoId PropuestaId Fecha'), bloqueos),
    insertar('SEFH_PRESENTACIONES', [...col('Id ConsolidadoId Numero:n Usuario Fecha CantidadPlantillas:n HuellaIntegridad Propuestas:j'), version], leer('presentaciones')),
    insertar('SEFH_DEVOLUCIONES', [...col('Id ConsolidadoId NumeroPresentacion:n Usuario Fecha Justificacion'), version], devoluciones, ['observaciones']),
    insertar('SEFH_OBSERVACIONES', col('DevolucionId Orden:n PropuestaId BloqueCodigo ParrafoId Problema Recomendacion'),
      devoluciones.flatMap((d) => (d.observaciones ?? []).map((o, i) => ({ ...o, devolucionId: d.id, orden: i + 1 })))),
    insertar('SEFH_MOVIMIENTOS', [...col('Id ConsolidadoId Tipo Usuario Fecha EstadoOrigen EstadoDestino NumeroPresentacion:n Detalle'), version], leer('movimientos')),
    insertar('SEFH_HISTORIAL', [...col('Id Fecha Usuario Coleccion EntidadId Accion VersionEntidad:n Anterior:j Nuevo:j'), version], leer('historial')),
    insertar('SEFH_SOLICITUDES',
      [...col('Id NumeroSolicitud Operador Beneficiario RubroId SubRubroId TipoBeneficio Estado FechaResolucion AnioGestion:n MontoExoneracion:n UnidadEjecutora Departamento Municipio EsDatoPrueba:b'), version],
      leer('solicitudes')),
    insertar('SEFH_DETALLES_BIENES',
      [...col('Id SolicitudId CodigoArancelario Descripcion UnidadMedida Cantidad:n ValorEstimado:n DescripcionArancelaria MontoAutorizado:n EsDatoPrueba:b'), version],
      leer('detallesBienes')),
  ];

  const resumen = conteos.map(([t, n]) => `SELECT N'${t}' AS Tabla, ${n} AS Esperadas, COUNT(*) AS Cargadas FROM dbo.${t}`).join('\nUNION ALL ');

  return `/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Datos de las tablas SEFH_*
   ---------------------------------------------------------------------------------------------------------------------
   Generado por generar-sefh.js a partir de los archivos JSON en uso (backend/EditorResoluciones.Api/Data).
   Todos los datos son ficticios, de prueba. Ejecute antes 01-SEFH-esquema.sql.
   Es una sola transacción: si una fila falla, no se carga nada. Al final muestra las filas esperadas y las cargadas.
   ===================================================================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

${partes.join('\n')}
COMMIT TRANSACTION;

${resumen};
`;
}

escribir('01-SEFH-esquema.sql', generarEsquema());
escribir('02-SEFH-datos.sql', generarDatos());
for (const [tabla, filas] of conteos) {
  console.log(tabla.padEnd(30), filas);
}
