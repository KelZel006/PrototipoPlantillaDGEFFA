# Documento Consolidado — Fase 3: registro y edición

Fecha: 16/10/2026. Diseño de referencia: [CONSOLIDADO-FASE1.md](CONSOLIDADO-FASE1.md). Modelo: [CONSOLIDADO-FASE2.md](CONSOLIDADO-FASE2.md).

**Criterio de aceptación:** un analista arma un PLAN con varias plantillas sin tocar ninguna vigente. ✅

## 1. Cambio principal: ya no se edita directamente (D11)

- Las plantillas y párrafos anteriores (`/api/plantillas`, `/api/parrafos`) son **de solo lectura**. Cualquier
  escritura responde 409 con el mensaje «Cree o modifique la plantilla dentro de un Documento Consolidado».
- La **versión vigente** es la fuente de verdad. Se edita **la propuesta** dentro de un PLAN; la vigente no cambia
  hasta la aprobación (Fase 6).
- Se retiró la copia automática inmediata de la Certificación (`SincronizadorCopias`) y las operaciones directas de
  párrafos (`ServicioParrafos`). Su lógica pasó a la propuesta derivada (sección 3).
- **Transición:** al iniciar, cada versión «Carga inicial» que ningún consolidado tomó se actualiza con lo que se
  editó antes de cerrar la edición directa. Desde esta fase esos datos ya no cambian, así que no vuelve a ocurrir.

## 2. Qué puede hacer el analista

| Acción | Dónde | Reglas (validadas en el servidor) |
|---|---|---|
| Crear un consolidado | Pantalla A, «+ Nuevo consolidado» | Solo analistas; número PLAN automático |
| CREAR plantilla | Pantalla B → diálogo C | Rubro, Sub Rubro, Tipo y Documento válidos; no existe esa plantilla; nadie más la está creando (D4) |
| MODIFICAR plantilla | Pantalla B → diálogo C | Plantilla vigente que no esté en otro consolidado abierto (D4); se muestra en cuál está |
| Editar la propuesta | Editor (el mismo de siempre) | Solo el analista dueño y solo EN REGISTRO; cada párrafo tiene versión: una edición desde otra pestaña desactualizada se rechaza |
| Vista previa en PDF | Pantalla B y editor | La propuesta, con o sin solicitud de prueba |
| Retirar una propuesta | Pantalla B | Motivo obligatorio (5 a 500 caracteres); queda en la bitácora; libera la plantilla |

- Una plantilla aparece **una sola vez** por consolidado: todos sus ajustes van en la misma propuesta.
- Cada cambio queda en el historial con el antes y el después **del párrafo** (no de la propuesta completa).

## 3. La Certificación va con la Resolución (D3)

- Al agregar una Resolución (CREAR o MODIFICAR), el servidor agrega su Certificación de la misma combinación:
  - si la Certificación existe, como MODIFICAR de su versión vigente;
  - si no existe, como CREAR, con una copia de la Resolución («resolución» → «certificación»).
- Si la Certificación ya estaba en el consolidado, se liga a la Resolución.
- Cada cambio guardado en la Resolución pasa a la Certificación con la regla de antes: solo lo que cambió; lo editado
  en la Certificación se conserva hasta que la Resolución cambie lo mismo; los párrafos propios no se tocan; los
  nuevos se ubican después de su anterior. También el diseño, dato por dato.
- No se puede retirar la Certificación sola: al retirar la Resolución se retiran las dos.
- Si la Certificación está en otro consolidado abierto, no se puede agregar la Resolución (se avisa dónde está).

## 4. Ajustes y «Tipos de documento incluidos» (D8)

- `ComparadorContenido` compara la propuesta con la versión original (en CREAR, con un documento vacío): bloques por
  código y párrafos por identificador, nunca por posición.
- Ajustes: «NUEVO CONSIDERANDO (posición 2)», «CONSIDERANDO 3 modificado», «RESUELVE 2 eliminado», «POR TANTO 1
  movido a la posición 2», «CONSIDERANDO 2: se desactivó, cambió la condición», «Pie de página modificado»…
- El resumen muestra **cada tipo documental una vez** con su operación, plantillas y ajustes. «Ver ajustes» despliega
  los ajustes de cada plantilla.
- La diferencia de texto palabra por palabra y la comparación lado a lado son de la Fase 4.

## 5. Pantallas

| Pantalla | Ruta | Contenido |
|---|---|---|
| Barra superior | todas | Selector «Usuario de prueba» (se guarda en el navegador y viaja en `X-Usuario-Prueba`) |
| A. Documentos consolidados | `/consolidados` | Tabla con número, creación, analista, tipos, plantillas, estado, última actualización; filtros por número, año, estado, analista, tipo y fecha; «+ Nuevo consolidado» para analistas |
| B. Registro del consolidado | `/consolidados/:id` | Estado (EN REGISTRO, VERIFICADO «pendiente de revisión jurídica», APROBADO), última devolución con sus observaciones, tipos incluidos con ajustes, CREAR / MODIFICAR PLANTILLA, propuestas (Editar, Vista previa, Retirar), retiradas, historial |
| C. Selección de plantilla | diálogo | CREAR con las cuatro listas y nombre opcional; MODIFICAR con buscador y versión vigente; avisa si se agrega también la Certificación |
| Editor de la propuesta | `/consolidados/:id/propuestas/:propuestaId` | El editor de siempre sobre la propuesta; solo lectura si no es editable |

- **GUARDAR:** los cambios se guardan al guardar cada párrafo o el diseño; la pantalla B lo indica.
- **ENVIAR A VERIFICACIÓN:** aparece deshabilitado; se implementa en la Fase 5.
- Los estados del consolidado son píldoras con un punto; la vigencia (Fase 6) tendrá otra forma (regla 19).

## 6. API nueva

| Método y ruta | Uso |
|---|---|
| `POST /api/consolidados/{id}/propuestas/crear` | CREAR (devuelve también la Certificación agregada) |
| `POST /api/consolidados/{id}/propuestas/modificar` | MODIFICAR |
| `GET /api/propuestas/{id}`, `/edicion`, `/ajustes`, `/pdf` | Consulta, datos para el editor, ajustes y vista previa |
| `GET/POST /api/propuestas/{id}/parrafos`, `PUT/DELETE …/parrafos/{p}`, `POST …/{p}/duplicar`, `POST …/reordenar` | Párrafos de la propuesta |
| `PUT /api/propuestas/{id}/diseno`, `POST/DELETE /api/propuestas/{id}/logo` | Diseño y logo |
| `POST /api/propuestas/{id}/retirar` | Retiro con motivo |
| `GET /api/plantillas-vigentes` | Catálogo para MODIFICAR (indica si están en un consolidado abierto) |
| `GET /api/versiones-plantilla/{id}/pdf` | PDF de una versión publicada |

## 7. Datos en uso

- Las propuestas de demostración (PLAN-20260002 y PLAN-20260003) se regeneraron con el contenido de los datos en uso,
  para que muestren solo sus ajustes previstos (antes se habían armado con los identificadores de los datos semilla).
  Respaldo previo: fuera del repositorio.
- Un archivo de logo solo se borra si ninguna plantilla, versión, propuesta ni presentación lo usa.

## 8. Pendiente

- **.NET 10 (D9):** hecho al iniciar la Fase 4 (ver [CONSOLIDADO-FASE4.md](CONSOLIDADO-FASE4.md)).

## 9. Pruebas

**195 del backend** y **96 del frontend**, todas correctas.

- Backend: las pruebas del editor (contenido permitido, condiciones, tablas, orden, duplicado, logo, diseño,
  interlineado y PDF) ahora se ejecutan sobre una propuesta. Nuevas: la Certificación derivada (crear, actualizar,
  duplicar, reordenar y eliminar en la Resolución; lo editado en la Certificación; diseño; ligar una Certificación ya
  incluida; CREAR de Resolución con su Certificación), D4 en el mismo y en otro consolidado, retiro, resumen y
  ajustes, permisos de edición (dueño, otro analista, Oficial Jurídico, administrador, aprobado), versión de párrafo
  desactualizada, edición directa cerrada, carga inicial que se actualiza solo mientras nadie la toma, demostraciones
  con sus ajustes exactos.
- Frontend: encabezado del usuario de prueba en cada petición, pantallas A, B y C, panel de la propuesta (editable,
  solo lectura, Certificación derivada, vista previa en PDF).
