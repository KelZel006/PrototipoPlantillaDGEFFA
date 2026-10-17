# Documento Consolidado — Fase 5: verificación y devolución

Fecha: 17/10/2026. Diseño de referencia: [CONSOLIDADO-FASE1.md](CONSOLIDADO-FASE1.md).

**Criterio de aceptación:** ciclo enviar → devolver → corregir → reenviar, con el historial completo. ✅

## 0. Ajustes pedidos antes de la fase

- **Diferencias solo para el Oficial Jurídico, con el documento VERIFICADO.** Mientras el documento está EN REGISTRO
  no se muestran: el analista no tiene el botón «Diferencias», y el editor tampoco tiene el enlace.
  - El servidor lo valida: `GET /api/propuestas/{id}/comparacion` responde 403 si quien consulta no es Oficial
    Jurídico, y 409 si el documento no está VERIFICADO.
  - El resumen de ajustes por tipo documental sigue visible en la pantalla B (decisión D8).
- **«Consolidado» en lugar de «Propuesta»** en los textos visibles:
  - en la comparación, las columnas «ORIGINAL / CONSOLIDADO» y el selector «Ambos / Original / Consolidado»;
  - en la pantalla B, «Plantillas del consolidado» y «Plantillas retiradas del consolidado»;
  - en el editor y en el diálogo de selección, «la plantilla en el consolidado».
  - Los nombres internos del código y de la API (`propuestas`) no cambian.

## 1. Enviar a verificación (analista)

`POST /api/consolidados/{id}/enviar`. Pantalla B, botón **ENVIAR A VERIFICACIÓN**, con confirmación.

- **Lo puede hacer:** solo el analista dueño, con el documento EN REGISTRO.
- **Se exige:**
  - al menos una plantilla;
  - los bloques obligatorios de cada plantilla con algún párrafo activo;
  - que la versión original de cada MODIFICAR siga siendo la vigente.

  Si algo falla, se informa qué plantilla y qué bloque.
- **Al enviar:**
  - el estado pasa a **VERIFICADO**;
  - aumenta el número de presentación y se registran el usuario y la fecha;
  - se crea la **presentación** con una copia congelada de lo enviado y su huella de integridad (SHA-256);
  - quedan el movimiento («Primera presentación: 3 plantillas (…)») y el historial.
- **Después:**
  - el documento **sale de la bandeja del analista** (D6) y aparece en la del Oficial Jurídico;
  - mientras se revisa, nadie lo edita.

## 2. Revisión jurídica (pantalla D)

Ruta `/consolidados/:id/revision`. Se llega con el botón **Revisar** desde la bandeja (pantalla A) o desde el documento.

- **A la izquierda:** las plantillas del consolidado, con su operación y la cantidad de ajustes. La seleccionada
  muestra sus ajustes («NUEVO CONSIDERANDO (posición 2)»…).
- **Al centro:** la comparación lado a lado ORIGINAL / CONSOLIDADO de la Fase 4.
- **Abajo:** el historial de observaciones y las acciones **DEVOLVER A REGISTRO** y **APROBAR**. APROBAR está
  deshabilitado hasta la Fase 6.
- **Restricciones:**
  - solo el Oficial Jurídico, con el documento VERIFICADO;
  - **separación de funciones:** quien preparó o envió el documento no puede revisarlo, aunque tenga ese rol;
  - el Oficial Jurídico no edita el contenido.

## 3. Devolución a registro (pantalla E)

`POST /api/consolidados/{id}/devolver`.

- **Justificación general obligatoria**, de 10 a 2000 caracteres.
- **Observaciones opcionales y repetibles:**
  - plantilla observada (debe estar en el documento);
  - bloque observado, opcional (si no se elige, es una observación general);
  - descripción del problema (obligatoria);
  - recomendación de corrección (opcional).
- Antes de devolver se pide **confirmación**.
- **Al devolver:**
  - el estado vuelve a **EN REGISTRO**, en la bandeja de su analista;
  - se crea la devolución con el número de presentación revisado;
  - quedan el movimiento («Primera devolución…») y el historial.
- **Nada se sobrescribe:** cada presentación y cada devolución quedan guardadas.
- **En la pantalla B**, el analista ve la última devolución destacada y la sección **Presentaciones y devoluciones**:
  «Primera presentación», «Primera devolución», «Segunda presentación»…, con las justificaciones y observaciones de
  cada una.

## 4. Pruebas

**213 del backend** y **101 del frontend**, todas correctas. Nuevas:

- **Ciclo completo:** dos presentaciones y dos devoluciones, con:
  - la bandeja de cada usuario;
  - el bloqueo de la edición durante la revisión;
  - lo congelado en cada presentación con su huella;
  - la secuencia de la bitácora.
- **Enviar:** sin plantillas, otro analista, Oficial Jurídico, bloque obligatorio vacío.
- **Devolver:** sin justificación, observaciones inválidas, analista o administrador, documento no verificado.
- **Separación de funciones:** quien preparó el documento no lo revisa.
- **Diferencias:** solo las ve el Oficial Jurídico con el documento VERIFICADO.
- **Frontend:**
  - envío con confirmación y salida de la bandeja;
  - el analista no ve «Diferencias»;
  - devolución con justificación y observación;
  - la revisión no se ofrece fuera de VERIFICADO.
