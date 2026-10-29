# Base de datos del Sistema de Gestión de Plantillas

Scripts para crear las tablas del sistema en **SQL Server 2019** y en **PostgreSQL 13 o superior**. El modelo es el
mismo en los dos y sigue la estrategia de [docs/CONSOLIDADO-FASE8.md](../docs/CONSOLIDADO-FASE8.md). La elección
del motor (decisión D12) sigue pendiente.

| Motor | Script | Convención de nombres |
|---|---|---|
| SQL Server 2019 | [sqlserver/01-esquema.sql](sqlserver/01-esquema.sql) | `plantillas.VersionesPlantilla.NumeroVersion` |
| PostgreSQL | [postgresql/01-esquema.sql](postgresql/01-esquema.sql) | `plantillas.versiones_plantilla.numero_version` |

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

## Tablas

| Grupo | Colección JSON actual | SQL Server | PostgreSQL |
|---|---|---|---|
| Usuarios | `usuarios` | `Usuarios`, `UsuarioRoles` | `usuarios`, `usuario_roles` |
| Catálogos | `unidades` | `Unidades` | `unidades` |
| | `rubros`, `subRubros` | `Rubros`, `SubRubros` | `rubros`, `sub_rubros` |
| | `clasesDocumento` | `ClasesDocumento` | `clases_documento` |
| | `bloques` (con los bloques que reemplaza cada uno) | `BloquesDocumentales`, `BloqueReemplazos` | `bloques_documentales`, `bloque_reemplazos` |
| | `tiposDocumento` (con sus bloques) | `TiposDocumento`, `TipoDocumentoBloques` | `tipos_documento`, `tipo_documento_bloques` |
| | `campos` (con sus contextos) | `Campos`, `CampoContextos` | `campos`, `campo_contextos` |
| Archivos | `archivos` y la carpeta `archivos/` | `Archivos` | `archivos` |
| Plantillas | `plantillas` (identidad) | `Plantillas` | `plantillas` |
| | `versionesPlantilla` | `VersionesPlantilla` | `versiones_plantilla` |
| Documento Consolidado | número PLAN | `SecuenciasPlan` | `secuencias_plan` |
| | `consolidados` | `Consolidados` | `consolidados` |
| | `propuestas` | `Propuestas` | `propuestas` |
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
| Identidad funcional única | `UNIQUE (Unidad, Rubro, SubRubro, TipoDocumento, ClaseDocumento)`: cada unidad tiene sus propias plantillas |
| El sub rubro pertenece a su rubro | Clave foránea compuesta `(RubroId, SubRubroId)` |
| D4: una plantilla en un solo documento abierto | `BloqueosPlantilla`, con la identidad como clave primaria |
| Separación de funciones | `CHECK`: quien aprueba no es quien preparó ni quien envió |
| Lo publicado no cambia (regla 15) | Trigger: de una versión solo cambian la vigencia (de VIGENTE a NO VIGENTE), el fin de vigencia y `Version`; no se elimina |
| Auditoría de solo inserción | Triggers y permisos: `Historial`, `Movimientos`, `Presentaciones`, `Devoluciones` y `Observaciones` no se modifican ni se eliminan |
| Un aprobado no vuelve atrás | Trigger en `Consolidados` |

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
