# Prototipo de editor de plantillas de resoluciones

Editor dinámico de resoluciones administrativas: Cabecera, Considerando y Resuelve, con campos dinámicos,
condiciones de visualización y tablas configurables.

> **Prototipo con datos de prueba.** Todos los datos son ficticios y se guardan en archivos JSON. Todavía no hay base
> de datos. Los documentos que genere este prototipo no son resoluciones oficiales.

**Estado actual: Fase 3e (cinco secciones: Cabecera, Considerando numerado, Por Tanto, Resuelve con PRIMERO, SEGUNDO… y Firmas).** Detalle en
[docs/FASE3C.md](docs/FASE3C.md), [docs/FASE3B.md](docs/FASE3B.md),
[docs/FASE3.md](docs/FASE3.md) y [docs/FASE2.md](docs/FASE2.md). Plan general
en [docs/PLAN.md](docs/PLAN.md). Diseño de referencia en [docs/diseno/referencia.png](docs/diseno/referencia.png).

## Tecnologías

| Parte | Tecnología |
|---|---|
| Backend | C# · ASP.NET Core 9 (API REST con controladores) |
| Frontend | Angular 22 (componentes standalone, signals, sin zone.js) · TypeScript · CSS |
| Datos | Archivos JSON (almacenamiento simulado) |
| Pruebas | xUnit (backend) · Vitest (frontend) |
| Entorno | Visual Studio Code (también abre en Visual Studio 2022/2026) |

## Estructura

```
PrototipoPlantillaDGEFFA/
├── EditorResoluciones.sln
├── backend/
│   ├── EditorResoluciones.Api/            Controladores REST, manejo de errores, configuración
│   │   ├── DatosSemilla/                  Datos de prueba iniciales (versionados)
│   │   └── Data/                          Datos en ejecución (se crea solo; no se versiona)
│   ├── EditorResoluciones.Dominio/        Modelos, interfaz IRepositorio<T>, validaciones y servicios
│   ├── EditorResoluciones.Infraestructura/ Almacenamiento JSON (AlmacenJson, RepositorioJson<T>)
│   └── tests/EditorResoluciones.Pruebas/  Pruebas unitarias y de integración
├── frontend/                              Aplicación Angular
│   └── src/app/
│       ├── core/                          Modelos, ApiService, estado del contexto, mensajes de error
│       └── editor/                        Selector de contexto, panel de plantilla, estado de datos
└── docs/                                  Plan, documentación por fase y diseño de referencia
```

## Requisitos

- SDK de .NET 9 (`dotnet --list-sdks`)
- Node.js 22.22+ o 24.15+ y npm (`node -v`)
- Visual Studio Code con las extensiones recomendadas (C# Dev Kit y Angular Language Service)

## Ejecutar

Primera vez:

```bash
cd frontend
npm install
```

En dos terminales:

```bash
dotnet run --project backend/EditorResoluciones.Api --launch-profile http
```
```bash
cd frontend
npm start
```

Abra <http://localhost:4200>. Angular envía `/api` a la API en `http://localhost:5080` mediante
[frontend/proxy.conf.json](frontend/proxy.conf.json).

En VS Code: **Terminal → Ejecutar tarea → Iniciar todo**, o en **Ejecutar y depurar** elija **API + Frontend**.

### Datos de prueba

- Al iniciar, la API copia cada archivo de `DatosSemilla/` a `Data/` si todavía no existe ahí. Los cambios se guardan
  en `Data/` y se conservan al reiniciar.
- **Para volver a los datos iniciales**, detenga la API y borre la carpeta `backend/EditorResoluciones.Api/Data`.
- Para que los datos actuales pasen a ser los iniciales, copie los `*.json` de `Data/` (sin los `.bak`) a
  `DatosSemilla/` y súbalos al repositorio.
- Las pruebas automáticas no usan `DatosSemilla/`: tienen su propia copia fija en
  `backend/tests/EditorResoluciones.Pruebas/DatosPrueba/`. Así, cambiar los datos iniciales no las rompe.
- Cada escritura deja la versión anterior del archivo como `*.json.bak`.
- Si un archivo está dañado, la API no lo sobrescribe. Guarda una copia en `Data/cuarentena/` y responde con un error
  claro (503). Para recuperarlo, restaure el `.bak` o borre el archivo.

## Pruebas

```bash
dotnet test EditorResoluciones.sln
```
```bash
cd frontend
npx ng test --watch=false
```

## API REST

Todas las colecciones de la tabla ofrecen `GET` (listar), `GET /{id}`, `POST`, `PUT /{id}` y `DELETE /{id}?version=n`.

| Ruta | Filtros de `GET` |
|---|---|
| `/api/rubros` | — |
| `/api/subrubros` | `rubroId` |
| `/api/tipos-documento` | — |
| `/api/plantillas` | `rubroId`, `subRubroId`, `tipoDocumentoId` |
| `/api/parrafos` | `plantillaId`, `seccion` (ordenados por sección y orden) |
| `/api/campos` | — |
| `/api/solicitudes` | — |
| `/api/detalles-bienes` | `solicitudId` |

Otras rutas:

- `GET /api/plantillas/{id}/pdf?solicitudId=&interlineado=`: PDF de prueba con Cabecera, Considerando y Resuelve.
  Con `solicitudId` se sustituyen los campos, se aplican las condiciones y las filas con campos de lista se repiten
  por cada registro. `interlineado` puede ser `1`, `1.15`, `1.5` (predeterminado) o `2`.
- `POST /api/plantillas/{id}/logo` (multipart, campo `archivo`) y `DELETE /api/plantillas/{id}/logo`: logo del
  encabezado (PNG o JPEG hasta 1 MB, validado por su contenido). `GET /api/archivos/{id}` devuelve la imagen.
- `POST /api/parrafos/reordenar`: guarda el orden completo de una sección en una sola operación.
  Cuerpo: `{ plantillaId, seccion, elementos: [{ id, version }] }`.
- `POST /api/parrafos/{id}/duplicar`: copia el párrafo, con su condición, justo debajo del original.
- `GET /api/campos/operadores`: operadores de comparación permitidos por tipo de dato.
- `GET /api/campos/{id}/valores`: valores del campo en los datos de prueba (sugerencias para las condiciones).

- `GET /api/historial?coleccion=&entidadId=`: registro de cambios, solo lectura.
- `GET /api/salud`: estado de cada archivo JSON.
- `GET /openapi/v1.json`: especificación OpenAPI, solo en desarrollo.

Respuestas de error (ProblemDetails, en español):

| Código | Cuándo |
|---|---|
| 400 | Validación o referencias inexistentes |
| 404 | El registro no existe |
| 409 | Versión desactualizada o identificador repetido |
| 503 | Archivo de datos dañado |

## PDF

- Se genera con **PDFsharp-MigraDoc 6.2.4 (licencia MIT)**. **No actualizar a la 6.5.0 o posterior** sin revisar la
  licencia: desde esa versión es AGPL-3.0.
- Usa las fuentes de Windows (Arial). Para generar PDF en Linux habrá que incluir un archivo de fuente con el proyecto.
- Orden: Cabecera, Considerando (CONSIDERANDO(n):), «POR TANTO:», «RESUELVE:» (PRIMERO:, SEGUNDO:…) y Firmas. El texto va
  justificado; los títulos, centrados. El pie lleva el texto configurado y «Página X de Y».

## Reglas de los datos

- **Control de versiones optimista.** Cada registro tiene `version`. Un `PUT` o `DELETE` con una versión que no
  coincide con la guardada se rechaza con 409, así nadie pisa sin saberlo el cambio de otra persona.
- **Validación en el servidor.** Se revisan las referencias entre colecciones (por ejemplo, que el sub rubro pertenezca
  al rubro) y se impide eliminar registros que otros usan.
- **Campos dinámicos.** Su origen solo puede ser una colección y una propiedad de una lista permitida. No se ejecutan
  consultas ni código escrito por el usuario.
- **Contenido del editor.** Se guarda como JSON de TipTap y el servidor solo acepta párrafos, listas, saltos de línea,
  campos, negrita, cursiva, subrayado y enlaces http/https/mailto. Calcula por su cuenta los campos usados.
- **Condiciones.** Cada operador debe corresponder al tipo de dato del campo, y el valor debe tener el formato del
  tipo (números con punto, fechas AAAA-MM-DD).
- **Contexto.** Rubro + Sub Rubro + Tipo Documento identifican una única plantilla.
