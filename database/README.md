# Base de datos del Sistema de Gestión de Plantillas

Scripts para crear las tablas del sistema en **SQL Server 2019** y en **PostgreSQL 13 o superior**. El modelo es el
mismo en los dos y sigue la estrategia de [docs/CONSOLIDADO-FASE8.md](../docs/CONSOLIDADO-FASE8.md). La elección
del motor (decisión D12) sigue pendiente.

| Motor | Script | Convención de nombres |
|---|---|---|
| SQL Server 2019 | [sqlserver/01-esquema.sql](sqlserver/01-esquema.sql) | `plantillas.PlantillasConsolidado.NumeroVersion` |
| PostgreSQL | [postgresql/01-esquema.sql](postgresql/01-esquema.sql) | `plantillas.plantillas_consolidado.numero_version` |

## Cómo ejecutarlos

Los scripts se ejecutan una vez sobre una base vacía y crean todo dentro del esquema `plantillas`.

**SQL Server.**
1. Cree la base con la intercalación recomendada: `CREATE DATABASE PlantillasDGEFFA COLLATE Modern_Spanish_CI_AI`.
   La línea está comentada al inicio del script.
2. Ejecute el script en SSMS o con `sqlcmd -i 01-esquema.sql`; trae separadores `GO`.
3. Mantenga `ANSI_NULLS` y `QUOTED_IDENTIFIER` en `ON`, que es lo predeterminado. Lo exige el índice filtrado de la
   regla 14.

**PostgreSQL.**
1. Cree la base en **UTF-8**; la línea está comentada al inicio del script.
2. Ejecute `psql -d plantillas_dgeffa -f 01-esquema.sql`.

Los dos scripts crean el rol `rol_editor_plantillas` con los permisos de la aplicación. Asígnelo al usuario con el
que se conecte la API; la línea está comentada al final.

## Scripts con tablas SEFH_* (SQL Server)

En [sqlserver-sefh/](sqlserver-sefh/) están el esquema y los datos del sistema con todas las tablas en `dbo` y el prefijo
`SEFH_` (`SEFH_PLANTILLAS`, `SEFH_RUBROS`…):

1. [01-SEFH-esquema.sql](sqlserver-sefh/01-SEFH-esquema.sql): 28 tablas, restricciones, índices, triggers y el rol `SEFH_ROL_EDITOR`.
2. [02-SEFH-datos.sql](sqlserver-sefh/02-SEFH-datos.sql): los datos de `backend/EditorResoluciones.Api/Data`, en una sola transacción. Al final muestra las filas esperadas y las cargadas.

Ejecútelos una vez, en ese orden. Con sqlcmd: `sqlcmd -d SuBase -f 65001 -b -i 01-SEFH-esquema.sql`.

Para actualizarlos con los datos del momento: `node database/sqlserver-sefh/generar-sefh.js`. El generador parte de
`sqlserver/01-esquema.sql` y le agrega lo que ese esquema no guarda: `SEFH_PARRAFOS`, y en plantillas, propuestas e historial
las columnas `NombreReferencia`, `Estado`, `Descripcion`, `LogoId`, `AlineacionLogo`, `PiePagina`, `EspacioFirmasCm` y
`Version`. Después aplica los nombres y la auditoría que se describen abajo. Si una colección trae una propiedad sin columna,
se detiene con error.

**Convenciones de nombres y auditoría (18/11/2026):**

- **Llave primaria `ID_<siglas>_SEQ`.** Guarda el mismo código de antes (`PLA-001`, `exoneraciones`…); solo cambia el nombre.

  | Tabla | Llave | Tabla | Llave |
  |---|---|---|---|
  | `SEFH_USUARIOS` | `ID_USU_SEQ` | `SEFH_PARRAFOS` | `ID_PARR_SEQ` |
  | `SEFH_UNIDADES` | `ID_UNI_SEQ` | `SEFH_PLANTILLAS_CONSOLIDADO` | `ID_VERPLA_SEQ` |
  | `SEFH_RUBROS` | `ID_RUBRO_SEQ` | `SEFH_PLANTILLAS_DOC_CAB` | `ID_CONS_SEQ` |
  | `SEFH_SUB_RUBROS` | `ID_SUBRUBRO_SEQ` | `SEFH_PLANTILLA_DOC` | `ID_PLADOC_SEQ` |
  | `SEFH_CLASES_DOCUMENTO` | `ID_CLADOC_SEQ` | `SEFH_PRESENTACIONES` | `ID_PRES_SEQ` |
  | `SEFH_BENEFICIOS` | `ID_BEN_SEQ` | `SEFH_DEVOLUCIONES` | `ID_DEV_SEQ` |
  | `SEFH_BLOQUES_DOCUMENTALES` | `ID_BLODOC_SEQ` | `SEFH_OBSERVACIONES` | `ID_OBS_SEQ` |
  | `SEFH_TIPOS_DOCUMENTO` | `ID_TIPDOC_SEQ` | `SEFH_MOVIMIENTOS` | `ID_MOV_SEQ` |
  | `SEFH_CAMPOS` | `ID_CAMPO_SEQ` | `SEFH_HISTORIAL` | `ID_HIST_SEQ` |
  | `SEFH_ARCHIVOS` | `ID_ARCH_SEQ` | `SEFH_SOLICITUDES` | `ID_SOL_SEQ` |
  | `SEFH_PLANTILLAS` | `ID_PLA_SEQ` | `SEFH_DETALLES_BIENES` | `ID_DETBIEN_SEQ` |

  Las seis tablas con llave compuesta (`SEFH_USUARIO_ROLES`, `SEFH_BLOQUE_REEMPLAZOS`, `SEFH_TIPO_DOCUMENTO_BLOQUES`,
  `SEFH_CAMPO_CONTEXTOS`, `SEFH_SECUENCIAS_PLAN` y `SEFH_BLOQUEOS_PLANTILLA`) no tenían columna `Id`.
- **Llaves foráneas con el nombre de la llave a la que apuntan:** `UnidadId` → `ID_UNI_SEQ`, `PlantillaId` → `ID_PLA_SEQ`,
  `LogoId` → `ID_ARCH_SEQ`, el `Usuario` de presentaciones y devoluciones → `ID_USU_SEQ`, etc. Conservan su nombre las que
  apuntan a su propia tabla (`CopiaDeTipoDocumentoId`, `VersionAnteriorId`, `DerivadaDePropuestaId`) y las que apuntan a una
  tabla a la que la fila ya apunta con otra columna (`ReemplazaBloqueId`, `AnalistaId`, `EnviadoPor`, `RevisorId`, `AprobadorId`).
- **Auditoría en todas las tablas:** `ESTADO` (`'ACTIVO'`/`'INACTIVO'`), `USU_CRE`, `FEC_CRE`, `USU_MOD` y `FEC_MOD`. La API
  guarda en `USU_CRE`/`USU_MOD` el usuario de la operación; `FEC_CRE` y `FEC_MOD` las pone el motor (`SYSDATETIME()`). Al
  ejecutar los scripts a mano, `USU_CRE` toma el inicio de sesión de SQL Server.
- **`ESTADO` reemplaza a `Activo`** en usuarios, catálogos y párrafos. El estado del negocio cambió de nombre para no chocar:
  `ESTADO_PLANTILLA` (Borrador/Publicada), `ESTADO_DOCUMENTO` (EnRegistro…Aprobado) y `ESTADO_SOLICITUD`.

**Verificación (06/11/2026), en SQL Server 2019 real (15.0.4500):** los dos scripts se ejecutaron sobre una base vacía, con
`Modern_Spanish_CI_AI` y con la intercalación del servidor; las 28 tablas cargaron sus filas esperadas; el contenido JSON,
los textos y los importes son idénticos al origen; y las reglas del motor rechazaron cada intento de romperlas.

**La aplicación usa estas tablas** desde el 13/11/2026 (`backend/EditorResoluciones.Infraestructura/Sql/`). Todas las tablas
llevan `OrdenRegistro` (autonumérica), salvo `SEFH_OBSERVACIONES`, que ya tiene su `Orden`: la aplicación lista los registros
en el orden en que se agregaron, como hacía con los archivos JSON. Comprobado en SQL Server 2025 (17.0.1000): con los mismos
datos, las 312 respuestas de lectura de la API fueron idénticas leyendo de SQL Server y de los archivos JSON, y las pruebas
`AlmacenSqlServerPruebas` recorren el ciclo completo de un documento (crear, editar, logo, enviar, devolver, aprobar).

## Tablas

| Grupo | Colección JSON actual | SQL Server | PostgreSQL |
|---|---|---|---|
| Usuarios | `usuarios` | `Usuarios`, `UsuarioRoles` | `usuarios`, `usuario_roles` |
| Catálogos | `unidades` | `Unidades` | `unidades` |
| | `rubros`, `subRubros` | `Rubros`, `SubRubros` | `rubros`, `sub_rubros` |
| | `clasesDocumento` | `ClasesDocumento` | `clases_documento` |
| | `beneficios` | `Beneficios` | `beneficios` |
| | `bloques` (con los bloques que reemplaza cada uno) | `BloquesDocumentales`, `BloqueReemplazos` | `bloques_documentales`, `bloque_reemplazos` |
| | `tiposDocumento` (con sus bloques) | `TiposDocumento`, `TipoDocumentoBloques` | `tipos_documento`, `tipo_documento_bloques` |
| | `campos` (con sus contextos) | `Campos`, `CampoContextos` | `campos`, `campo_contextos` |
| Archivos | `archivos` y la carpeta `archivos/` | `Archivos` | `archivos` |
| Plantillas | `plantillas` (identidad) | `Plantillas` | `plantillas` |
| | `versionesPlantilla` | `PlantillasConsolidado` | `plantillas_consolidado` |
| Documento Consolidado | número PLAN | `SecuenciasPlan` | `secuencias_plan` |
| | `consolidados` | `PlantillasDocCab` | `plantillas_doc_cab` |
| | `propuestas` | `PlantillaDoc` | `plantilla_doc` |
| | (D4, nueva) | `BloqueosPlantilla` | `bloqueos_plantilla` |
| | `presentaciones` | `Presentaciones` | `presentaciones` |
| | `devoluciones` (con sus observaciones) | `Devoluciones`, `Observaciones` | `devoluciones`, `observaciones` |
| Auditoría | `movimientos` | `Movimientos` | `movimientos` |
| | `historial` | `Historial` | `historial` |
| Datos de origen | `solicitudes`, `detallesBienes` | `Solicitudes`, `DetallesBienes` | `solicitudes`, `detalles_bienes` |

**Decisiones del diseño:**
- **Mismos identificadores que el JSON** (`PLA-001`, `CON-0001`…), para importar sin traducir.
- **Enumeraciones como texto**, igual que en el JSON (`EnRegistro`, `Vigente`), validadas con `CHECK`.
- **Contenido de versiones y propuestas como JSON** (`nvarchar(max)` con `ISJSON` en SQL Server, `jsonb` en
  PostgreSQL). Es una fotografía inmutable. La huella SHA-256 la calcula la aplicación sobre su propia
  serialización, nunca sobre el texto que devuelve `jsonb`, que reordena las claves.
- **«Unir con el párrafo anterior»**, la marca **«Título»** y los demás datos de cada párrafo viajan dentro de ese
  JSON: no hay tabla de párrafos.
- **Unidades:** cada plantilla, documento y propuesta pertenece a una unidad (Exoneraciones, Dispensas, Registro
  Exonerados, Notas de Crédito y Devoluciones).
- **`Campos.AlertarCambios`:** marca los campos cuyo cambio se le avisa al aprobador (por ejemplo, Operador).
- **`Version`** es el control de concurrencia optimista que ya usa la aplicación.
- **No se migra** la colección `parrafos`, anterior a la Fase 3: su contenido ya está en las versiones.

## Reglas que protege el propio motor

| Regla | Cómo |
|---|---|
| Número PLAN único y bien formado | `UNIQUE (Anio, Secuencia)`, `UNIQUE (Numero)` y un `CHECK` que lo arma desde el año y la secuencia |
| Una sola versión vigente por plantilla (regla 14) | Índice único filtrado `WHERE Vigencia = 'Vigente'` |
| Identidad funcional única | `UNIQUE (Unidad, Rubro, SubRubro, TipoDocumento, ClaseDocumento, Beneficio)`: cada unidad tiene sus propias plantillas. Las tres últimas son opcionales: el valor vacío de los JSON se guarda como `NULL` (en PostgreSQL, índice único con `COALESCE`) |
| El sub rubro pertenece a su rubro | Clave foránea compuesta `(RubroId, SubRubroId)` |
| D4: una plantilla en un solo documento abierto | `BloqueosPlantilla`, con la identidad como clave primaria |
| Separación de funciones | `CHECK`: quien aprueba no es quien preparó ni quien envió |
| Lo publicado no cambia (regla 15) | Trigger: de una versión solo cambian la vigencia (de VIGENTE a NO VIGENTE), el fin de vigencia y `Version`; no se elimina |
| Auditoría de solo inserción | Triggers y permisos: `Historial`, `Movimientos`, `Presentaciones`, `Devoluciones` y `Observaciones` no se modifican ni se eliminan |
| Un aprobado no vuelve atrás | Trigger en `PlantillasDocCab` |

Las validaciones del dominio se mantienen: dan los mensajes en español, y el motor queda como última barrera.

## Verificación realizada (18/10/2026)

- **PostgreSQL 16.14** (servidor embebido):
  - el script se ejecutó completo;
  - se cargaron los datos en uso del prototipo: 6 usuarios, 23 rubros, 167 sub rubros, 10 plantillas, 11 versiones,
    4 consolidados, 7 propuestas, presentaciones, devoluciones, bitácora, historial, solicitudes y bienes;
  - 19 comprobaciones: cada regla de la tabla anterior rechazó el intento de romperla, y pasar una versión de VIGENTE
    a NO VIGENTE funcionó.
  - Repetida el 29/10/2026 con las unidades y la marca del campo vigilado, también correcta.
- **SQL Server 2019:** no había un servidor disponible. El script se validó con el analizador oficial de Microsoft
  (`Microsoft.SqlServer.TransactSql.ScriptDom`, modo SQL Server 2019): 0 errores, 26 tablas y todas las claves
  foráneas e índices apuntan a tablas y columnas existentes. **Falta ejecutarlo en un servidor real.**

**Datos semilla.** Los datos semilla (`DatosSemilla/`) no se importan tal cual: la API completa la versión de origen
y las huellas al iniciar. Importe desde `Data/`, después de iniciar la API al menos una vez.
