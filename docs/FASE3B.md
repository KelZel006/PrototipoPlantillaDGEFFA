# Fase 3b: tablas, orden por arrastre, PDF y pestaña Cabecera

**Pedido del usuario (14/10/2026):**

- Poder crear tablas en el editor.
- Ordenar los considerandos arrastrando las tarjetas.
- Generar un PDF con el contenido, en el orden de las pestañas Cabecera, Considerando y Resuelve.
- Habilitar la pestaña Cabecera.

**Resultado: cumplido.** Para que el PDF decida qué párrafos incluir, se adelantó parte de la Fase 4: la sustitución de
los campos por sus valores y la evaluación de las condiciones.

## Qué se construyó

### Tablas en el editor

- Botón **Insertar tabla** en la barra: se eligen filas, columnas y si la primera fila es encabezado. El máximo es
  12 columnas y 100 filas.
- Con el cursor dentro de una tabla aparece la barra **Tabla**:
  - agregar fila arriba o abajo, y columna a la izquierda o a la derecha;
  - quitar una fila o una columna;
  - activar o quitar la fila de encabezado;
  - combinar o separar celdas;
  - eliminar la tabla.
- Las celdas admiten texto, formato y **campos dinámicos**.
- No se permiten tablas dentro de otra tabla.
- **Validación en el servidor:**
  - estructura (una tabla solo contiene filas, y una fila solo contiene celdas);
  - límites de filas y columnas;
  - valores de celdas combinadas;
  - tablas anidadas.
- En el listado, una tabla aparece como `[Tabla]` dentro del resumen.

### Orden por arrastre

- Cada tarjeta del listado se puede **tomar y soltar** en otra posición; el asa ⠿ indica dónde agarrarla. Se usa
  Angular CDK 22.2.1.
- El nuevo orden se ve al instante y se guarda en una sola operación. Si el servidor lo rechaza (por ejemplo, porque
  otra persona cambió la lista), se avisa y se recarga la lista.
- Se conservan las flechas de subir y bajar, para quien use el teclado.
- **El orden del listado es el orden del documento.**

### PDF

- En el panel de la plantilla: lista **Solicitud de prueba** y botón **Generar PDF**. El PDF se abre en una pestaña
  nueva; si el navegador la bloquea, se descarga.
- **Orden del documento:** Cabecera, «CONSIDERANDO:», considerandos, «POR TANTO, RESUELVE:» y resuelve. Dentro de cada
  sección, el orden del listado.
- **Con una solicitud de prueba:**
  - Los campos se reemplazan por sus valores: montos como `L 250,000.00`, fechas como «1 de octubre de 2026».
  - Se aplican las condiciones. Un párrafo cuya condición no se cumple no aparece. Si faltan datos para evaluarla,
    **tampoco aparece** y se advierte.
  - Un campo sin dato se muestra en rojo como `[sin dato: Municipio]` y se advierte.
- **Sin solicitud:** se incluyen todos los párrafos activos y los campos se muestran como `{Etiqueta}` en azul.
- Los párrafos inactivos nunca aparecen.
- **Cada página lleva** «DOCUMENTO DE PRUEBA · DATOS SIMULADOS · SIN VALIDEZ OFICIAL» y un pie con la plantilla, la fecha
  de generación y «Página X de Y».
- **Última página, «Resumen de la generación» (solo en el prototipo):** solicitud usada, párrafos incluidos por
  sección, párrafos no incluidos con su motivo y las advertencias.
- **Endpoint:** `GET /api/plantillas/{id}/pdf?solicitudId=SOL-001`.

### Evaluación de condiciones (adelanto de la Fase 4)

- **Tres resultados posibles:** verdadero, falso o indeterminado. Faltan datos = indeterminado, que nunca cuenta como
  verdadero.
- **Y:** un falso decide; si no hay falsos pero sí un indeterminado, el resultado es indeterminado.
- **O:** un verdadero decide; si no hay verdaderos pero sí un indeterminado, el resultado es indeterminado.
- Los textos se comparan sin distinguir mayúsculas; los números, montos y fechas se comparan como tales.

### Pestaña Cabecera

- Habilitada, con el mismo editor: condiciones, texto, tablas, listado y campos.
- Cabecera y Considerando quedan cargadas al mismo tiempo, así que **cada pestaña conserva su borrador** al cambiar
  de una a otra.
- Un punto ● en la pestaña indica que tiene cambios sin guardar.
- Resuelve sigue como «Próximamente».

## Decisiones técnicas

- **Librería de PDF: PDFsharp-MigraDoc 6.2.4 (licencia MIT).** La versión 6.5.0, publicada el 14/10/2026, **cambió a
  AGPL-3.0**, que obligaría a publicar el código de la aplicación. **No se debe actualizar sin revisar la licencia.**
  Hay una advertencia en el código (`GeneradorPdfMigraDoc.cs`).
- **Fuentes del PDF.** PDFsharp usa las fuentes de Windows (Arial). Para generar PDF en Linux habrá que incluir un
  archivo de fuente con el proyecto.
- **Editor en diferido.** El editor (TipTap, tablas y arrastre) se descarga solo cuando hay una plantilla. La carga
  inicial bajó de 791 kB a 237 kB (69 kB transferidos).
- **Pruebas con datos propios.** Usan una copia fija en `backend/tests/.../DatosPrueba/`, así que los datos iniciales
  de la aplicación (`DatosSemilla/`) pueden cambiar sin romperlas.

## Problemas encontrados y corregidos

1. **Faltaban las "s" en el resumen del PDF** («e ta e una prueba»). Al editar desde la línea de comandos se perdió una
   barra invertida en la expresión regular (`s+` en lugar de `\s+`). Se corrigió y hay una prueba que lo cubre.
2. **Identificadores repetidos.** Con dos secciones cargadas a la vez, los títulos, el campo de enlace y las
   sugerencias de valores repetían el mismo `id` en la página. Ahora cada instancia genera los suyos.
3. **Tabla del PDF.** El encabezado no salía en negrita y quedaba demasiado espacio debajo de la tabla.

## Pruebas

| Conjunto | Resultado |
|---|---|
| Backend (`dotnet test`) | **87 de 87 correctas** (26 nuevas) |
| Frontend (`ng test`) | **70 de 70 correctas** (12 nuevas) |
| Compilación de producción | Correcta; carga inicial de 69 kB y editor aparte de 147 kB (transferidos) |

**Backend:**

- **Evaluación de condiciones:**
  - textos sin distinguir mayúsculas, «contiene», «está vacío»;
  - montos y fechas;
  - las seis combinaciones de Y/O con un resultado indeterminado.
- **Formato de valores:** Lempiras, fecha en letras, año sin separador de miles y Sí/No.
- **Documento:**
  - orden de las secciones;
  - inactivos omitidos;
  - campos como `{Etiqueta}` sin solicitud y sustituidos con `SOL-001`;
  - condiciones que no se cumplen con `SOL-002`;
  - datos faltantes y otro rubro con `SOL-004`;
  - el resumen conserva el texto.
- **Endpoint del PDF:** devuelve un archivo `%PDF` con nombre sugerido; responde 404 si no existe la solicitud o la
  plantilla.
- **Tablas:** se acepta una tabla con campo y celdas combinadas, y llega al PDF; se rechazan las anidadas, las de más
  de 12 columnas y las mal formadas.

**Frontend:**

- **Tablas en TipTap real:**
  - encabezado y celdas correctos;
  - barra de tabla visible y botón deshabilitado dentro de una tabla;
  - límites de filas y columnas;
  - no se insertan tablas anidadas;
  - el resumen muestra `[Tabla]`.
- **Arrastre:**
  - soltar emite el nuevo orden;
  - se guarda la sección completa;
  - el orden se ve al instante;
  - si el servidor rechaza el cambio, se avisa y se recarga.
- **PDF:**
  - primero se proponen las solicitudes de la misma combinación;
  - se abre el PDF;
  - sin solicitud no se envía el parámetro;
  - los errores del servidor se muestran aunque lleguen como archivo.
- **Pestañas y estado:**
  - Cabecera habilitada y Resuelve no;
  - las dos secciones quedan cargadas y se muestra la activa;
  - el punto de cambios sin guardar;
  - el estado de cambios se lleva por sección.

## Verificación en el navegador (14/10/2026)

Antes de probar se respaldaron los datos del usuario y al terminar se restauraron.

1. Las pestañas Cabecera y Considerando están habilitadas y Resuelve aparece como «Próximamente». Las dos secciones
   quedan cargadas.
2. En Considerando se escribió un texto, se insertó una tabla de 2×2 con encabezado y se agregó el campo
   {Beneficiario} en una celda. Apareció la barra de la tabla y el párrafo se guardó.
3. Se arrastró la quinta tarjeta al primer lugar: el listado y la API quedaron con el nuevo orden (1 al 5).
4. La pestaña Cabecera mostró «Texto de la Cabecera», «Listado de la Cabecera» y su párrafo.
5. «Generar PDF» abrió el PDF en una pestaña nueva. Con `SOL-001`, la tabla mostró el beneficiario sustituido, y el
   párrafo condicionado a «Beneficiario Ficticio Dos» quedó en el resumen como no incluido.
