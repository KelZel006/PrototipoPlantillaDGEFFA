# Documento Consolidado — Fase 2: modelo JSON, transacciones, usuarios y permisos

Fecha: 16/10/2026. Diseño de referencia: [CONSOLIDADO-FASE1.md](CONSOLIDADO-FASE1.md).

Esta fase prepara los datos y la infraestructura del Documento Consolidado. **Todavía no hay pantallas nuevas.** El
editor actual sigue funcionando igual hasta la Fase 3.

## 1. Qué se hizo

| Tema | Detalle |
|---|---|
| **D1** | «Constancia de Registro» pasó a llamarse **Constancia de Obligados Tributarios** (TDO-003), en los datos semilla y en los datos en uso |
| Modelos | `VersionPlantilla` (con vigencia), `ContenidoPlantilla` (diseño, bloques y párrafos), `DocumentoConsolidado`, `Propuesta`, `Presentacion`, `Devolucion` con `Observacion`, `Movimiento` (bitácora) y `Usuario` con roles. Archivos: `Dominio/Modelos/Versiones.cs`, `Consolidado.cs`, `Usuario.cs` |
| Colecciones | 7 nuevas: `usuarios`, `versionesPlantilla`, `consolidados`, `propuestas`, `presentaciones`, `devoluciones`, `movimientos`. Ninguna se puede escribir desde la API genérica: solo las escribe el dominio |
| Transacciones | `IUnidadDeTrabajo` (dominio) y `UnidadDeTrabajoJson` (infraestructura): todo o nada sobre varias colecciones (sección 2) |
| Usuarios y permisos | Encabezado `X-Usuario-Prueba`. `ServicioAutorizacion` valida en el servidor: 401 si el usuario no existe o está inactivo, 403 si le falta el rol |
| Numeración | PLAN-AAAANNNN dentro de una transacción: correlativo por año, año según la zona horaria institucional (`Consolidado:ZonaHoraria`, por defecto `America/Tegucigalpa`), máximo 9999 por año |
| Bandejas (D6, D10) | El analista ve solo los suyos, EN REGISTRO o APROBADOS: al enviarlo a verificación, sale de su bandeja. El Oficial Jurídico ve los VERIFICADOS y los APROBADOS. El administrador ve todos. Un consolidado que no le corresponde al usuario responde 404 |
| Carga inicial (D5) | Al iniciar la API, cada plantilla sin versión vigente recibe su **versión 1 VIGENTE «Carga inicial»** con su contenido actual. Se registra en el historial y se informan las inconsistencias de vigencia |
| API (solo lo necesario para probar) | `GET/POST /api/consolidados`, `GET /api/consolidados/{id}`, `GET /api/versiones-plantilla?plantillaId=&vigencia=`, `GET /api/usuarios` |

## 2. Transacciones con archivos JSON

`UnidadDeTrabajoJson` ejecuta una transacción a la vez y bloquea cada colección que usa hasta terminar:

1. Los cambios se hacen en memoria. **Si la operación falla, no se escribe nada.**
2. Se escribe un **diario** (`Data/transacciones/*.json`) con el contenido final de las colecciones modificadas.
3. Se reemplaza cada archivo de forma atómica.
   - **Si una escritura falla** (disco lleno, archivo bloqueado), las colecciones ya escritas **se restauran** y se
     borra el diario: no queda nada a medias.
   - **Si el proceso se interrumpe de golpe**, al iniciar se **completa** la transacción desde el diario. Así la
     operación queda aplicada completa, nunca a medias.
4. Se borra el diario.

Esto protege dentro de **un solo proceso** de la API. **No es una solución de producción**: en la Fase 8 se reemplaza
por una transacción de la base de datos, sin cambiar el dominio.

## 3. Usuarios de prueba

| Usuario | Roles | Uso |
|---|---|---|
| `usuario.prueba` | Analista, Administrador | Se usa cuando no se envía el encabezado. Es el que usa hoy el editor |
| `a.perez` | Analista | Dueña de PLAN-20260001 (aprobado) y PLAN-20260002 (verificado) |
| `m.lopez` | Analista | Dueño de PLAN-20260003 (en registro) |
| `j.ramos` | Oficial Jurídico | Revisa PLAN-20260002 |
| `admin` | Administrador | Consulta todo |
| `inactivo` | Analista (inactivo) | Para probar el rechazo (401) |

Ejemplo: `curl -H "X-Usuario-Prueba: j.ramos" http://localhost:5080/api/consolidados`.

**Advertencia:** el encabezado **no es autenticación**. Solo sirve para simular el flujo en el prototipo. En producción,
`IUsuarioActual` tomará el usuario del proveedor de identidad institucional.

## 4. Datos de prueba

| Consolidado | Estado | Analista | Contenido |
|---|---|---|---|
| PLAN-20260001 | APROBADO | a.perez | Modificó el Dictamen PLA-002: su versión 1 quedó **NO VIGENTE** y la 2 quedó **VIGENTE** |
| PLAN-20260002 | VERIFICADO (2.ª presentación) | a.perez | Modificar Resolución PLA-001 (un **NUEVO CONSIDERANDO** y un Por Tanto modificado), su Certificación PLA-004 derivada (D3) y **Crear** una Constancia de Obligados Tributarios. Tiene una devolución con observaciones |
| PLAN-20260003 | EN REGISTRO | m.lopez | Modificar Resolución PLA-003 y **Crear** su Certificación derivada |

- Los datos semilla no pueden traer escritos algunos valores, que se completan al iniciar:
  - las **huellas** (SHA-256 calculadas por el servidor);
  - la **versión original** de las propuestas MODIFICAR, porque las versiones de carga inicial se crean al iniciar.
- **En los datos en uso** (`Data/`), la API creó al iniciar las 9 versiones de carga inicial que faltaban, sin
  inconsistencias. La versión 2 vigente del Dictamen PLA-002 viene de los datos de demostración, no de lo editado en
  `Data/`.

## 5. Ajuste respecto de la Fase 1

- **Contenido de la propuesta.** En la Fase 1 se propuso guardar los párrafos en trabajo de una propuesta en la
  colección `parrafos`. En esta fase, el contenido propuesto va **dentro de la propia propuesta**
  (`contenidoPropuesto`), con la misma forma que una versión.
  - Ventajas: cada guardado de la propuesta es una sola escritura, y comparar, congelar al enviar y publicar no
    necesita armar el contenido desde otra colección.
  - En la Fase 3, el editor trabajará sobre ese contenido.

## 6. Transición hasta la Fase 3

- El editor actual **sigue editando las plantillas directamente**. La versión vigente es una fotografía tomada al
  iniciar la API por primera vez con esta fase, así que lo que se edite ahora en el editor no cambia la vigente.
- En la Fase 3, la vigente pasa a ser la fuente de verdad y la edición directa desaparece (D11). Antes de ese cambio,
  la carga inicial se actualizará con el contenido del momento, siempre que ningún consolidado haya modificado esa
  plantilla.

## 7. Pendiente

- **.NET 10 (D9):** el SDK de .NET 10 todavía no está instalado. Hoy solo está el 9.0.300. Al instalarlo, se cambia
  `TargetFramework` y se ejecutan las pruebas.

## 8. Pruebas

**185 del backend** (156 anteriores + 29 nuevas) y 91 del frontend, todas correctas. Nuevas (`ConsolidadoModeloPruebas.cs`):

- **Transacciones:** guarda varias colecciones juntas; si la operación falla no guarda nada; una versión desactualizada
  cancela todo; si falla la escritura de una colección se revierte lo ya escrito; un diario pendiente se completa al
  iniciar.
- **Numeración:** formato y rango; secuencia del año; el 31/12 a las 23:00 de Tegucigalpa sigue siendo del año
  anterior; reinicio en el año nuevo; 25 creaciones simultáneas obtienen números únicos y correlativos; rechazo al
  superar 9999.
- **Permisos:** solo un analista registrado y activo crea; 401 y 403; cada bandeja ve solo lo suyo; un consolidado
  ajeno responde 404; cada tipo documental aparece una sola vez en el listado; el detalle conserva presentaciones,
  devoluciones y bitácora.
- **Carga inicial:** una sola vigente por plantilla; huellas correctas; historia de PLA-002 (NO VIGENTE y VIGENTE);
  propuestas y presentaciones completas; migración idempotente; datos semilla coherentes con las reglas (incluida D4).
