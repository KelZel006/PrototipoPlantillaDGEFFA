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

## Espacio entre firmas e interlineado de 0.8 a 2.0 (15/10/2026)

Pedido del usuario: poder manejar el espacio entre firmas, e interlineado de 0.8, 0.9, 1, 1.1… hasta 2.

- **Espacio entre firmas.**
  - Nuevo campo en «Diseño del documento: logo, pie de página y firmas». Se guarda con la plantilla
    (`espacioFirmasCm`).
  - Es el espacio, en centímetros, antes de **cada bloque de firma** del listado de Firmas: separa las firmas entre sí
    y la primera del texto anterior.
  - Admite valores de 0 a 10 cm, con un decimal. El predeterminado es 1.2 cm, el mismo que había antes.
  - El servidor rechaza valores fuera de rango.
  - Para dos firmas en el mismo nivel, una al lado de la otra, se puede usar una tabla en la pestaña Firmas.
- **Interlineado de 0.8 a 2.0, de 0.1 en 0.1** (13 opciones; 1.0 es sencillo y 2.0 es doble).
  - El predeterminado sigue siendo 1.5. Desaparece el 1.15.
  - El servidor rechaza cualquier otro valor.
  - Las firmas usan como mínimo interlineado 1.0, para que la línea de firma no se monte sobre el nombre.
- **Verificación.** PDF con 3 firmas, 3 cm de espacio e interlineado 0.8 y 2.0. Los datos del usuario se respaldaron
  antes y se restauraron después. Captura en
  [diseno/fase3f-firmas-interlineado.png](diseno/fase3f-firmas-interlineado.png).
- **Pruebas:** 125 del backend y 86 del frontend, todas correctas.

## Rubros y sub rubros reales, lista Documento y pestaña inicial (15/10/2026)

Pedido del usuario: cargar los rubros y sub rubros de la información adjunta, abrir siempre en la pestaña Cabecera,
llenar Tipo Documento y agregar la lista Documento.

- **Rubros y sub rubros.**
  - La imagen adjunta venía recortada, así que los datos se tomaron de la planilla `Libro1 (1).xlsx` (hoja «Hoja1»).
  - Son 23 rubros y 167 sub rubros. El id de cada uno es su código (por ejemplo `1000` «POR MANDATO
    CONSTITUCIONAL»), y cada sub rubro apunta a su rubro con el código de la columna PARAMETRO.
  - El límite del nombre subió a 300 caracteres, porque hay descripciones de 152 y 178.
  - Las plantillas y solicitudes de prueba que usaban los rubros ficticios se pasaron a rubros reales equivalentes:

    | Antes | Ahora |
    |---|---|
    | RUB-001 | 1100 |
    | RUB-002 | 1800 |
    | RUB-003 | 1600 |
    | SUB-001 | 1107 |
    | SUB-002 | 1102 |
    | SUB-003 y SUB-004 | 1801 |
    | SUB-005 | 1601 |

  - Los nombres guardados de esas plantillas no cambian. Se pueden renombrar si hace falta.
- **Tipo Documento.** Constancia de Registro (desde el 16/10/2026, Constancia de Obligados Tributarios), Dictamen, Resolución y Certificación, en ese orden.
- **Lista Documento (nueva).**
  - Es la cuarta lista, con Original, Modificación y Aclaratoria.
  - Forma parte de la combinación que identifica la plantilla: la misma combinación de Rubro, Sub Rubro y Tipo
    Documento puede tener una plantilla Original, otra de Modificación y otra Aclaratoria.
  - Catálogo nuevo `clasesDocumento.json` (`GET /api/clases-documento`). Las plantillas existentes quedaron como
    «Original».
  - Una plantilla nueva se llama «Tipo (Documento) – Sub Rubro».
- **Pestañas.** Al abrir una plantilla, el editor se posiciona siempre en Cabecera. Guardar o crear una versión
  nueva de la misma plantilla no cambia la pestaña.
- **Verificación.** Se revisaron en el navegador las cuatro listas y el regreso a Cabecera al cambiar de plantilla.
  Los datos del usuario no cambiaron. Captura en [diseno/fase3g-listas.png](diseno/fase3g-listas.png).
- **Pruebas:** 127 del backend y 87 del frontend, todas correctas.

## Solicitudes de prueba relacionadas con cada plantilla (15/10/2026)

Pedido del usuario: relacionar el resto de la información con las plantillas ya generadas.

- **Solicitudes nuevas** (todas con bienes, para que la tabla de Código Arancelario, Descripción Arancelaria y Monto
  Autorizado salga llena):

  | Solicitud | Rubro / Sub Rubro | Bienes |
  |---|---|---|
  | PRUEBA-0005-2026 | 1100 / 1102 ORGANISMOS | 2 |
  | PRUEBA-0006-2026 | 1100 / 1101 MISIONES | 1 |
  | PRUEBA-0007-2026 | 1000 / 1001 MAESTROS | 3 |
  | PRUEBA-0008-2026 | 1000 / 1002 UNAH | 2 |
  | PRUEBA-0009-2026 | 1800 / 1801 ONGS | 2 |

  PRUEBA-0004-2026 sigue sin bienes y con datos faltantes, para probar esos casos.
- **Plantillas con contenido.** PLA-002 (Dictamen, 1800 / 1801) y la nueva PLA-003 (Resolución, 1600 / 1601 DEPÓSITOS,
  para PRUEBA-0003-2025) tienen ahora párrafos en las cinco secciones: Cabecera con título y VISTO, cuatro
  considerandos (uno con la tabla de bienes y otro que solo aparece si el monto supera 100,000), Por Tanto, dos puntos
  del Resuelve y una firma.
- **Verificación.** PDF generado para cada plantilla con su solicitud. Pruebas del backend: 127, todas correctas.

## Certificación con texto corrido (15/10/2026)

Pedido del usuario: la Certificación lleva en general la misma información que la Resolución, pero todo va pegado, sin
espacio en el texto; solo los cuadros conservan su forma. El documento ya debe generarse así.

- **Regla en el catálogo.** Cada tipo de documento tiene la marca `textoCorrido`. Solo la Certificación la tiene
  activa, así que el comportamiento no depende del nombre y se puede activar para otro tipo si hace falta.
- **Cómo se genera** (`Dominio/Documento/TextoCorrido.cs`):
  - Cabecera, Considerando, Por Tanto y Resuelve se transcriben seguidos, en un solo párrafo justificado, separados
    solo por un espacio. Los saltos de línea también se vuelven espacios.
  - Los títulos «POR TANTO:» y «RESUELVE:» y las etiquetas CONSIDERANDO(n): y PRIMERO:, SEGUNDO:… quedan en negrita
    dentro del texto.
  - Las tablas cortan el texto, se presentan como cuadro y el texto continúa después.
  - Las firmas conservan su presentación (centradas, con su espacio).
  - Negrita, cursiva y subrayado se conservan.
- **Pantalla.** Al elegir Certificación, el panel avisa que el texto se genera corrido.
- **Plantilla de ejemplo.** PLA-004: Certificación (Original) de 1100 / 1107, con una copia del contenido de la
  Resolución PLA-001. Se genera con PRUEBA-0001-2026.
- **Pruebas:** 130 del backend y 88 del frontend, todas correctas.

## Tamaño del texto y justificado del PDF (16/10/2026)

Pedido del usuario: agregar el tamaño del texto en la barra del editor y corregir el borde derecho del PDF, que se veía
en escalera y no justificado.

- **Justificado del PDF (corrección).**
  - Causa: el editor no guarda la alineación cuando es la predeterminada (justificada). Al no encontrarla, el generador
    asignaba al párrafo su alineación actual, que en MigraDoc vale «izquierda» por defecto. Así anulaba el justificado
    del estilo y el borde derecho quedaba disparejo.
  - Ahora, sin alineación guardada, no se asigna nada y el párrafo hereda el justificado. Afecta a todos los documentos,
    también a la Certificación con texto corrido.
- **Tamaño del texto.**
  - Nueva lista en la barra del editor (junto a Negrita, Cursiva y Subrayado): Normal, 8, 9, 10, 11, 12, 13, 14, 16,
    18, 20 y 24 puntos. Se aplica al texto seleccionado o al que se escriba después.
  - Se guarda en el párrafo como formato del texto (`textStyle` con `fontSize`, por ejemplo «14pt») y el PDF lo
    respeta, también dentro de tablas y en el texto corrido.
  - El servidor solo acepta los tamaños de la lista. Al pegar texto desde Word, el editor conserva los tamaños de la
    lista y descarta los demás.
  - Extensión nueva: `@tiptap/extension-text-style` 3.31.4 (MIT).
- **Captura:** [diseno/fase3h-tamano-texto.png](diseno/fase3h-tamano-texto.png).
- **Pruebas:** 137 del backend y 90 del frontend, todas correctas.

## La Certificación se copia de la Resolución (16/10/2026)

Pedido del usuario: cada vez que se guarda o actualiza la Resolución, la misma información debe quedar en la
Certificación.

- **Regla en el catálogo.** Los tipos de documento tienen `copiaDeTipoDocumentoId`. La Certificación (TDO-004) es
  copia de la Resolución (TDO-001). La copia se hace entre plantillas con el mismo Rubro, Sub Rubro y Documento.
- **Cuándo se copia** (`Dominio/Servicios/SincronizadorCopias.cs`): después de cada cambio guardado en la Resolución,
  es decir, al crear, actualizar, eliminar, duplicar o reordenar un párrafo, y al cambiar el diseño (logo, pie de
  página o espacio entre firmas).
  - Si la Certificación no existe, se crea: «Certificación (Documento) – Sub Rubro».
  - Sus párrafos quedan iguales a los de la Resolución: contenido con su formato, sección, orden, activo y condición.
    Cada copia guarda su párrafo de origen (`origenParrafoId`). Lo que ya no está en la Resolución se elimina.
  - Solo se reescribe lo que cambió, y todo queda en el historial.
  - Una Resolución que todavía no se ha guardado desde este cambio tendrá su Certificación al primer guardado.
- **La Certificación no se edita directamente** (`ReglaCopiasNoEditables`). El servidor rechaza crear, modificar,
  eliminar, duplicar o reordenar sus párrafos y cambiar su diseño. En la pantalla, al elegir Certificación, se ocultan
  las pestañas y el diseño, y aparece un aviso con el botón «Ir a la Resolución». Se puede generar su PDF, con el
  texto corrido.
- **Datos.** PLA-004 quedó enlazada con PLA-001.
- **Captura:** [diseno/fase3i-certificacion-copia.png](diseno/fase3i-certificacion-copia.png).
- **Pruebas:** 142 del backend y 91 del frontend, todas correctas.

## «Resolución» → «Certificación» en la copia y logos compartidos (16/10/2026)

Pedido del usuario: al guardar, cambiar automáticamente la palabra resolución por certificación, con tilde, en
mayúsculas y en minúsculas.

- **Cambio de palabra** (`Dominio/Servicios/CambioPalabra.cs`). Al copiar cada párrafo a la Certificación, en el
  texto se cambia el nombre del tipo de origen por el de la copia. Las palabras salen de los nombres del catálogo.

  | En la Resolución | En la Certificación |
  |---|---|
  | RESOLUCIÓN / RESOLUCION | CERTIFICACIÓN |
  | Resolución / Resolucion | Certificación |
  | resolución / resolucion | certificación |
  | resoluciones / RESOLUCIONES | certificaciones / CERTIFICACIONES |

  Solo cambia palabras completas («resolutivo» o «irresolución» no se tocan) y no toca los campos dinámicos. La
  Resolución conserva su texto.
- **Logos compartidos (corrección).** La Resolución y su Certificación usan el mismo archivo de logo. Ahora el
  servidor no borra un archivo de logo que otra plantilla todavía use. Si una carga de logo se rechaza, el archivo
  nuevo se descarta.
  - Antes de esta corrección se había perdido el logo de PLA-001 en los datos en uso (se compartía con PLA-004 y se
    borró al reemplazar el de PLA-004). Se reparó asignándole el logo vigente de la Certificación.
- **Pruebas:** 155 del backend y 91 del frontend, todas correctas.

## La Certificación también se edita (16/10/2026)

Pedido del usuario: al elegir una Certificación, que se pueda editar si se desea, pero que siempre se actualice con todo
lo que se haga en la Resolución.

- **Se edita como cualquier plantilla.** Se quitó `ReglaCopiasNoEditables`: en la Certificación se pueden crear,
  modificar, eliminar, duplicar y reordenar párrafos, y cambiar el diseño (logo, alineación, pie de página y espacio
  entre firmas). En la pantalla aparecen las pestañas y el diseño. El aviso explica la regla y conserva el botón «Ir a
  la Resolución».
- **La Resolución sigue mandando, dato por dato** (`SincronizadorCopias`). Después de cada guardado en la Resolución
  solo pasa a la Certificación **lo que cambió en ese guardado**:
  - Párrafo modificado: se copian solo los datos que cambiaron (texto, activo, «mostrar solo si cumple», condición,
    sección u orden). Si ese mismo dato se había editado en la Certificación, lo reemplaza el de la Resolución; lo
    demás que se editó en la Certificación se conserva.
  - Párrafo nuevo: se agrega en la Certificación después de la copia del párrafo que lo precede en la Resolución.
  - Párrafo eliminado en la Resolución: se elimina su copia, aunque se haya editado en la Certificación.
  - Diseño: solo los datos que cambiaron. Si la Certificación tenía un logo propio y la Resolución cambia el suyo, la
    Certificación pasa al de la Resolución y el archivo propio, si ya nadie lo usa, se borra.
  - Si se eliminó en la Certificación un párrafo copiado y luego cambia ese párrafo en la Resolución (incluso de
    posición), vuelve a aparecer. Para ocultarlo en la Certificación sin perderlo, conviene desactivarlo.
- **Lo propio de la Certificación no se toca.** Los párrafos agregados o duplicados en la Certificación no tienen
  `origenParrafoId` y la sincronización no los modifica ni los elimina. Las secciones afectadas se renumeran 1, 2, 3…
- **Los cambios de la Certificación no pasan a la Resolución.**
- **`origenParrafoId` lo controla el servidor.** Al guardar un párrafo se conserva el valor guardado, aunque el
  cliente envíe otro.
- **Pruebas:** 156 del backend y 91 del frontend, todas correctas.
