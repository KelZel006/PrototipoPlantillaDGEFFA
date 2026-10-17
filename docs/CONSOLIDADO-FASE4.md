# Documento Consolidado — Fase 4: bloques configurables y comparación

Fecha: 16/10/2026. Diseño de referencia: [CONSOLIDADO-FASE1.md](CONSOLIDADO-FASE1.md).

**Criterio de aceptación:** pruebas de comparación de texto agregado, eliminado y sustituido, párrafos movidos, bloques
nuevos y eliminados, cambios de formato y de diseño. ✅

## 0. .NET 10 (D9)

- Se instaló el SDK de .NET 10.0.402 y los cuatro proyectos pasaron a `net10.0`, con los paquetes de Microsoft en la
  versión 10.0.13.
- **Corrección de seguridad:** PDFsharp traía `System.Security.Cryptography.Pkcs` 8.0.1, que tiene vulnerabilidades
  conocidas (GHSA-2fmv-rv2c-59x7, de gravedad moderada, y GHSA-846f-f764-76mx, de gravedad alta). Se fijó la versión
  10.0.13 y la compilación quedó sin advertencias.
- El instalador sugiere reiniciar el equipo. El SDK ya funciona sin reiniciar.

## 1. Bloques configurables por tipo documental (D2)

- **Catálogo** (`bloques.json`, `/api/bloques`): cada bloque tiene código, nombre, título opcional en el documento,
  numeración (ninguna, «CONSIDERANDO(n):» u ordinal «PRIMERO:»), si es de firmas y el contexto de los campos que admite.
  Incluye los cinco de siempre y además IDENTIFICACION, ANTECEDENTES, VISTOS, FUNDAMENTACION_LEGAL, CERTIFICACION y
  SELLOS.
- **Por tipo documental** (`tiposDocumento.bloques`): qué bloques usa, en qué orden y cuáles son obligatorios. Hoy los
  cuatro tipos usan los cinco bloques de siempre, con Cabecera y Firmas obligatorios. El administrador los cambia con
  `PUT /api/tipos-documento/{id}`. `GET /api/tipos-documento/{id}/bloques` devuelve la configuración resuelta.
- **Qué se arma con la configuración:**
  - las pestañas del editor (con `*` en los obligatorios);
  - el contexto de los campos de cada bloque (por ejemplo, Vistos admite los campos del considerando);
  - el contenido vacío de una plantilla nueva;
  - el orden, los títulos y la numeración del PDF;
  - los nombres en la comparación.
- **Obligatorios:** el editor avisa cuando un bloque obligatorio no tiene párrafos activos. El envío a verificación lo
  exigirá (Fase 5).
- **Contenido anterior:** si un tipo deja de usar un bloque que tiene texto, ese bloque se sigue mostrando al final,
  para no ocultar contenido.
- **Permisos:** los catálogos (rubros, sub rubros, tipos de documento, documento, campos y bloques) solo los modifica
  el **administrador**. Consultarlos sigue abierto (sección 11 de la especificación).
- **Validaciones:** el código del bloque va en mayúsculas; el contexto debe ser válido; un tipo no repite bloques ni
  usa bloques inexistentes; no se elimina un bloque que use algún tipo.
- La API de párrafos de la propuesta identifica el bloque por su código (`bloque: "CONSIDERANDO"`, `?bloque=`) en lugar
  de la sección fija.

## 2. Comparación (vista de diferencias)

`GET /api/propuestas/{id}/comparacion` (`ComparadorContenido.Comparar`):

| Elemento | Cómo se compara |
|---|---|
| Bloques | Por código. Estados: Sin cambios, Modificado, NUEVO BLOQUE, BLOQUE ELIMINADO |
| Párrafos | Por identificador estable. Estados: sin cambios, agregado, eliminado, modificado; además, movido (posición original → propuesta). El eliminado aparece donde estaba en el original |
| Texto | Palabra por palabra (subsecuencia común más larga; la puntuación se compara por separado). Marca lo **agregado**, lo **eliminado** y lo **sustituido** (en el original, lo que sale; en la propuesta, lo que entra) |
| Tablas | Se compara el texto de sus celdas |
| Formato | Si solo cambió el formato (negrita, alineación…), se indica «Cambio de formato (el texto es igual)» |
| Configuración | Activo, condición y «mostrar solo si cumple» |
| Diseño | Logo, alineación, pie de página y espacio entre firmas |

**Pantalla de diferencias** (`/consolidados/:id/propuestas/:propuestaId/diferencias`). **Ajuste del 17/10/2026:** solo
la consulta el Oficial Jurídico con el documento VERIFICADO; EN REGISTRO no se muestra (ver la Fase 5).
- Original a la izquierda y propuesta a la derecha, bloque contra bloque y párrafo contra párrafo.
- Los bloques sin cambios aparecen plegados.
- Selector «Ambos / Original / Propuesta» para pantallas angostas.
- Leyenda de colores.
- Se abre desde la tarjeta de cada propuesta («Diferencias») y desde el editor («Ver diferencias con la vigente»).
- Es el mismo componente que usará la revisión jurídica (pantalla D, Fase 5).

## 3. Datos en uso

- `Data/tiposDocumento.json` recibió la configuración de bloques; `bloques.json` se copió de la semilla al iniciar.
  Respaldo previo fuera del repositorio.

## 4. Pruebas

**208 del backend** y **98 del frontend**, todas correctas. Nuevas:

- **Diferencia de texto:** agregado, eliminado, sustituido, sin cambios; cada lado conserva su texto completo.
- **Comparador:** emparejamiento por identificador y ubicación de los eliminados; movidos, cambios de formato y de
  configuración; tablas; bloques nuevos y eliminados; diseño.
- **Bloques configurables:** permisos de administrador; validaciones del catálogo y del tipo; pestañas, contexto de
  campos y obligatorios del editor; título y orden en el documento; plantilla nueva con los bloques de su tipo;
  comparación por la API.
- **Frontend:** pestañas según la configuración (incluido un bloque nuevo, Vistos) y vista de comparación (plegado,
  sustituciones, eliminados, diseño y columnas).
