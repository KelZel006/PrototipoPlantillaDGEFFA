// Genera los scripts de SQL Server con las tablas PAM_*:
//   01-PAM-esquema.sql  a partir de ../sqlserver/01-esquema.sql, más lo que ese esquema no guardaba
//   02-PAM-datos.sql    a partir de backend/EditorResoluciones.Api/Data (inicie la API una vez antes)
// Uso, desde la raíz del repositorio: node database/sqlserver-pam/generar-pam.js
// Falla si una colección trae una propiedad sin columna: así ningún dato se pierde sin avisar.
const fs = require('fs');
const path = require('path');

const raiz = path.resolve(__dirname, '..', '..');
const DATA = path.join(raiz, 'backend', 'EditorResoluciones.Api', 'Data');
const leer = (nombre) => JSON.parse(fs.readFileSync(path.join(DATA, `${nombre}.json`), 'utf8'));
// UTF-8 con BOM y CRLF: SSMS y sqlcmd conservan así las tildes.
const escribir = (nombre, texto) => fs.writeFileSync(path.join(__dirname, nombre), `﻿${texto.replace(/\r?\n/g, '\r\n')}`);

const TABLAS = {
  Usuarios: 'PAM_USUARIOS', UsuarioRoles: 'PAM_USUARIO_ROLES', Unidades: 'PAM_UNIDADES', Rubros: 'PAM_RUBROS',
  SubRubros: 'PAM_SUB_RUBROS', ClasesDocumento: 'PAM_CLASES_DOCUMENTO', Beneficios: 'PAM_BENEFICIOS',
  BloquesDocumentales: 'PAM_BLOQUES_DOCUMENTALES', BloqueReemplazos: 'PAM_BLOQUE_REEMPLAZOS',
  TiposDocumento: 'PAM_TIPOS_DOCUMENTO', TipoDocumentoBloques: 'PAM_TIPO_DOCUMENTO_BLOQUES', Campos: 'PAM_CAMPOS',
  CampoContextos: 'PAM_CAMPO_CONTEXTOS', Archivos: 'PAM_ARCHIVOS', Plantillas: 'PAM_PLANTILLAS',
  VersionesPlantilla: 'PAM_VERSIONES_PLANTILLA', SecuenciasPlan: 'PAM_SECUENCIAS_PLAN', Consolidados: 'PAM_CONSOLIDADOS',
  Propuestas: 'PAM_PROPUESTAS', BloqueosPlantilla: 'PAM_BLOQUEOS_PLANTILLA', Presentaciones: 'PAM_PRESENTACIONES',
  Devoluciones: 'PAM_DEVOLUCIONES', Observaciones: 'PAM_OBSERVACIONES', Movimientos: 'PAM_MOVIMIENTOS',
  Historial: 'PAM_HISTORIAL', Solicitudes: 'PAM_SOLICITUDES', DetallesBienes: 'PAM_DETALLES_BIENES',
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

  // Encabezado, esquema propio y permisos: se reescriben para las tablas PAM_* en dbo.
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
  sql = reemplazar(sql, `CREATE TABLE plantillas.VersionesPlantilla (`, `-- Párrafos anteriores a las versiones publicadas: la aplicación los usa para la carga inicial de cada plantilla.
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

CREATE TABLE plantillas.VersionesPlantilla (`);

  // Nombres: tablas PAM_* en dbo; restricciones, índices y triggers con el mismo prefijo.
  const tablas = { ...TABLAS, Parrafos: 'PAM_PARRAFOS' };
  sql = sql.replace(/plantillas\.([A-Za-z]+)/g, (_, tabla) => {
    if (tablas[tabla]) {
      return `dbo.${tablas[tabla]}`;
    }
    return `dbo.${tabla.replace(/^TR_/, 'TR_PAM_')}`;
  });
  sql = sql.replace(/\b(PK|FK|UQ|CK|DF|IX|UX|TR)_(?!PAM_)/g, '$1_PAM_');

  const nombres = Object.values(tablas);
  const soloInsercion = ['PAM_HISTORIAL', 'PAM_MOVIMIENTOS', 'PAM_PRESENTACIONES', 'PAM_DEVOLUCIONES', 'PAM_OBSERVACIONES'];
  const permisos = [
    ...nombres.map((t) => `GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.${t} TO PAM_ROL_EDITOR;`),
    ...soloInsercion.map((t) => `DENY UPDATE, DELETE ON dbo.${t} TO PAM_ROL_EDITOR;`),
    'DENY DELETE ON dbo.PAM_VERSIONES_PLANTILLA TO PAM_ROL_EDITOR;',
  ].join('\n');

  return `/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Esquema para SQL Server 2019 con tablas PAM_*
   ---------------------------------------------------------------------------------------------------------------------
   Generado por generar-pam.js a partir de ../sqlserver/01-esquema.sql. No lo edite a mano.
   - Crea ${nombres.length} tablas en el esquema dbo, todas con el prefijo PAM_.
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

${sql}/* ---------------------------------------------------------------------------------------------------------------------
   9. Permisos del usuario de la aplicación (ajustar el nombre del usuario o rol institucional)
   --------------------------------------------------------------------------------------------------------------------- */
IF DATABASE_PRINCIPAL_ID(N'PAM_ROL_EDITOR') IS NULL
    CREATE ROLE PAM_ROL_EDITOR;
GO
${permisos}
-- ALTER ROLE PAM_ROL_EDITOR ADD MEMBER [usuario_de_la_aplicacion];
GO
`;
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
};

/** Una columna: [columna, propiedad del JSON, tipo, valor si falta]. */
function insertar(tabla, columnas, filas, omitir = []) {
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
    insertar('PAM_USUARIOS', [...col('Id NombreCompleto Activo:b'), version], usuarios, ['roles']),
    insertar('PAM_USUARIO_ROLES', col('UsuarioId Rol'), usuarios.flatMap((u) => u.roles.map((rol) => ({ usuarioId: u.id, rol })))),
    insertar('PAM_UNIDADES', [...col('Id Nombre Orden:n Activo:b'), version], leer('unidades')),
    insertar('PAM_RUBROS', [...col('Id Nombre Activo:b'), version], leer('rubros')),
    insertar('PAM_SUB_RUBROS', [...col('Id RubroId Nombre Activo:b'), version], leer('subRubros')),
    insertar('PAM_CLASES_DOCUMENTO', [...col('Id Nombre Activo:b'), version], leer('clasesDocumento')),
    insertar('PAM_BENEFICIOS', [...col('Id Nombre Activo:b'), version], leer('beneficios')),
    insertar('PAM_BLOQUES_DOCUMENTALES', [...col('Id Nombre Titulo Numeracion EsFirmas:b ContextoCampos Descripcion Activo:b'), version], bloques, ['reemplaza']),
    insertar('PAM_BLOQUE_REEMPLAZOS', col('BloqueId ReemplazaBloqueId'),
      bloques.flatMap((b) => (b.reemplaza ?? []).map((r) => ({ bloqueId: b.id, reemplazaBloqueId: r })))),
    insertar('PAM_TIPOS_DOCUMENTO', [...col('Id Nombre TextoCorrido:b CopiaDeTipoDocumentoId Activo:b'), version], tipos, ['bloques']),
    insertar('PAM_TIPO_DOCUMENTO_BLOQUES', col('TipoDocumentoId BloqueId Orden:n Obligatorio:b'),
      tipos.flatMap((t) => (t.bloques ?? []).map((b) => ({ tipoDocumentoId: t.id, ...b })))),
    insertar('PAM_CAMPOS', [...col('Id Etiqueta Categoria TipoDato OrigenColeccion OrigenPropiedad Descripcion AlertarCambios:b'), version],
      campos.map(({ origen, ...c }) => ({ ...c, origenColeccion: origen.coleccion, origenPropiedad: origen.propiedad })), ['contextos']),
    insertar('PAM_CAMPO_CONTEXTOS', col('CampoId Contexto'), campos.flatMap((c) => c.contextos.map((contexto) => ({ campoId: c.id, contexto })))),
    insertar('PAM_ARCHIVOS', [...col('Id NombreOriginal TipoContenido Tamano:n Sha256 FechaCarga'), version], leer('archivos')),
    insertar('PAM_PLANTILLAS',
      [...col('Id Nombre NombreReferencia UnidadId RubroId SubRubroId TipoDocumentoId:o ClaseDocumentoId:o BeneficioId:o Estado:t:Borrador Descripcion LogoId AlineacionLogo:t:Centro PiePagina EspacioFirmasCm:n:1.2 FechaModificacion'), version],
      leer('plantillas')),
    insertar('PAM_PARRAFOS',
      [...col('Id PlantillaId Seccion Orden:n Contenido:j Activo:b MostrarSoloSiCumple:b UnirConAnterior:b EsTitulo:b Condicion:j OrigenParrafoId FechaModificacion'),
        ['CamposUsados', 'camposUsados', 'j', []], version],
      leer('parrafos')),
    insertar('PAM_SECUENCIAS_PLAN', col('Anio:n Ultimo:n'), secuencias),
    insertar('PAM_CONSOLIDADOS',
      [...col('Id Numero Anio:n Secuencia:n Estado UnidadId AnalistaId FechaCreacion FechaActualizacion NumeroPresentacion:n FechaEnvio EnviadoPor RevisorId FechaRevision AprobadorId FechaAprobacion'), version],
      consolidados),
    insertar('PAM_VERSIONES_PLANTILLA',
      [...col('Id PlantillaId NumeroVersion:n Vigencia Contenido:j Huella VersionAnteriorId ConsolidadoId CargaInicial:b PublicadaPor FechaPublicacion InicioVigencia FinVigencia'), version],
      versiones),
    insertar('PAM_PROPUESTAS',
      [...col('Id ConsolidadoId Operacion UnidadId RubroId SubRubroId TipoDocumentoId:o ClaseDocumentoId:o BeneficioId:o Nombre NombreReferencia PlantillaId VersionOrigenId HuellaOrigen ContenidoPropuesto:j DerivadaDePropuestaId Retirada:b MotivoRetiro FechaCreacion FechaModificacion'), version],
      propuestas),
    insertar('PAM_BLOQUEOS_PLANTILLA', col('UnidadId RubroId SubRubroId TipoDocumentoId:o ClaseDocumentoId:o BeneficioId:o ConsolidadoId PropuestaId Fecha'), bloqueos),
    insertar('PAM_PRESENTACIONES', [...col('Id ConsolidadoId Numero:n Usuario Fecha CantidadPlantillas:n HuellaIntegridad Propuestas:j'), version], leer('presentaciones')),
    insertar('PAM_DEVOLUCIONES', [...col('Id ConsolidadoId NumeroPresentacion:n Usuario Fecha Justificacion'), version], devoluciones, ['observaciones']),
    insertar('PAM_OBSERVACIONES', col('DevolucionId Orden:n PropuestaId BloqueCodigo ParrafoId Problema Recomendacion'),
      devoluciones.flatMap((d) => (d.observaciones ?? []).map((o, i) => ({ ...o, devolucionId: d.id, orden: i + 1 })))),
    insertar('PAM_MOVIMIENTOS', [...col('Id ConsolidadoId Tipo Usuario Fecha EstadoOrigen EstadoDestino NumeroPresentacion:n Detalle'), version], leer('movimientos')),
    insertar('PAM_HISTORIAL', [...col('Id Fecha Usuario Coleccion EntidadId Accion VersionEntidad:n Anterior:j Nuevo:j'), version], leer('historial')),
    insertar('PAM_SOLICITUDES',
      [...col('Id NumeroSolicitud Operador Beneficiario RubroId SubRubroId TipoBeneficio Estado FechaResolucion AnioGestion:n MontoExoneracion:n UnidadEjecutora Departamento Municipio EsDatoPrueba:b'), version],
      leer('solicitudes')),
    insertar('PAM_DETALLES_BIENES',
      [...col('Id SolicitudId CodigoArancelario Descripcion UnidadMedida Cantidad:n ValorEstimado:n DescripcionArancelaria MontoAutorizado:n EsDatoPrueba:b'), version],
      leer('detallesBienes')),
  ];

  const resumen = conteos.map(([t, n]) => `SELECT N'${t}' AS Tabla, ${n} AS Esperadas, COUNT(*) AS Cargadas FROM dbo.${t}`).join('\nUNION ALL ');

  return `/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Datos de las tablas PAM_*
   ---------------------------------------------------------------------------------------------------------------------
   Generado por generar-pam.js a partir de los archivos JSON en uso (backend/EditorResoluciones.Api/Data).
   Todos los datos son ficticios, de prueba. Ejecute antes 01-PAM-esquema.sql.
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

escribir('01-PAM-esquema.sql', generarEsquema());
escribir('02-PAM-datos.sql', generarDatos());
for (const [tabla, filas] of conteos) {
  console.log(tabla.padEnd(30), filas);
}
