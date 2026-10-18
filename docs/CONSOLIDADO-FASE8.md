# Documento Consolidado — Fase 8: estrategia para la base de datos real

Fecha: 17/10/2026. Estado: **propuesta para aprobación**. No se crearon tablas ni scripts ejecutables: este documento
define cómo reemplazar los archivos JSON sin trasladar la lógica de negocio.

**Criterio de aceptación:** estrategia aprobada; el dominio no cambia.

## 1. Decisiones pendientes

| # | Tema | Recomendación |
|---|---|---|
| D12 | Motor de base de datos | **SQL Server 2022** (o Azure SQL), por coherencia con .NET, Visual Studio y el entorno institucional habitual. PostgreSQL 16 también sirve sin cambios de diseño |
| D13 | Acceso a datos | **Entity Framework Core 10**, con migraciones versionadas en el repositorio |
| D14 | Identidad de los usuarios | El proveedor institucional (Active Directory / Entra ID con OpenID Connect). Los roles Analista, Oficial Jurídico y Administrador, como grupos o *claims* |
| D15 | Archivos (logos) | Una tabla `Archivos` con `varbinary(max)` mientras sean pocos y pequeños (hasta 1 MB); un almacén de objetos si crecen |

## 2. Qué se reemplaza y qué no

| Pieza | Hoy (JSON) | Con base de datos | ¿Cambia el dominio? |
|---|---|---|---|
| `IRepositorio<T>` | `RepositorioJson<T>` | `RepositorioEf<T>` sobre `DbContext` | No |
| `IUnidadDeTrabajo` / `ITransaccion` | `UnidadDeTrabajoJson` (diario y bloqueos) | `UnidadDeTrabajoEf`: una transacción del motor | No |
| Control de concurrencia (`Version`) | Comparación en memoria | `Version` como *token* de concurrencia (`IsConcurrencyToken`); el motor rechaza la actualización con versión vieja | No |
| `IUsuarioActual` | `UsuarioPrueba` (encabezado) | Usuario autenticado y sus *claims* | No (la API sí: autenticación) |
| `ServicioAutorizacion` | Busca el usuario en `usuarios.json` | Lee los roles del usuario autenticado; la tabla `Usuarios` queda como espejo opcional | Mínimo: cambia de dónde salen los roles |
| `IAlmacenArchivos` | Carpeta `Data/archivos` | Tabla `Archivos` o almacén de objetos | No |
| Diagnóstico y semilla | Archivos y `DatosSemilla/` | Migración inicial y una carga de datos idempotente | No |

**Registro.** `RegistroInfraestructura` tendrá `AgregarAlmacenamientoSql(...)` junto a `AgregarAlmacenamientoJson()`, y
`Program.cs` elegirá uno según la configuración (`Almacenamiento:Tipo = Json | Sql`).

**Ajuste necesario.** Los servicios del dominio hoy hacen `ListarAsync()` y filtran en memoria. Con los volúmenes del
prototipo no importa, pero con años de documentos sí. Se agregarán a `IRepositorio<T>` **consultas con filtro**
(`ListarAsync(Expression<Func<T,bool>>)`, implementadas con `Where` en EF y con LINQ en JSON) y se usarán en los puntos
de mayor volumen: versiones, propuestas, movimientos e historial. La lógica de negocio no cambia; solo cómo se piden
los datos.

## 3. Esquema propuesto

Ilustrativo, para revisión; no es un script ejecutable. Los nombres siguen los del dominio.

```sql
-- Catálogos
CREATE TABLE Rubros            (Id varchar(20) PRIMARY KEY, Nombre nvarchar(300) NOT NULL, Activo bit NOT NULL, Version int NOT NULL);
CREATE TABLE SubRubros         (Id varchar(20) PRIMARY KEY, RubroId varchar(20) NOT NULL REFERENCES Rubros, Nombre nvarchar(300) NOT NULL,
                                Activo bit NOT NULL, Version int NOT NULL);
CREATE TABLE ClasesDocumento   (Id varchar(20) PRIMARY KEY, Nombre nvarchar(150) NOT NULL, Activo bit NOT NULL, Version int NOT NULL);
CREATE TABLE BloquesDocumentales (Id varchar(40) PRIMARY KEY, Nombre nvarchar(80) NOT NULL, Titulo nvarchar(80) NULL,
                                Numeracion varchar(20) NOT NULL, EsFirmas bit NOT NULL, ContextoCampos varchar(20) NOT NULL,
                                Descripcion nvarchar(300) NULL, Activo bit NOT NULL, Version int NOT NULL);
CREATE TABLE TiposDocumento    (Id varchar(20) PRIMARY KEY, Nombre nvarchar(150) NOT NULL, TextoCorrido bit NOT NULL,
                                CopiaDeTipoDocumentoId varchar(20) NULL REFERENCES TiposDocumento, Activo bit NOT NULL, Version int NOT NULL);
CREATE TABLE TipoDocumentoBloques (TipoDocumentoId varchar(20) REFERENCES TiposDocumento, BloqueId varchar(40) REFERENCES BloquesDocumentales,
                                Orden int NOT NULL, Obligatorio bit NOT NULL, PRIMARY KEY (TipoDocumentoId, BloqueId));
CREATE TABLE Campos            (Id varchar(60) PRIMARY KEY, Etiqueta nvarchar(100) NOT NULL, Categoria nvarchar(100) NOT NULL,
                                TipoDato varchar(20) NOT NULL, OrigenColeccion varchar(40) NOT NULL, OrigenPropiedad varchar(60) NOT NULL,
                                Contextos varchar(200) NOT NULL, Descripcion nvarchar(500) NULL, Version int NOT NULL);

-- Plantillas y versiones
CREATE TABLE Plantillas (
    Id varchar(20) PRIMARY KEY, Nombre nvarchar(200) NOT NULL,
    RubroId varchar(20) NOT NULL REFERENCES Rubros, SubRubroId varchar(20) NOT NULL REFERENCES SubRubros,
    TipoDocumentoId varchar(20) NOT NULL REFERENCES TiposDocumento, ClaseDocumentoId varchar(20) NOT NULL REFERENCES ClasesDocumento,
    Version int NOT NULL,
    CONSTRAINT UQ_Plantilla_Identidad UNIQUE (RubroId, SubRubroId, TipoDocumentoId, ClaseDocumentoId));   -- identidad funcional

CREATE TABLE VersionesPlantilla (
    Id varchar(20) PRIMARY KEY, PlantillaId varchar(20) NOT NULL REFERENCES Plantillas, NumeroVersion int NOT NULL,
    Vigencia varchar(10) NOT NULL CHECK (Vigencia IN ('Vigente','NoVigente')),
    Contenido nvarchar(max) NOT NULL CHECK (ISJSON(Contenido) = 1),          -- diseño, bloques y párrafos: documento inmutable
    Huella char(64) NOT NULL, VersionAnteriorId varchar(20) NULL REFERENCES VersionesPlantilla,
    ConsolidadoId varchar(20) NULL, CargaInicial bit NOT NULL,
    PublicadaPor varchar(60) NOT NULL, FechaPublicacion datetimeoffset NOT NULL,
    InicioVigencia datetimeoffset NOT NULL, FinVigencia datetimeoffset NULL, Version int NOT NULL,
    CONSTRAINT UQ_Version_Numero UNIQUE (PlantillaId, NumeroVersion));
-- Regla 14 garantizada por el motor: como máximo una versión vigente por plantilla.
CREATE UNIQUE INDEX UX_Version_Vigente ON VersionesPlantilla (PlantillaId) WHERE Vigencia = 'Vigente';

-- Documento Consolidado
CREATE TABLE Consolidados (
    Id varchar(20) PRIMARY KEY, Numero char(13) NOT NULL UNIQUE, Anio int NOT NULL, Secuencia int NOT NULL CHECK (Secuencia BETWEEN 1 AND 9999),
    Estado varchar(12) NOT NULL CHECK (Estado IN ('EnRegistro','Verificado','Aprobado')), AnalistaId varchar(60) NOT NULL,
    FechaCreacion datetimeoffset NOT NULL, FechaActualizacion datetimeoffset NOT NULL, NumeroPresentacion int NOT NULL,
    FechaEnvio datetimeoffset NULL, EnviadoPor varchar(60) NULL, RevisorId varchar(60) NULL, FechaRevision datetimeoffset NULL,
    AprobadorId varchar(60) NULL, FechaAprobacion datetimeoffset NULL, Version int NOT NULL,
    CONSTRAINT UQ_Consolidado_Secuencia UNIQUE (Anio, Secuencia));
CREATE TABLE SecuenciasPlan (Anio int PRIMARY KEY, Ultimo int NOT NULL);   -- se incrementa con UPDLOCK en la misma transacción

CREATE TABLE Propuestas (
    Id varchar(20) PRIMARY KEY, ConsolidadoId varchar(20) NOT NULL REFERENCES Consolidados, Operacion varchar(10) NOT NULL,
    RubroId varchar(20) NOT NULL, SubRubroId varchar(20) NOT NULL, TipoDocumentoId varchar(20) NOT NULL, ClaseDocumentoId varchar(20) NOT NULL,
    Nombre nvarchar(200) NOT NULL, PlantillaId varchar(20) NULL REFERENCES Plantillas, VersionOrigenId varchar(20) NULL REFERENCES VersionesPlantilla,
    HuellaOrigen char(64) NULL, ContenidoPropuesto nvarchar(max) NOT NULL CHECK (ISJSON(ContenidoPropuesto) = 1),
    DerivadaDePropuestaId varchar(20) NULL REFERENCES Propuestas, Retirada bit NOT NULL, MotivoRetiro nvarchar(500) NULL,
    FechaCreacion datetimeoffset NOT NULL, FechaModificacion datetimeoffset NOT NULL, Version int NOT NULL);

-- D4 garantizada por el motor: una identidad solo puede estar en un consolidado abierto.
-- Se inserta al agregar la propuesta y se borra al retirarla, al aprobar o si el documento se descarta.
CREATE TABLE BloqueosPlantilla (
    RubroId varchar(20), SubRubroId varchar(20), TipoDocumentoId varchar(20), ClaseDocumentoId varchar(20),
    ConsolidadoId varchar(20) NOT NULL REFERENCES Consolidados, PropuestaId varchar(20) NOT NULL REFERENCES Propuestas,
    PRIMARY KEY (RubroId, SubRubroId, TipoDocumentoId, ClaseDocumentoId));

CREATE TABLE Presentaciones (
    Id varchar(20) PRIMARY KEY, ConsolidadoId varchar(20) NOT NULL REFERENCES Consolidados, Numero int NOT NULL,
    Usuario varchar(60) NOT NULL, Fecha datetimeoffset NOT NULL, CantidadPlantillas int NOT NULL, HuellaIntegridad char(64) NOT NULL,
    PropuestasEnviadas nvarchar(max) NOT NULL CHECK (ISJSON(PropuestasEnviadas) = 1),   -- copia congelada
    CONSTRAINT UQ_Presentacion UNIQUE (ConsolidadoId, Numero));
CREATE TABLE Devoluciones (
    Id varchar(20) PRIMARY KEY, ConsolidadoId varchar(20) NOT NULL REFERENCES Consolidados, NumeroPresentacion int NOT NULL,
    Usuario varchar(60) NOT NULL, Fecha datetimeoffset NOT NULL, Justificacion nvarchar(2000) NOT NULL);
CREATE TABLE Observaciones (
    Id int IDENTITY PRIMARY KEY, DevolucionId varchar(20) NOT NULL REFERENCES Devoluciones, PropuestaId varchar(20) NOT NULL REFERENCES Propuestas,
    BloqueCodigo varchar(40) NULL, Problema nvarchar(2000) NOT NULL, Recomendacion nvarchar(2000) NULL);

-- Bitácora e historial: solo se insertan (ver sección 5).
CREATE TABLE Movimientos (Id varchar(20) PRIMARY KEY, ConsolidadoId varchar(20) NOT NULL REFERENCES Consolidados, Tipo varchar(30) NOT NULL,
    Usuario varchar(60) NOT NULL, Fecha datetimeoffset NOT NULL, EstadoOrigen varchar(12) NULL, EstadoDestino varchar(12) NULL,
    NumeroPresentacion int NULL, Detalle nvarchar(1000) NOT NULL);
CREATE TABLE Historial (Id varchar(20) PRIMARY KEY, Fecha datetimeoffset NOT NULL, Usuario varchar(60) NOT NULL, Coleccion varchar(40) NOT NULL,
    EntidadId varchar(60) NOT NULL, Accion varchar(20) NOT NULL, VersionEntidad int NOT NULL, Anterior nvarchar(max) NULL, Nuevo nvarchar(max) NULL);
CREATE INDEX IX_Historial_Entidad ON Historial (Coleccion, EntidadId, Fecha);

CREATE TABLE Archivos (Id varchar(20) PRIMARY KEY, NombreOriginal nvarchar(150) NOT NULL, TipoContenido varchar(40) NOT NULL,
    Tamano int NOT NULL, Sha256 char(64) NOT NULL, FechaCarga datetimeoffset NOT NULL, Contenido varbinary(max) NOT NULL);
```

**Por qué el contenido va como JSON y no en tablas de párrafos:**
- **Inmutable:** una versión publicada es una fotografía que no cambia, y su huella se calcula sobre ella.
- **Igual a hoy:** se guarda y se lee completa, exactamente como hoy; el comparador, el PDF y la publicación no
  cambian.
- **Consultable:** si se necesitan búsquedas dentro del texto, se pueden agregar columnas calculadas o un índice de
  texto completo sin tocar el dominio.

Las tablas de **datos de prueba** (`Solicitudes`, `DetallesBienes`) y las de la colección anterior a la Fase 3
(`Parrafos`, contenido de `Plantillas`) no pasan a producción. Las solicitudes reales vendrán del sistema de origen.

## 4. Las reglas que pasan al motor

| Regla | En JSON (hoy) | En la base de datos |
|---|---|---|
| Número PLAN único y correlativo | Bloqueo de la colección en la transacción | `SecuenciasPlan` con `UPDLOCK, HOLDLOCK` y `UNIQUE (Anio, Secuencia)` |
| Una sola vigente por plantilla (regla 14) | Validación dentro de la transacción | Además, índice único filtrado `UX_Version_Vigente` |
| Una identidad por plantilla | Validación | `UQ_Plantilla_Identidad` |
| D4: una plantilla en un solo consolidado abierto | Validación con la colección bloqueada | Tabla `BloqueosPlantilla` con clave primaria por identidad |
| Publicación todo o nada (regla 16) | Diario y reversión | Una transacción con aislamiento `SERIALIZABLE` (o `SNAPSHOT` con reintento) alrededor de `PublicarAsync` |
| Concurrencia optimista | `Version` comparada en memoria | `Version` como token: `UPDATE … WHERE Id = @id AND Version = @v` |
| Historial de solo lectura (regla 15) | No hay operaciones de borrado | Además, `DENY UPDATE, DELETE` sobre `Historial`, `Movimientos`, `VersionesPlantilla` (salvo `Vigencia` y `FinVigencia`), `Presentaciones` y `Devoluciones` para el usuario de la aplicación |

Las validaciones del dominio se mantienen aunque el motor las repita: dan mensajes claros en español, y el motor es la
última barrera.

## 5. Auditoría en la base de datos

- `Historial` y `Movimientos` solo admiten `INSERT`, por permisos del usuario de la aplicación.
- Opcional: **tablas temporales** del sistema (`SYSTEM_VERSIONING`) en `Consolidados` y `Propuestas`, como segunda
  fuente de auditoría independiente de la aplicación.
- **Respaldo:** completo diario y de registro de transacciones cada 15 minutos (o lo que fije la institución).
- **Crecimiento del historial:** se particiona por año o se archiva en frío. La trazabilidad de la Fase 7 no depende
  del historial técnico: usa versiones, presentaciones y devoluciones.

## 6. Identidad y permisos

- **Autenticación:** `UsuarioPrueba` y el encabezado `X-Usuario-Prueba` desaparecen. La API usa `AddAuthentication`
  con el proveedor institucional, y `IUsuarioActual` toma el usuario del token.
- **Roles:** salen de grupos o *claims*. `ServicioAutorizacion` sigue exigiendo los mismos roles, y las reglas de
  bandeja (D6, D10) y de separación de funciones no cambian.
- **Pantalla:** el selector «Usuario de prueba» se reemplaza por el nombre del usuario y «Cerrar sesión».

## 7. Plan de migración

1. **Proyecto de infraestructura SQL** (`EditorResoluciones.Infraestructura.Sql`):
   - `DbContext` con las configuraciones de la sección 3;
   - `RepositorioEf<T>` y `UnidadDeTrabajoEf`;
   - migración inicial.
2. **Pruebas de contrato:** las mismas 254 pruebas del backend se ejecutan contra SQL Server en un contenedor
   (*Testcontainers*), con `Almacenamiento:Tipo = Sql`. La meta es que todas pasen sin tocar el dominio.
3. **Importación de datos** (herramienta de consola, idempotente):
   - lee los archivos JSON y los inserta respetando los identificadores;
   - verifica conteos y recalcula cada huella para comprobar que el contenido llegó idéntico.
4. **Paralelo y corte:**
   - en un ambiente de prueba se valida con usuarios reales;
   - el día del corte se congela la escritura, se importa, se verifica y se cambia la configuración;
   - los archivos JSON se conservan como respaldo de solo lectura.
5. **Retiro del JSON:** pasado un período de estabilidad, se quita `AgregarAlmacenamientoJson` de producción. Puede
   seguir disponible para demostraciones.

## 8. Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| Diferencias de comportamiento entre JSON y SQL | La misma batería de pruebas contra ambos almacenamientos |
| Bloqueos largos al aprobar documentos grandes | La publicación ya prepara todo en memoria; la transacción solo escribe. Si hace falta, se usa `SNAPSHOT` |
| JSON grande en `Contenido` | El límite actual es de 2000 caracteres por párrafo; un documento típico ocupa decenas de KB |
| Fechas y zona horaria | `datetimeoffset` en todas las fechas; el año del número PLAN sigue usando la zona institucional |
| Dependencia de la herramienta de importación | Es idempotente y verifica huellas; se puede repetir hasta que cuadre |

## 9. Resultado esperado

- La API, el dominio y las pantallas no cambian.
- Cambian la infraestructura (repositorios, transacciones y archivos), la autenticación y la configuración.
- Las reglas críticas (número PLAN, una vigente por plantilla, D4 e historial de solo lectura) quedan protegidas
  también por el motor.
