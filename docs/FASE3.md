# Fase 3: sección Considerando

**Alcance acordado (14/10/2026):** primero se construye la pestaña **Considerando**. Cabecera y Resuelve se habilitarán
después con el mismo componente. La salida final del documento será **PDF** (Fase 6).

**Criterio de aceptación:** la sección Considerando se administra (crear, editar, duplicar, eliminar, reordenar,
activar y desactivar) y conserva su configuración. **Resultado: cumplido** (verificación al final).

Capturas: [escritorio](diseno/fase3-considerando.png), [condiciones con grupos](diseno/fase3-condiciones.png),
[celular](diseno/fase3-movil.png).

## Qué se construyó

### Pantalla, con la distribución del diseño de referencia

| Área | Funciones |
|---|---|
| Pestañas | Cabecera, Considerando y Resuelve. Cabecera y Resuelve aparecen marcadas como «Próximamente» |
| Condiciones de visualización | Filas Campo / Operador / Valor; «+ Agregar condición»; selector **Y (todas) / O (alguna)**; **grupos anidados** (hasta 5 niveles); casilla «Mostrar solo si se cumple la condición» |
| Texto del Considerando | Editor TipTap con negrita, cursiva, subrayado, viñetas, numeración, 4 alineaciones, enlace, quitar formato e «Insertar campo»; contador 0/2000; botones Limpiar, Cancelar edición y Agregar a la lista / Actualizar |
| Listado de Considerandos | Resumen del texto con los campos como `{Etiqueta}`; condiciones resumidas sin abrir el editor; acciones editar, eliminar (con confirmación), duplicar, activar o desactivar, subir y bajar |
| Campos disponibles | Búsqueda sin distinguir tildes ni mayúsculas; agrupación por categoría; etiqueta, identificador técnico y tipo; marca «En uso»; inserción en la posición del cursor |

Detalles de comportamiento:

- **Campos en el texto.** Se ven como etiquetas resaltadas con ícono y se insertan, mueven o borran enteros. En el JSON
  solo se guarda su identificador.
- **Operadores por tipo de dato.** Los define el servidor:
  - Texto: igual, distinto, contiene y no contiene.
  - Números, montos y fechas: igual, distinto, mayor, menor, mayor o igual y menor o igual.
  - Todos los tipos: «está vacío» y «no está vacío».
- **Valor de la condición.** El control se adapta al tipo: número, calendario para fechas o Sí/No. En los campos de
  texto sugiere los valores que existen en los datos de prueba (por ejemplo, los estados de solicitud).
- **Avisos según la condición.**
  - Si la casilla está desactivada pero hay condición, se avisa que el párrafo se mostrará siempre.
  - Sin condiciones se muestra la nota del diseño: el párrafo se mostrará en todos los casos.
- **Cambios sin guardar.**
  - Un indicador muestra si hay cambios pendientes.
  - Se pide confirmación al editar otro párrafo, al cancelar o al cambiar Rubro, Sub Rubro o Tipo Documento.
  - El navegador advierte si se cierra la pestaña con cambios.
- **Conflictos.** Si otra persona modificó el párrafo, el texto escrito se conserva. Se ofrece «Recargar listado» y,
  después, guardar sobre la versión nueva.

### Backend

- **Contenido del editor validado en el servidor.**
  - Solo se aceptan párrafos, listas, saltos de línea, campos, negrita, cursiva, subrayado y enlaces http/https/mailto.
  - Se rechazan imágenes, estilos, encabezados, atributos desconocidos y enlaces `javascript:`.
  - El párrafo no puede estar vacío ni superar 2000 caracteres. Cada campo cuenta como `{Etiqueta}`, igual que el
    contador del editor.
  - Cada campo debe existir y estar permitido en la sección.
- **Campos usados.** El servidor los calcula a partir del contenido; no confía en la lista que envía el cliente.
- **Orden automático.** Con `orden: 0`, el párrafo se coloca al final de la sección.
- **Condiciones dentro del párrafo.** Se guardan en el propio párrafo, en `condicion`; ya no existe `condiciones.json`.
  Así el párrafo y su condición se guardan juntos, y al duplicar se copia la condición sin compartirla.
- **Validación de condiciones.**
  - Los campos deben salir de los datos de la solicitud; las columnas de bienes no sirven en un párrafo.
  - El operador debe ser compatible con el tipo de dato.
  - El valor debe tener el formato del tipo: números con punto, fechas AAAA-MM-DD, `true`/`false`.
  - No se aceptan grupos vacíos.
- **Nuevos endpoints.**
  - `POST /api/parrafos/reordenar`: guarda toda la sección en una sola operación y la rechaza si la lista o las
    versiones cambiaron.
  - `POST /api/parrafos/{id}/duplicar`: inserta la copia debajo del original y desplaza los siguientes.
  - `GET /api/campos/operadores` y `GET /api/campos/{id}/valores`.
- **Operación por lotes atómica** (`AplicarLoteAsync`). Crea y actualiza varios registros en una sola escritura: se
  aplican todos o ninguno. El historial de cada operación también se guarda en una sola escritura.

## Problemas encontrados y corregidos durante la verificación

1. **Formato sobre campos.** Al poner en negrita un texto que incluía un campo, TipTap aplicaba la negrita también al
   campo, y el servidor lo rechazaba. Ahora el servidor acepta formato en los elementos en línea (texto, campo y salto
   de línea), pero no en los bloques. Hay una prueba para ambos casos.
2. **Duplicar cada vez más lento** (0,35–0,42 s y creciendo). Cada registro del historial reescribía el archivo
   completo. Ahora los cambios de una operación se registran juntos: unos 0,15 s.
3. **Fechas en el listado.** Se mostraban como 2026-12-31; ahora aparecen como 31/12/2026.

## Pruebas

| Conjunto | Resultado |
|---|---|
| Backend (`dotnet test`) | **61 de 61 correctas** (37 de la Fase 2, adaptadas, más 24 nuevas) |
| Frontend (`ng test`) | **58 de 58 correctas** (21 de la Fase 2, adaptadas, más 37 nuevas) |
| Compilación de producción (`ng build`) | Correcta (177 kB transferidos; TipTap es la mayor parte) |

Pruebas nuevas del backend:

- **Contenido rechazado:** imagen, estilo, formato no permitido, enlace `javascript:`, alineación inválida, párrafo
  vacío y campo de tabla en Considerando.
- **Límite de caracteres:** 2000 se acepta y 2001 se rechaza.
- **Formato:** se acepta en campos y se rechaza en bloques.
- **Condiciones:**
  - incompatibles: campo de bienes, «contiene» en un monto, número o fecha mal escritos, valor faltante;
  - válidas, con grupos y «está vacío»;
  - grupo vacío rechazado y condición sin reglas guardada como null.
- **Párrafos:**
  - el orden automático y los campos calculados por el servidor;
  - reordenar en una operación, sin tocar los que no se movieron;
  - reordenar con datos viejos o incompletos devuelve 409 sin cambiar nada;
  - duplicar con su condición, sin compartirla, con el historial correcto.
- **Campos:**
  - operadores por tipo y valores sugeridos;
  - no se puede eliminar un campo usado en una condición.
- **Lote:** un lote con un conflicto no aplica nada; un lote válido crea y actualiza a la vez.

Pruebas nuevas del frontend:

- **Utilidades:** campos usados, conteo de caracteres igual al del servidor, texto del resumen, limpieza del JSON,
  descripción de condiciones con grupos y fechas, reglas incompletas.
- **Editor TipTap real (en jsdom):**
  - campos como etiquetas;
  - el HTML pegado pierde lo que no está en el esquema (imagen, script, encabezado, enlace `javascript:`);
  - barra de herramientas accesible.
- **Condiciones:**
  - operadores según el tipo y entrada numérica para montos;
  - sugerencias y «está vacío» sin valor;
  - Y/O y grupos.
- **Listado y panel de campos:**
  - resumen de condiciones;
  - acciones y botones deshabilitados en los extremos y al guardar;
  - búsqueda sin tildes y marca «En uso».
- **Sección completa:**
  - agregar con JSON limpio;
  - conflicto 409 con recarga sin perder el borrador;
  - eliminar con confirmación y versión;
  - reordenar;
  - confirmación al descartar cambios.
- **Selector:** confirmación al cambiar de plantilla con cambios sin guardar.

## Verificación manual en el navegador (14/10/2026)

Con la API y Angular en ejecución, sobre la plantilla `PLA-001`:

1. Se agregó una condición «Estado de Solicitud igual a En Revisión». Los operadores eran los de texto y las
   sugerencias mostraban los estados existentes.
2. Se escribió el texto, se insertaron {Beneficiario} y {Número de Solicitud} desde el panel (marcados «En uso») y se
   aplicó negrita. Se agregó como párrafo 4 y el servidor calculó los campos usados.
3. Editar mostró «Editando párrafo 1». Al cancelar con cambios apareció la confirmación; «Cancelar» conservó la edición
   y «Descartar cambios» la cerró.
4. Subir, duplicar, desactivar y eliminar (con confirmación) funcionaron, y la API confirmó órdenes y versiones.
5. Se agregó un grupo anidado con una fecha (O … o (Fecha de Resolución menor que 31/12/2026)); se guardó y el listado
   lo resumió.
6. En celular (390 px) no hay desplazamiento horizontal y las columnas se apilan.

## Pendiente para las siguientes fases

- **Cabecera y Resuelve:** habilitar las pestañas. El componente ya es reutilizable.
- **Fase 4:** sustituir los valores de los campos y evaluar las condiciones con una solicitud de prueba. Si faltan datos,
  la condición no se da por cumplida.
- **Fase 5:** tablas dinámicas (el botón «Insertar tabla» se agregará al editor).
- **Fase 6:** vista previa y PDF.

## Limitaciones conocidas

- Reordenar usa botones de subir y bajar; no hay arrastrar y soltar.
- El historial guarda los estados completos y crece con cada cambio (unos 3 kB por cambio de párrafo).
- Las condiciones se configuran y se validan, pero todavía no se evalúan: eso corresponde a la Fase 4.
