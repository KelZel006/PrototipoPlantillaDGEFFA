# Documento Consolidado — Fase 7: auditoría y pruebas

Fecha: 17/10/2026. Diseño de referencia: [CONSOLIDADO-FASE1.md](CONSOLIDADO-FASE1.md).

**Criterio de aceptación:** las reglas 1 a 20 de la especificación están cubiertas por pruebas automáticas. ✅

## 1. Cambios de auditoría

| Cambio | Detalle |
|---|---|
| **Trazabilidad por versión** | `GET /api/versiones-plantilla/{id}/trazabilidad`. Responde qué texto estaba vigente antes (versión anterior completa), qué se publicó, los ajustes entre ambos, el documento consolidado que lo originó, quién lo preparó, lo envió y lo aprobó, y todas sus presentaciones y devoluciones. La consultan el administrador y el Oficial Jurídico |
| **Historial técnico solo para el administrador** | `GET /api/historial` guarda el antes y el después de cada registro, incluido el contenido de todos los documentos. Antes estaba abierto a cualquiera, lo que permitía a un analista leer documentos de otros y rompía D10. Ahora solo lo consulta el administrador |
| **Nada se borra ni se reescribe** | No existe ninguna operación para borrar o modificar versiones, documentos consolidados ni historial. Las pruebas lo verifican (regla 15) |

**Qué queda registrado, y dónde** (sección 10 de la especificación):

| Hecho | Bitácora del consolidado (`movimientos`) | Historial técnico (`historial`) |
|---|:-:|:-:|
| Creación del consolidado y número asignado | ✅ | ✅ |
| Plantilla agregada (CREAR o MODIFICAR), retirada y su motivo | ✅ | ✅ |
| Cada cambio de párrafo o de diseño (antes y después) | — | ✅ |
| Envío a verificación (presentación congelada con su huella) | ✅ | ✅ |
| Devolución, justificación y observaciones | ✅ | ✅ |
| Aprobación, publicación y cambio de vigencia | ✅ | ✅ |
| Error de publicación | ✅ | — |
| Carga inicial y su actualización de transición | — | ✅ |

## 2. Pruebas nuevas (`AuditoriaPruebas.cs`)

- **Trazabilidad completa:**
  - con dos presentaciones y una devolución, la versión publicada se rastrea hasta su documento y sus responsables;
  - el analista no la consulta.
- **Historial:** registra cada paso con el usuario y solo lo lee el administrador.
- **Nada se borra:** borrar o reescribir versiones, documentos o historial responde 405 o 404.
- **Regla 14:** con dos versiones vigentes en los datos, no se publica y se informa la inconsistencia.
- **Concurrencia.** Se ejecutaron 5 veces seguidas, todas correctas:
  - 6 ediciones simultáneas del mismo párrafo: gana 1 y las otras 5 se rechazan con 409;
  - 5 aprobaciones simultáneas del mismo documento: se publica una sola vez;
  - 4 documentos que toman la misma plantilla al mismo tiempo: solo 1 lo logra (D4);
  - 25 creaciones simultáneas obtienen números PLAN únicos (Fase 2).
- **Matriz de permisos:** 22 combinaciones de rol y operación, todas validadas en el servidor (regla 18).

**Total:** **254 pruebas del backend** y **103 del frontend**, todas correctas.

## 3. Cobertura de las reglas de negocio (sección 14 de la especificación)

| # | Regla | Pruebas que la verifican |
|---|---|---|
| 1 | Número único PLAN-AAAANNNN | `NumeracionPlanPruebas` (formato, año institucional, reinicio anual, 25 simultáneas, máximo 9999) |
| 2 | Tres estados: EN REGISTRO, VERIFICADO, APROBADO | `RevisionPruebas.Ciclo_completo…`, `PublicacionPruebas.Aprobar_publica_todo…` |
| 3 | Varias plantillas de distintos tipos | `PublicacionSemillaPruebas` (Resolución, Certificación y Constancia), `PublicacionPruebas` (MODIFICAR y CREAR) |
| 4 | Cada tipo documental una sola vez en el listado | `PermisosConsolidadoPruebas.El_listado_muestra_cada_tipo…`, `CopiaCertificacionPruebas.El_resumen_muestra_cada_tipo…` |
| 5 | Varios bloques en una misma propuesta | `CopiaCertificacionPruebas.El_resumen…` (Considerando, Por Tanto y diseño en una propuesta), `Una_plantilla_no_puede_estar_en_dos…` (todos los ajustes en la misma propuesta) |
| 6 | Las propuestas no modifican lo vigente | `CopiaCertificacionPruebas.Los_cambios…sin_tocar_las_vigentes`, `ListasYDisenoPruebas.El_diseno_de_la_propuesta_no_cambia…`, `ParrafosPruebas.La_plantilla_vigente_no_se_edita_directamente` |
| 7 | El Oficial Jurídico compara original y propuesta | `RevisionPruebas.Las_diferencias_solo_las_ve…`, `ComparacionPruebas` (palabras, bloques, tablas, formato, diseño) |
| 8 | Toda devolución exige justificación y queda registrada | `RevisionPruebas.Devolver_exige_justificacion…`, `Ciclo_completo…` |
| 9 | El analista corrige lo devuelto | `RevisionPruebas.Ciclo_completo…` (corrige y reenvía) |
| 10 | No se aprueba lo que no está VERIFICADO | `PublicacionPruebas.No_se_aprueba_lo_que_no_esta_verificado`, `Aprobar_publica_todo…` (no se aprueba dos veces) |
| 11 | La aprobación publica todas las plantillas | `PublicacionPruebas.Aprobar_publica_todo…`, `PublicacionSemillaPruebas` |
| 12 | MODIFICAR: anterior NO VIGENTE, nueva VIGENTE | `PublicacionPruebas.Aprobar_publica_todo…` |
| 13 | CREAR se publica VIGENTE sin alterar otras | `PublicacionPruebas.Aprobar_publica_todo…` (una sola versión NO VIGENTE en total) |
| 14 | Como máximo una vigente por identidad | `AuditoriaPruebas.Con_dos_versiones_vigentes…`, `CargaInicialPruebas.Cada_plantilla_queda_con_una_sola…` |
| 15 | No se eliminan versiones históricas | `AuditoriaPruebas.Versiones_documentos_e_historial_no_se_borran…`, `PublicacionPruebas` (la anterior se conserva con su contenido) |
| 16 | Publicación transaccional | `PublicacionPruebas.Un_fallo_al_guardar_no_deja_cambios_parciales`, `UnidadDeTrabajoPruebas` (reversión y recuperación del diario) |
| 17 | Lo aprobado no se modifica silenciosamente | `EdicionPropuestaPermisosPruebas` (editar un aprobado: 409), `PublicacionPruebas` (un cambio nuevo requiere otro PLAN y parte de la versión publicada) |
| 18 | Permisos en el servidor y en la pantalla | `AuditoriaPruebas.Cada_rol_solo_puede…` (22 casos), `PermisosConsolidadoPruebas`, pruebas del frontend (botones según rol y estado) |
| 19 | Estados del consolidado distintos de la vigencia | Frontend: `InsigniaEstado` (píldora) frente a `InsigniaVigencia` (etiqueta rectangular); `plantillas-vigentes.spec.ts` |
| 20 | Nada terminado sin pruebas | Esta tabla: 254 pruebas del backend y 103 del frontend |

## 4. Recuperación ante errores (resumen)

| Situación | Comportamiento | Prueba |
|---|---|---|
| La operación falla antes de guardar | No se escribe nada | `UnidadDeTrabajoPruebas.Si_la_operacion_falla…` |
| Falla la escritura de un archivo a mitad de la operación | Se restaura lo ya escrito; error 503 | `UnidadDeTrabajoPruebas.Si_falla_la_escritura…`, `PublicacionPruebas.Un_fallo_al_guardar…` |
| El proceso se interrumpe con el diario preparado | Al iniciar se completa la operación | `UnidadDeTrabajoPruebas.Un_diario_pendiente…` |
| Un archivo de datos dañado | No se sobrescribe; se copia a cuarentena y se informa | `ApiPruebas.Archivo_danado…` |
| Conflicto al aprobar | No se publica nada; queda «Error de publicación» en la bitácora | `PublicacionPruebas.Si_la_version_vigente_cambio…` |

## 5. Limitaciones conocidas del prototipo

- **El usuario de prueba no es autenticación.** Cualquiera puede elegir un usuario en la pantalla o en el encabezado
  `X-Usuario-Prueba`. Los permisos se validan bien en el servidor, pero la identidad no está garantizada. En producción
  se reemplaza por el proveedor de identidad institucional (Fase 8).
- **Los archivos JSON protegen un solo proceso de la API.** No sirven para varios servidores (Fase 8).
- **El historial técnico guarda el contenido completo de cada cambio,** así que crece con el uso. En una base de datos
  conviene particionarlo o archivarlo.
