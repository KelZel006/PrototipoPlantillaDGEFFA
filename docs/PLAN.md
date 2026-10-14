# Plan del proyecto

Resumen del diagnóstico y del plan aprobados en la Fase 1 (13/10/2026), con los ajustes que definió el usuario.

## Decisiones

| Tema | Decisión |
|---|---|
| Repositorio | Proyecto nuevo e independiente: <https://github.com/KelZel006/PrototipoPlantillaDGEFFA> |
| Listas superiores | Son **tres**: **Rubro**, **Sub Rubro** (depende del rubro) y **Tipo Documento**. Juntas identifican una plantilla |
| Diseño | Se sigue [diseno/referencia.png](diseno/referencia.png): tres columnas (condiciones y editor, listado, campos disponibles), pestañas en bloque y campos mostrados como etiquetas resaltadas |
| Datos | Todos ficticios y marcados como de prueba. Se guardan en archivos JSON |
| Entorno | Visual Studio Code. Visual Studio queda solo como referencia, sin instalar |
| .NET | .NET 9, que es el SDK instalado. Pasar a .NET 10 (LTS) solo requiere cambiar `TargetFramework` (el soporte de .NET 9 termina el 10/11/2026) |
| Sin condiciones | Un párrafo sin condiciones se muestra siempre, como indica el diseño |
| Límite del párrafo | 2000 caracteres, también según el diseño |

## Pendiente de definir

- Formato de salida del documento (DOCX, PDF o ambos). Se decide antes de la Fase 6.
- Roles y flujo de aprobación. Por ahora se usa un usuario de prueba (`Simulacion:Usuario`).
- Valores reales de Rubro, Sub Rubro y Tipo Documento. Hoy son ficticios.

## Fases

| Fase | Contenido | Criterio de aceptación |
|---|---|---|
| 1 | Diagnóstico y plan | Plan aprobado |
| **2** | **Estructura base, almacenamiento JSON, API REST y datos de prueba** | **Angular consulta y escribe datos a través de la API; los datos persisten tras reiniciar** |
| 3 | Listas, pestañas, editor y listado de párrafos (crear, editar, duplicar, eliminar y reordenar) | Las tres secciones se administran y conservan su configuración |
| 4 | Catálogo de campos, inserción en el editor, resolución de valores y constructor de condiciones Y/O | Los campos se sustituyen y las condiciones muestran u ocultan elementos |
| 5 | Constructor de tablas dinámicas (columnas, estilos, filas generadas y condiciones por fila) | Una tabla genera sus filas con 0, 1 o varios registros |
| 6 | Motor de generación, vista previa y exportación | La vista previa coincide con la configuración |
| 7 | Pruebas y estabilización | Funciones principales cubiertas por pruebas |
| 8 | Documentación y entrega | Manual, endpoints, limitaciones y guía para pasar a base de datos |
