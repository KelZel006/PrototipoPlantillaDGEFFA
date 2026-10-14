# Fase 2: estructura base y datos JSON

**Criterio de aceptación:** Angular consulta los datos simulados a través del backend y las escrituras funcionan sin
una base de datos real. **Resultado: cumplido** (verificación al final).

## Qué se construyó

### Backend (ASP.NET Core 9)

- **Dominio** (`EditorResoluciones.Dominio`). No depende del almacenamiento.
  - Modelos: Rubro, SubRubro, TipoDocumento, Plantilla, Párrafo (contenido estructurado de TipTap/ProseMirror, nunca
    HTML libre), Campo, Condición (grupos Y/O anidados), Solicitud, DetalleBien y RegistroHistorial.
  - `IRepositorio<T>`: el contrato que en el futuro implementará la base de datos.
  - `ServicioEntidades<T>`: valida en el servidor, controla las versiones, asigna fechas y registra el historial.
  - Un validador por colección, que revisa las referencias y no permite eliminar registros en uso.
- **Infraestructura** (`EditorResoluciones.Infraestructura`).
  - `AlmacenJson`:
    - un bloqueo por colección;
    - escritura atómica (archivo temporal y reemplazo, con respaldo `.bak`);
    - copia de los datos semilla cuando falta un archivo;
    - cuarentena de los archivos dañados, sin sobrescribirlos;
    - lista cerrada de colecciones, para que ninguna ruta de archivo llegue desde el cliente.
  - `RepositorioJson<T>`: identificadores con prefijo (por ejemplo `PLA-…`) y control de versión.
- **API** (`EditorResoluciones.Api`).
  - Controladores REST para 9 colecciones, más historial (solo lectura) y diagnóstico (`/api/salud`).
  - Errores en formato ProblemDetails y en español, también cuando el JSON enviado está mal formado.
  - Límite de 1 MB por petición.

### Datos de prueba (`backend/EditorResoluciones.Api/DatosSemilla/`)

| Archivo | Contenido ficticio |
|---|---|
| `rubros.json` | 3 rubros |
| `subRubros.json` | 5 sub rubros, cada uno ligado a su rubro |
| `tiposDocumento.json` | 3 tipos (aprobación, denegación, ampliación) |
| `plantillas.json` | 2 plantillas: `PLA-001` con párrafos y `PLA-002` vacía |
| `parrafos.json` | 6 párrafos en las tres secciones: con y sin condición, uno inactivo |
| `campos.json` | Los 11 campos del diseño, más 5 de detalle de bienes para tablas |
| `condiciones.json` | `CON-001` (estado = "En Curso") y `CON-002` (monto > 100000 **O** tipo contiene "Importación")  _(en la Fase 3 estas condiciones pasaron a guardarse dentro de cada párrafo y el archivo se eliminó)_ |
| `solicitudes.json` | 4 solicitudes: `SOL-004` tiene datos faltantes para probar condiciones |
| `detallesBienes.json` | `SOL-001` con 0 bienes, `SOL-002` con 1 y `SOL-003` con 3 |
| `historial.json` | Vacío al inicio |

### Frontend (Angular 22)

- `ApiService`: único acceso a los datos. Angular no toca archivos.
- `ContextoService`: guarda la selección de las tres listas. Al cambiar el rubro se descarta el sub rubro.
- Componentes:
  - **Selector de contexto:** Rubro, Sub Rubro (se carga según el rubro) y Tipo Documento, con los íconos del diseño.
  - **Plantilla seleccionada:** muestra la plantilla de la combinación elegida y el resumen de párrafos por sección.
    Si la combinación no tiene plantilla, permite crearla.
  - **Datos de prueba:** estado de cada archivo JSON.
- Tokens visuales tomados del diseño de referencia. Diseño adaptable (verificado a 1366 px y a 390 px).

## Pruebas

| Conjunto | Resultado |
|---|---|
| Backend (`dotnet test`) | **37 de 37 correctas** |
| Frontend (`ng test`) | **21 de 21 correctas** |
| Compilación de producción (`ng build`) | Correcta (54 kB transferidos) |

Pruebas del backend:

- **Almacén JSON:**
  - archivo inexistente, con y sin semilla;
  - respaldo `.bak`;
  - archivo dañado o vacío (no se sobrescribe y se copia una sola vez a cuarentena);
  - 50 escrituras simultáneas sin pérdida;
  - una operación fallida no modifica el archivo;
  - se rechazan colecciones no registradas, incluido `../appsettings`.
- **Repositorio:**
  - identificadores generados;
  - identificador repetido;
  - conflicto de versión;
  - eliminación con versión;
  - persistencia con un almacén nuevo (equivale a reiniciar).
- **Datos semilla:**
  - identificadores únicos;
  - solicitudes con 0, 1 y varios bienes;
  - una solicitud con datos faltantes;
  - los campos insertados en cada párrafo coinciden con `camposUsados`;
  - cada registro cumple las validaciones del servidor.
- **API de extremo a extremo:**
  - filtros;
  - crear y comprobar tras reiniciar la API, y que quede en el historial;
  - plantilla repetida;
  - sub rubro de otro rubro;
  - 409 por versión desactualizada;
  - editar un párrafo no altera los demás;
  - no se elimina un rubro con sub rubros;
  - origen de campo no permitido;
  - referencias inexistentes;
  - JSON mal formado (errores en español);
  - archivo dañado devuelve 503 y aparece en el diagnóstico.

Pruebas del frontend:

- mensajes de error;
- reglas del contexto;
- parámetros de `ApiService`;
- carga dependiente de Sub Rubro;
- solo se ofrecen registros activos;
- error de conexión con opción de reintentar;
- resumen por sección;
- creación de plantilla y error de validación mostrado.

## Verificación manual (14/10/2026)

1. Se inició la API (`:5080`) y Angular (`:4200`).
2. `GET /api/salud` a través del proxy de Angular: estado correcto en las 10 colecciones.
3. `POST /api/plantillas` a través del proxy: 201. Quedó registrada en el historial y se creó `plantillas.json.bak`.
4. Se reinició la API y la plantilla seguía disponible.
5. En el navegador: al elegir RUB-001 / SUB-001 / TDO-001 aparece PLA-001 con 1 / 3 / 2 párrafos por sección.
   Con una combinación sin plantilla, el botón **Crear plantilla de prueba** la creó desde la interfaz.
6. La consola del navegador no mostró errores. Capturas en [diseno/fase2-escritorio.png](diseno/fase2-escritorio.png)
   y [diseno/fase2-movil.png](diseno/fase2-movil.png).

## Limitaciones conocidas

- Los bloqueos protegen las escrituras dentro de **un solo proceso**. No se deben ejecutar varias instancias de la API
  sobre la misma carpeta de datos.
- Las validaciones que consultan otras colecciones se hacen antes de la escritura y no en la misma transacción. Basta
  para un prototipo, pero una base de datos real debe garantizarlo con transacciones.
- El historial guarda el estado anterior y el nuevo completos, por lo que el archivo crece con cada cambio.
- No hay autenticación. El usuario del historial es el de prueba configurado en `appsettings.json`.
- Si falla el registro en el historial después de guardar, el cambio queda guardado pero la API devuelve un error.
