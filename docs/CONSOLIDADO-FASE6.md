# Documento Consolidado — Fase 6: aprobación, publicación y vigencia

Fecha: 17/10/2026. Diseño de referencia: [CONSOLIDADO-FASE1.md](CONSOLIDADO-FASE1.md), sección 7.

**Criterio de aceptación:** aprobación de un PLAN con varias plantillas; un conflicto se detecta; un error no deja
cambios parciales. ✅

## 1. Aprobar y publicar

`POST /api/consolidados/{id}/aprobar`. Pantalla D, botón **APROBAR**, con confirmación.

**Validaciones.** Todas se hacen dentro de la misma transacción:

| Validación | Si falla |
|---|---|
| El documento está VERIFICADO | 409 |
| Aprueba un Oficial Jurídico que no preparó ni envió el documento | 403 |
| Lo actual coincide con lo enviado (huella de integridad de la última presentación) | 409, «el contenido cambió desde que se envió» |
| MODIFICAR: la versión original sigue siendo la única vigente y no cambió (identificador y huella) | 409, «la versión vigente … cambió» |
| CREAR: no existe otra plantilla con la misma identidad funcional | 409 |
| Después de publicar, como máximo una versión vigente por plantilla y una plantilla por identidad (regla 14) | 409 |

**Publicación:**
- **MODIFICAR:**
  - la versión original pasa a **NO VIGENTE**, con su fecha de fin de vigencia; no se borra;
  - el contenido propuesto se publica como **versión nueva VIGENTE**, con el número siguiente, ligada a la anterior y
    al consolidado, con quien la publicó y la fecha.
- **CREAR:**
  - se crea la plantilla (su identidad) y su **versión 1 VIGENTE**;
  - ninguna otra versión cambia de vigencia.
- **El documento** pasa a **APROBADO**, con el aprobador y la fecha. Ya no se modifica: un cambio nuevo requiere otro PLAN.
- **Bitácora:**
  - «Aprobación final de la … presentación»;
  - una «Publicación» por cada plantilla;
  - un «Cambio de vigencia» por cada versión que pasa a NO VIGENTE.
- **Historial:** queda cada cambio con su antes y su después.

## 2. Todo o nada

- **Una sola transacción.** Todo se publica en una sola transacción de varias colecciones (`UnidadDeTrabajoJson`, Fase 2):
  - si una validación falla, no se escribe nada;
  - si falla la escritura de un archivo, lo ya escrito se restaura;
  - si el proceso se cae a mitad, al iniciar se completa la operación desde el diario.
- **Errores documentados.** Un intento fallido queda en la bitácora como **«Error de publicación»**, con el motivo, y el
  documento sigue VERIFICADO para intentarlo de nuevo o devolverlo.
- **Aviso:** como en la Fase 2, esto protege un solo proceso de la API. Con una base de datos será una transacción del
  motor (Fase 8).

## 3. Consolidado de plantillas (pantalla F)

Ruta `/vigentes`, enlace «Plantillas vigentes» en la barra superior. API: `GET /api/consolidado-plantillas`.

- Cada plantilla muestra:
  - tipo, nombre, código, rubro y sub rubro;
  - **versión vigente** y desde cuándo rige;
  - el **PLAN que la publicó**, o «Carga inicial».
- Filtros por texto y por tipo documental.
- **«Versiones (n)»** despliega todas las versiones, de la más reciente a la más antigua, cada una con:
  - su vigencia (**VIGENTE / NO VIGENTE**);
  - desde y hasta cuándo rigió;
  - el PLAN y el usuario que la publicaron;
  - su **PDF**.
- La vigencia es una **etiqueta rectangular con borde**, distinta de la píldora del estado del consolidado (EN
  REGISTRO, VERIFICADO, APROBADO), para no confundirlos (regla 19).
- Los documentos aprobados muestran el aviso «Aprobado el … por …» y un enlace a las plantillas vigentes.

## 4. Datos en uso

- No se aprobó ningún documento en `Data/`: la aprobación se probó con copias de los datos.
- El documento de demostración PLAN-20260002 está listo para aprobarse. Una prueba automática lo aprueba sobre una copia
  de los datos semilla y publica sus tres plantillas.

## 5. Pruebas

**222 del backend** y **103 del frontend**, todas correctas. Nuevas:

- **Aprobación completa:**
  - la versión anterior pasa a NO VIGENTE y se conserva con su contenido;
  - la nueva queda VIGENTE, ligada a la anterior y al PLAN;
  - la plantilla nueva aparece recién al aprobar;
  - quedan la bitácora y el consolidado de plantillas;
  - un documento aprobado ya no se aprueba otra vez;
  - un cambio nuevo parte de la versión publicada.
- **Conflictos sin cambios y documentados:** la versión vigente cambió; el contenido cambió después del envío.
- **Fallo al guardar** a mitad de la transacción: no quedan cambios parciales, el error queda registrado y luego se
  aprueba normalmente.
- **Permisos:** solo el Oficial Jurídico aprueba; lo que no está VERIFICADO no se aprueba.
- **Demostración:** se aprueba PLAN-20260002 de los datos semilla.
- **Frontend:** aprobación con confirmación; pantalla F con versión vigente, filtro y versiones anteriores con su origen.
