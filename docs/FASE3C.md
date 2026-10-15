# Fase 3c: tablas con campos de lista, logo, pie de página e interlineado

**Pedido del usuario (14/10/2026):**

- En una tabla, el número de columnas lo define el usuario. A partir de la segunda fila, si hay un campo de tipo
  lista, se generan tantas filas como registros tenga la lista.
- Crear tres campos de lista: **Código Arancelario**, **Descripción Arancelaria** y **Monto Autorizado**.
- Agregar un **logo** en el encabezado y un **texto de pie de página**, los dos editables.
- Poder elegir el **interlineado** al generar el documento.

**Resultado: cumplido.**

Capturas: [diseño del documento](diseno/fase3c-diseno.png) y [tabla con campos de lista](diseno/fase3c-tabla-lista.png).
Hay un logo ficticio para probar en [diseno/logo-prueba.png](diseno/logo-prueba.png).

## Cómo funciona

### Campos de lista y filas que se repiten

- Un **campo de lista** tiene un valor por cada registro de la solicitud; por ahora, por cada bien de
  `detallesBienes`. En el editor se ven en **verde**, con un ícono de lista.
- **Solo se insertan dentro de una tabla.** Fuera de una tabla aparecen deshabilitados en el panel y en el menú
  «Insertar campo». El servidor también rechaza un campo de lista fuera de una tabla.
- Uso típico:
  - **fila 1:** los encabezados (Código, Descripción, Monto);
  - **fila 2:** los campos de lista.
- **Al generar el documento:**
  - **Con una solicitud**, cada fila que tenga un campo de lista se repite **una vez por cada registro**, en orden.
    Los campos generales de esa fila, como el número de solicitud, se repiten en cada fila.
  - **Sin registros**, la fila se reemplaza por «No hay registros para esta solicitud.» y se agrega una advertencia
    en el resumen.
  - **Sin solicitud**, la fila aparece una vez, con los campos como `{Etiqueta}`.
  - Las filas sin campos de lista se mantienen tal cual.

**Campos de lista disponibles:**

| Campo | Tipo | Origen |
|---|---|---|
| **Código Arancelario** | Texto | `detallesBienes.codigoArancelario` |
| **Descripción Arancelaria** (nuevo) | Texto | `detallesBienes.descripcionArancelaria` |
| **Monto Autorizado** (nuevo) | Moneda (L) | `detallesBienes.montoAutorizado` |
| Descripción del Bien, Unidad de Medida, Cantidad, Valor Estimado | — | Ya existían; ahora también se tratan como lista |

Los cuatro bienes de prueba tienen descripción arancelaria y monto autorizado ficticios. Los datos para probar las
filas son:

- `SOL-001`: 0 registros.
- `SOL-002`: 1 registro.
- `SOL-003`: 3 registros.

### Logo y pie de página (por plantilla)

- En el panel de la plantilla, sección desplegable **Diseño del documento: logo y pie de página**.
- **Logo:**
  - se carga, se reemplaza o se quita (quitarlo pide confirmación);
  - acepta PNG o JPEG de hasta 1 MB, con vista previa y alineación a la izquierda, al centro o a la derecha;
  - el servidor comprueba el **contenido real** del archivo (la firma PNG o JPEG), no su nombre: rechaza, por
    ejemplo, un SVG o un texto con extensión `.png`;
  - el archivo se guarda en `Data/archivos/` con un nombre generado por el servidor, y al reemplazarlo se borra el
    anterior.
- **Pie de página:** texto de hasta 500 caracteres y 8 líneas. Se guarda con «Guardar diseño» y aparece centrado en
  todas las páginas, sobre el número de página.
- La marca «DOCUMENTO DE PRUEBA · SIN VALIDEZ OFICIAL» sigue en todas las páginas, debajo del logo.

### Interlineado

- Junto a «Generar PDF», lista **Interlineado**: Sencillo, 1.15, 1.5 (predeterminado) o Doble.
- Se aplica al texto del documento. Las tablas, el pie y el resumen mantienen interlineado sencillo.
- El servidor solo acepta esos cuatro valores.

## Endpoints nuevos

| Método y ruta | Uso |
|---|---|
| `POST /api/plantillas/{id}/logo` | Carga o reemplaza el logo (`multipart/form-data`, campo `archivo`) |
| `DELETE /api/plantillas/{id}/logo` | Quita el logo |
| `GET /api/archivos/{id}` | Devuelve la imagen del logo |
| `GET /api/plantillas/{id}/pdf?solicitudId=&interlineado=` | Nuevo parámetro `interlineado`: `1`, `1.15`, `1.5` o `2` |

La plantilla tiene tres propiedades nuevas: `logoId`, `alineacionLogo` y `piePagina`. Se cambian con el `PUT` de
siempre, y el servidor valida que el logo exista.

## Problemas encontrados en la verificación y corregidos

1. **Al cargar el logo se cerraba el panel de diseño y se reiniciaban los borradores.** Los componentes dependían de
   la plantilla completa, no solo de su identificador. Por eso, cualquier versión nueva de la plantilla (como la que
   se crea al cargar un logo) reiniciaba el formulario del pie y, más grave, **borraba los párrafos que el usuario
   estaba editando** en Cabecera y Considerando. Ahora solo se reinician al cambiar de plantilla. Hay dos pruebas que
   cubren el caso.
2. **Caracteres escapados al editar por línea de comandos.** Los `\n` del código del pie de página se convirtieron en
   saltos de línea reales; se corrigió antes de compilar.

## Pruebas

| Conjunto | Resultado |
|---|---|
| Backend | **102 de 102 correctas** (15 nuevas) |
| Frontend | **79 de 79 correctas** (9 nuevas) |
| Compilación de producción | Correcta; carga inicial de 71 kB transferidos |

**Backend:**

- **Filas de lista:**
  - se repiten por registro, con 3, 1 y 0 registros;
  - sin solicitud se deja la fila una vez;
  - los campos generales se repiten en cada fila;
  - un campo de lista fuera de una tabla se rechaza.
- **Logo:**
  - cargar, ver, reemplazar (el archivo anterior se borra) y quitar;
  - se rechazan texto y SVG, y archivos de más de 1 MB;
  - se rechaza un logo inexistente en el `PUT`.
- **Diseño e interlineado:**
  - el pie, la alineación y el interlineado llegan al documento;
  - interlineados válidos e inválidos.

**Frontend:**

- **Campos de lista:**
  - se habilitan solo dentro de una tabla;
  - se insertan dentro de la tabla con estilo distinto;
  - el editor avisa cuando el cursor entra en una tabla.
- **Diseño del documento:**
  - validación de tipo y tamaño antes de enviar;
  - carga del logo como formulario;
  - guardado del pie y la alineación sobre la versión actual;
  - confirmación al quitar el logo;
  - el panel no se cierra al cargar el logo.
- **Interlineado:** se envía el elegido.
- **Borrador:** se conserva si la plantilla cambia de versión.

## Verificación en el navegador (14/10/2026)

Se respaldaron los datos del usuario antes de probar y se restauraron al terminar.

1. Se cargó el logo ficticio, se escribió un pie de dos líneas y se eligió la alineación a la izquierda. La API guardó
   todo y en disco quedó un solo archivo, aunque el logo se reemplazó durante la prueba.
2. Fuera de la tabla, los siete campos de lista estaban deshabilitados; dentro de la tabla, habilitados.
3. Se creó una tabla de 3 columnas con encabezado y una fila con Código Arancelario, Descripción Arancelaria y Monto
   Autorizado, y se guardó.
4. El PDF con `SOL-003` e interlineado 1.15 mostró:
   - el logo a la izquierda y el pie en todas las páginas;
   - la tabla con **3 filas** (`9999.20.01`, `9999.20.02` y `9999.30.01`) y los montos en Lempiras.

## Limitaciones

- Los logos cargados quedan en `Data/archivos/`, que no se sube al repositorio. Para incluir uno en los datos
  iniciales habría que copiarlo a `DatosSemilla/` (pendiente, si se necesita).
- Por ahora solo hay una lista de origen: el detalle de bienes. Agregar otras listas implica registrar su colección
  como lista en `OrigenesDatos`.
- Todavía no hay condiciones por fila; por ejemplo, mostrar solo los bienes con monto mayor a cierto valor.

## Ajustes posteriores (14/10/2026)

Pedido del usuario: la tabla con campos de lista no mostraba datos; numerar cada considerando; poder ocultar el panel
«Campos disponibles».

- **Tabla sin datos.** No era un fallo de la tabla. La solicitud que se proponía por defecto para `PLA-001`
  (`SOL-001`) no tenía bienes, a propósito, para probar el caso de cero registros. Se corrigió así:
  - `SOL-001` tiene ahora 2 bienes de prueba (`BIE-005` y `BIE-006`). El caso de cero registros lo sigue cubriendo
    `SOL-004`.
  - En la lista **Solicitud de prueba** se muestra cuántos bienes tiene cada solicitud (por ejemplo, «· 2 bienes» o
    «· sin bienes»), para saber cuántas filas generará la tabla.
- **Numeración de los considerandos.**
  - En el PDF, cada considerando empieza con **«CONSIDERANDO(1):»**, **«CONSIDERANDO(2):»**… en negrita, según el
    orden en que aparece.
  - Los que se omiten por su condición **no consumen número**.
  - Si un considerando empieza con una tabla, la etiqueta va en un párrafo propio antes de ella.
  - Se quitó el título general «CONSIDERANDO:», que quedaba repetido.
  - En el listado del editor cada tarjeta activa muestra su número. Es el número previsto; el definitivo depende de
    las condiciones al generar.
- **Panel «Campos disponibles» plegable.**
  - El botón › del panel lo oculta, y queda una pestaña vertical para volver a mostrarlo.
  - Con el panel oculto, el editor gana espacio (de 639 a 810 px en una pantalla de 1440 px).
  - La preferencia se recuerda en el navegador. Mientras está oculto, se pueden seguir insertando campos con el menú
    «Insertar campo» del editor.
- **Corrección encontrada al verificar.** Con un logo alto, el párrafo del logo heredaba el interlineado del documento
  y el encabezado tapaba el título. Ahora el encabezado usa interlineado sencillo y un margen superior mayor;
  comprobado con interlineado doble.
- **Pruebas:** 104 del backend y 82 del frontend, todas correctas.

## Pestaña Resuelve (15/10/2026)

Pedido del usuario: desarrollar el Resuelve igual que el Considerando, pero numerando cada bloque como PRIMERO,
SEGUNDO, TERCERO…

- **Pestaña Resuelve habilitada** con el mismo editor: condiciones, texto con formato, tablas (incluidas las filas de
  lista), campos y el «Listado de Resolución», ordenable por arrastre. Ya no hay pestañas «Próximamente».
- **Numeración en palabras.** En el PDF, cada punto resolutivo empieza con **PRIMERO:**, **SEGUNDO:**,
  **TERCERO:**… en negrita, según el orden en que aparece.
  - Los inactivos y los que no cumplen su condición no consumen número.
  - Del 11 en adelante: DÉCIMO PRIMERO, VIGÉSIMO… hasta NONAGÉSIMO NOVENO; desde el 100, «100°».
  - El listado muestra la numeración prevista.
  - El título «POR TANTO, RESUELVE:» se mantiene antes del primer punto.
- **Misma regla en los dos lados.** El servidor (`Documento/Numeracion.cs`) y la pantalla (`core/numeracion.ts`) usan la
  misma regla, y hay pruebas idénticas en ambos.
- **Datos.**
  - Se quitaron los prefijos «PRIMERO:» y «SEGUNDO:» escritos a mano en los párrafos de prueba del Resuelve; si no,
    habrían quedado repetidos.
  - Se agregó un segundo punto resolutivo de prueba (`PAR-007`, «Notificar la presente resolución a {Beneficiario}…»)
    a los datos iniciales y a los datos en uso.
- **Pruebas:** 115 del backend y 84 del frontend, todas correctas. Captura en
  [diseno/fase3d-resuelve.png](diseno/fase3d-resuelve.png).

## Pestañas Por Tanto y Firmas, y documento final (15/10/2026)

Pedido del usuario:
- agregar las pestañas «Por Tanto» (que se comporte como Cabecera) y «Firmas»;
- centrar los títulos POR TANTO y RESUELVE;
- justificar todo el texto;
- quitar del PDF el resumen final, el aviso «DOCUMENTO DE PRUEBA · DATOS SIMULADOS · SIN VALIDEZ OFICIAL» y
  «PLA-001 · generado el …».

- **Cinco pestañas en el orden del documento:** Cabecera, Considerando, Por Tanto, Resuelve y Firmas. Por Tanto y
  Firmas usan el mismo editor que Cabecera: condiciones, texto, tablas, campos y listado ordenable, sin numeración.
  Captura en [diseno/fase3e-pestanas.png](diseno/fase3e-pestanas.png).
- **Títulos centrados y en negrita.** «POR TANTO:» antes de la sección Por Tanto y «RESUELVE:» antes de los puntos
  resolutivos. Reemplazan al anterior «POR TANTO, RESUELVE:». Cada título aparece solo si su sección tiene contenido.
- **Firmas** al final del documento, con un espacio previo de 1.2 cm.
- **Texto justificado.**
  - En el PDF, los párrafos sin alineación o alineados a la izquierda salen justificados. Los centrados o a la
    derecha a propósito (título de la cabecera, firmas) se respetan.
  - En el editor, el texto se muestra justificado y el botón «Justificar» está activo por defecto.
  - Las celdas de tabla mantienen su alineación.
- **PDF sin marcas del prototipo.** Se quitaron el aviso del encabezado, el texto de plantilla y fecha del pie y la
  página «Resumen de la generación». El pie conserva el texto configurado por el usuario y «Página X de Y». Sin el aviso,
  el margen superior se redujo.
- **Datos.**
  - Los campos que sirven en Cabecera ahora también sirven en Por Tanto y Firmas.
  - Se agregaron párrafos de ejemplo a los datos iniciales y a los datos en uso: `PAR-008` (Por Tanto) y `PAR-009`
    (firma centrada con línea, nombre y cargo de prueba).
- **Pruebas:** 116 del backend y 85 del frontend, todas correctas.

Nota: las advertencias de generación (datos faltantes, párrafos omitidos) ya no aparecen en el PDF. Si se necesitan para
revisar, se pueden mostrar en la pantalla al generar.
