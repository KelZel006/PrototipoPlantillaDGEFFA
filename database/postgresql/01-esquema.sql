/* =====================================================================================================================
   Sistema de Gestión de Plantillas DGEFFA — Esquema para PostgreSQL (13 o superior)
   ---------------------------------------------------------------------------------------------------------------------
   Mismo modelo que database/sqlserver/01-esquema.sql, con la convención de PostgreSQL: nombres en minúsculas con
   guion bajo (version_plantilla.numero_version ↔ VersionesPlantilla.NumeroVersion).
   - Los identificadores son los mismos de los archivos JSON (por ejemplo, PLA-001, CON-0001).
   - Los valores de las enumeraciones se guardan como texto, igual que en el JSON ('EnRegistro', 'Vigente'…).
   - El contenido de una versión o propuesta (diseño, bloques y párrafos) se guarda en jsonb.
     Importante: la huella SHA-256 se calcula en la aplicación sobre su propia serialización, nunca sobre el texto que
     devuelve jsonb (jsonb reordena las claves).
   - La columna version es el control de concurrencia optimista de la aplicación.
   Se ejecuta sobre una base de datos vacía.
   ===================================================================================================================== */

-- Base de datos recomendada (ejecutar aparte, con un usuario administrador):
-- CREATE DATABASE plantillas_dgeffa ENCODING 'UTF8' LC_COLLATE 'es_HN.UTF-8' LC_CTYPE 'es_HN.UTF-8' TEMPLATE template0;

BEGIN;

CREATE SCHEMA IF NOT EXISTS plantillas;
SET search_path TO plantillas;

/* ---------------------------------------------------------------------------------------------------------------------
   1. Usuarios y roles
   En producción la identidad viene del proveedor institucional; esta tabla es el espejo que usan la bandeja (D10),
   la separación de funciones y la auditoría.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE usuarios (
    id              varchar(60)     NOT NULL,
    nombre_completo varchar(150)    NOT NULL,
    activo          boolean         NOT NULL DEFAULT true,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_usuarios PRIMARY KEY (id)
);

CREATE TABLE usuario_roles (
    usuario_id      varchar(60)     NOT NULL,
    rol             varchar(20)     NOT NULL,
    CONSTRAINT pk_usuario_roles PRIMARY KEY (usuario_id, rol),
    CONSTRAINT fk_usuario_roles_usuario FOREIGN KEY (usuario_id) REFERENCES usuarios (id),
    CONSTRAINT ck_usuario_roles_rol CHECK (rol IN ('Analista', 'OficialJuridico', 'Administrador'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   2. Catálogos
   --------------------------------------------------------------------------------------------------------------------- */
-- Unidades que elaboran sus propias plantillas: Exoneraciones, Dispensas, Registro Exonerados, Notas de Crédito y Devoluciones.
CREATE TABLE unidades (
    id              varchar(40)     NOT NULL,           -- por ejemplo exoneraciones
    nombre          varchar(150)    NOT NULL,
    orden           integer         NOT NULL,           -- posición en el menú
    activo          boolean         NOT NULL DEFAULT true,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_unidades PRIMARY KEY (id),
    CONSTRAINT uq_unidades_nombre UNIQUE (nombre),
    CONSTRAINT ck_unidades_orden CHECK (orden BETWEEN 1 AND 100)
);

CREATE TABLE rubros (
    id              varchar(20)     NOT NULL,           -- código institucional, por ejemplo 1000
    nombre          varchar(300)    NOT NULL,
    activo          boolean         NOT NULL DEFAULT true,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_rubros PRIMARY KEY (id)
);

CREATE TABLE sub_rubros (
    id              varchar(20)     NOT NULL,
    rubro_id        varchar(20)     NOT NULL,
    nombre          varchar(300)    NOT NULL,
    activo          boolean         NOT NULL DEFAULT true,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_sub_rubros PRIMARY KEY (id),
    CONSTRAINT fk_sub_rubros_rubro FOREIGN KEY (rubro_id) REFERENCES rubros (id),
    -- Permite exigir en otras tablas que el sub rubro pertenezca a su rubro.
    CONSTRAINT uq_sub_rubros_rubro_id UNIQUE (rubro_id, id)
);

-- Lista «Documento»: Original, Modificación, Aclaratoria.
CREATE TABLE clases_documento (
    id              varchar(20)     NOT NULL,
    nombre          varchar(150)    NOT NULL,
    activo          boolean         NOT NULL DEFAULT true,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_clases_documento PRIMARY KEY (id),
    CONSTRAINT uq_clases_documento_nombre UNIQUE (nombre)
);

-- Catálogo de bloques documentales: CABECERA, VISTOS, CONSIDERANDO, FUNDAMENTACION_LEGAL…
CREATE TABLE bloques_documentales (
    id              varchar(40)     NOT NULL,
    nombre          varchar(80)     NOT NULL,
    titulo          varchar(80),
    numeracion      varchar(15)     NOT NULL DEFAULT 'Ninguna',
    es_firmas       boolean         NOT NULL DEFAULT false,
    contexto_campos varchar(20)     NOT NULL,
    descripcion     varchar(300),
    activo          boolean         NOT NULL DEFAULT true,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_bloques_documentales PRIMARY KEY (id),
    CONSTRAINT ck_bloques_documentales_numeracion CHECK (numeracion IN ('Ninguna', 'Considerando', 'Ordinal')),
    CONSTRAINT ck_bloques_documentales_contexto CHECK (contexto_campos IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas'))
);

-- Bloque nuevo que recibe el contenido de uno anterior cuando un tipo documental cambia de formato
-- (por ejemplo, en el Dictamen, Dictamina reemplaza a Resuelve).
CREATE TABLE bloque_reemplazos (
    bloque_id           varchar(40)     NOT NULL,
    reemplaza_bloque_id varchar(40)     NOT NULL,
    CONSTRAINT pk_bloque_reemplazos PRIMARY KEY (bloque_id, reemplaza_bloque_id),
    CONSTRAINT fk_bloque_reemplazos_bloque FOREIGN KEY (bloque_id) REFERENCES bloques_documentales (id),
    CONSTRAINT fk_bloque_reemplazos_anterior FOREIGN KEY (reemplaza_bloque_id) REFERENCES bloques_documentales (id),
    CONSTRAINT ck_bloque_reemplazos_distinto CHECK (bloque_id <> reemplaza_bloque_id)
);

-- Resolución, Certificación, Dictamen, Constancia de Obligados Tributarios.
CREATE TABLE tipos_documento (
    id                          varchar(20)     NOT NULL,
    nombre                      varchar(150)    NOT NULL,
    texto_corrido               boolean         NOT NULL DEFAULT false,
    copia_de_tipo_documento_id  varchar(20),        -- la Certificación es copia de la Resolución (D3)
    activo                      boolean         NOT NULL DEFAULT true,
    version                     integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_tipos_documento PRIMARY KEY (id),
    CONSTRAINT uq_tipos_documento_nombre UNIQUE (nombre),
    CONSTRAINT fk_tipos_documento_copia_de FOREIGN KEY (copia_de_tipo_documento_id) REFERENCES tipos_documento (id),
    CONSTRAINT ck_tipos_documento_copia_distinta CHECK (copia_de_tipo_documento_id IS NULL OR copia_de_tipo_documento_id <> id)
);

-- Bloques de cada tipo documental, con su orden y si son obligatorios (D2).
CREATE TABLE tipo_documento_bloques (
    tipo_documento_id varchar(20)   NOT NULL,
    bloque_id       varchar(40)     NOT NULL,
    orden           integer         NOT NULL,
    obligatorio     boolean         NOT NULL DEFAULT false,
    CONSTRAINT pk_tipo_documento_bloques PRIMARY KEY (tipo_documento_id, bloque_id),
    CONSTRAINT fk_tipo_documento_bloques_tipo FOREIGN KEY (tipo_documento_id) REFERENCES tipos_documento (id),
    CONSTRAINT fk_tipo_documento_bloques_bloque FOREIGN KEY (bloque_id) REFERENCES bloques_documentales (id),
    CONSTRAINT uq_tipo_documento_bloques_orden UNIQUE (tipo_documento_id, orden),
    CONSTRAINT ck_tipo_documento_bloques_orden CHECK (orden >= 1)
);

-- Campos dinámicos. Su origen sale de una lista cerrada de colecciones y propiedades (la valida la aplicación).
CREATE TABLE campos (
    id                  varchar(60)     NOT NULL,
    etiqueta            varchar(100)    NOT NULL,
    categoria           varchar(100)    NOT NULL,
    tipo_dato           varchar(10)     NOT NULL,
    origen_coleccion    varchar(40)     NOT NULL,
    origen_propiedad    varchar(60)     NOT NULL,
    descripcion         varchar(300),
    alertar_cambios     boolean         NOT NULL DEFAULT false,     -- avisar al aprobador (Operador)
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_campos PRIMARY KEY (id),
    CONSTRAINT ck_campos_tipo_dato CHECK (tipo_dato IN ('Texto', 'Numero', 'Moneda', 'Fecha', 'Booleano'))
);

-- Bloques (contextos) en los que se puede usar cada campo.
CREATE TABLE campo_contextos (
    campo_id        varchar(60)     NOT NULL,
    contexto        varchar(20)     NOT NULL,
    CONSTRAINT pk_campo_contextos PRIMARY KEY (campo_id, contexto),
    CONSTRAINT fk_campo_contextos_campo FOREIGN KEY (campo_id) REFERENCES campos (id) ON DELETE CASCADE,
    CONSTRAINT ck_campo_contextos_contexto CHECK (contexto IN ('cabecera', 'considerando', 'portanto', 'resuelve', 'firmas', 'tabla'))
);

/* ---------------------------------------------------------------------------------------------------------------------
   3. Archivos (logos). Hasta 1 MB, PNG o JPEG (lo valida la aplicación por el contenido real).
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE archivos (
    id              varchar(40)     NOT NULL,
    nombre_original varchar(150)    NOT NULL,
    tipo_contenido  varchar(40)     NOT NULL,
    tamano          integer         NOT NULL,
    sha256          char(64)        NOT NULL,
    fecha_carga     timestamptz     NOT NULL,
    contenido       bytea           NOT NULL,
    version         integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_archivos PRIMARY KEY (id),
    CONSTRAINT ck_archivos_tipo CHECK (tipo_contenido IN ('image/png', 'image/jpeg')),
    CONSTRAINT ck_archivos_tamano CHECK (tamano BETWEEN 1 AND 1048576)
);

/* ---------------------------------------------------------------------------------------------------------------------
   4. Plantillas (identidad) y sus versiones publicadas
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE plantillas (
    id                  varchar(40)     NOT NULL,
    nombre              varchar(200)    NOT NULL,
    unidad_id           varchar(40)     NOT NULL,
    rubro_id            varchar(20)     NOT NULL,
    sub_rubro_id        varchar(20)     NOT NULL,
    tipo_documento_id   varchar(20)     NOT NULL,
    clase_documento_id  varchar(20)     NOT NULL,
    fecha_modificacion  timestamptz     NOT NULL,
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_plantillas PRIMARY KEY (id),
    CONSTRAINT fk_plantillas_unidad FOREIGN KEY (unidad_id) REFERENCES unidades (id),
    CONSTRAINT fk_plantillas_sub_rubro FOREIGN KEY (rubro_id, sub_rubro_id) REFERENCES sub_rubros (rubro_id, id),
    CONSTRAINT fk_plantillas_tipo FOREIGN KEY (tipo_documento_id) REFERENCES tipos_documento (id),
    CONSTRAINT fk_plantillas_clase FOREIGN KEY (clase_documento_id) REFERENCES clases_documento (id),
    -- Identidad funcional: Unidad + Rubro + Sub Rubro + Tipo Documento + Documento identifican una única plantilla.
    CONSTRAINT uq_plantillas_identidad UNIQUE (unidad_id, rubro_id, sub_rubro_id, tipo_documento_id, clase_documento_id)
);

CREATE TABLE versiones_plantilla (
    id                  varchar(40)     NOT NULL,
    plantilla_id        varchar(40)     NOT NULL,
    numero_version      integer         NOT NULL,
    vigencia            varchar(10)     NOT NULL,
    contenido           jsonb           NOT NULL,       -- diseño, bloques y párrafos (inmutable)
    huella              char(64)        NOT NULL,       -- SHA-256 del contenido
    version_anterior_id varchar(40),
    consolidado_id      varchar(40),                    -- documento que la originó; nulo en la carga inicial
    carga_inicial       boolean         NOT NULL DEFAULT false,
    publicada_por       varchar(60)     NOT NULL,
    fecha_publicacion   timestamptz     NOT NULL,
    inicio_vigencia     timestamptz     NOT NULL,
    fin_vigencia        timestamptz,
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_versiones_plantilla PRIMARY KEY (id),
    CONSTRAINT fk_versiones_plantilla_plantilla FOREIGN KEY (plantilla_id) REFERENCES plantillas (id),
    CONSTRAINT fk_versiones_plantilla_anterior FOREIGN KEY (version_anterior_id) REFERENCES versiones_plantilla (id),
    CONSTRAINT uq_versiones_plantilla_numero UNIQUE (plantilla_id, numero_version),
    CONSTRAINT ck_versiones_plantilla_vigencia CHECK (vigencia IN ('Vigente', 'NoVigente')),
    CONSTRAINT ck_versiones_plantilla_contenido CHECK (jsonb_typeof(contenido) = 'object'),
    CONSTRAINT ck_versiones_plantilla_numero CHECK (numero_version >= 1),
    -- Una versión vigente no tiene fin de vigencia; una no vigente sí.
    CONSTRAINT ck_versiones_plantilla_fin CHECK ((vigencia = 'Vigente' AND fin_vigencia IS NULL) OR (vigencia = 'NoVigente' AND fin_vigencia IS NOT NULL)),
    CONSTRAINT ck_versiones_plantilla_origen CHECK (carga_inicial OR consolidado_id IS NOT NULL)
);

-- Regla 14: como máximo una versión vigente por plantilla, garantizado por el motor.
CREATE UNIQUE INDEX ux_versiones_plantilla_vigente ON versiones_plantilla (plantilla_id) WHERE vigencia = 'Vigente';

/* ---------------------------------------------------------------------------------------------------------------------
   5. Documento Consolidado (PLAN-AAAANNNN)
   --------------------------------------------------------------------------------------------------------------------- */
-- Último número usado por año. Se incrementa con SELECT … FOR UPDATE (o INSERT … ON CONFLICT DO UPDATE … RETURNING)
-- dentro de la transacción que crea el documento.
CREATE TABLE secuencias_plan (
    anio            integer         NOT NULL,
    ultimo          integer         NOT NULL DEFAULT 0,
    CONSTRAINT pk_secuencias_plan PRIMARY KEY (anio),
    CONSTRAINT ck_secuencias_plan_ultimo CHECK (ultimo BETWEEN 0 AND 9999)
);

CREATE TABLE consolidados (
    id                  varchar(40)     NOT NULL,
    numero              char(13)        NOT NULL,
    anio                integer         NOT NULL,
    secuencia           integer         NOT NULL,
    estado              varchar(12)     NOT NULL DEFAULT 'EnRegistro',
    unidad_id           varchar(40)     NOT NULL,       -- solo contiene plantillas de su unidad
    analista_id         varchar(60)     NOT NULL,
    fecha_creacion      timestamptz     NOT NULL,
    fecha_actualizacion timestamptz     NOT NULL,
    numero_presentacion integer         NOT NULL DEFAULT 0,
    fecha_envio         timestamptz,
    enviado_por         varchar(60),
    revisor_id          varchar(60),
    fecha_revision      timestamptz,
    aprobador_id        varchar(60),
    fecha_aprobacion    timestamptz,
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_consolidados PRIMARY KEY (id),
    CONSTRAINT uq_consolidados_numero UNIQUE (numero),
    CONSTRAINT uq_consolidados_secuencia UNIQUE (anio, secuencia),
    CONSTRAINT fk_consolidados_unidad FOREIGN KEY (unidad_id) REFERENCES unidades (id),
    CONSTRAINT fk_consolidados_analista FOREIGN KEY (analista_id) REFERENCES usuarios (id),
    CONSTRAINT fk_consolidados_enviado_por FOREIGN KEY (enviado_por) REFERENCES usuarios (id),
    CONSTRAINT fk_consolidados_revisor FOREIGN KEY (revisor_id) REFERENCES usuarios (id),
    CONSTRAINT fk_consolidados_aprobador FOREIGN KEY (aprobador_id) REFERENCES usuarios (id),
    CONSTRAINT ck_consolidados_estado CHECK (estado IN ('EnRegistro', 'Verificado', 'Aprobado')),
    CONSTRAINT ck_consolidados_secuencia CHECK (secuencia BETWEEN 1 AND 9999),
    CONSTRAINT ck_consolidados_anio CHECK (anio BETWEEN 2000 AND 2999),
    -- El número se deriva del año y la secuencia: PLAN-20260001.
    CONSTRAINT ck_consolidados_numero CHECK (numero = 'PLAN-' || anio::text || lpad(secuencia::text, 4, '0')),
    -- Lo aprobado tiene aprobador y fecha, y nunca lo aprueba quien lo preparó (separación de funciones).
    CONSTRAINT ck_consolidados_aprobacion CHECK (estado <> 'Aprobado' OR (aprobador_id IS NOT NULL AND fecha_aprobacion IS NOT NULL)),
    CONSTRAINT ck_consolidados_separacion CHECK (aprobador_id IS NULL OR (aprobador_id <> analista_id AND (enviado_por IS NULL OR aprobador_id <> enviado_por)))
);

CREATE INDEX ix_consolidados_bandeja ON consolidados (unidad_id, estado, analista_id) INCLUDE (numero, fecha_actualizacion);

CREATE TABLE propuestas (
    id                          varchar(40)     NOT NULL,
    consolidado_id              varchar(40)     NOT NULL,
    operacion                   varchar(10)     NOT NULL,
    unidad_id                   varchar(40)     NOT NULL,
    rubro_id                    varchar(20)     NOT NULL,
    sub_rubro_id                varchar(20)     NOT NULL,
    tipo_documento_id           varchar(20)     NOT NULL,
    clase_documento_id          varchar(20)     NOT NULL,
    nombre                      varchar(200)    NOT NULL,
    plantilla_id                varchar(40),        -- obligatorio en MODIFICAR
    version_origen_id           varchar(40),        -- versión vigente de la que partió (MODIFICAR)
    huella_origen               char(64),
    contenido_propuesto         jsonb           NOT NULL,
    derivada_de_propuesta_id    varchar(40),        -- Certificación derivada de su Resolución (D3)
    retirada                    boolean         NOT NULL DEFAULT false,
    motivo_retiro               varchar(500),
    fecha_creacion              timestamptz     NOT NULL,
    fecha_modificacion          timestamptz     NOT NULL,
    version                     integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_propuestas PRIMARY KEY (id),
    CONSTRAINT fk_propuestas_consolidado FOREIGN KEY (consolidado_id) REFERENCES consolidados (id),
    CONSTRAINT fk_propuestas_unidad FOREIGN KEY (unidad_id) REFERENCES unidades (id),
    CONSTRAINT fk_propuestas_sub_rubro FOREIGN KEY (rubro_id, sub_rubro_id) REFERENCES sub_rubros (rubro_id, id),
    CONSTRAINT fk_propuestas_tipo FOREIGN KEY (tipo_documento_id) REFERENCES tipos_documento (id),
    CONSTRAINT fk_propuestas_clase FOREIGN KEY (clase_documento_id) REFERENCES clases_documento (id),
    CONSTRAINT fk_propuestas_plantilla FOREIGN KEY (plantilla_id) REFERENCES plantillas (id),
    CONSTRAINT fk_propuestas_version_origen FOREIGN KEY (version_origen_id) REFERENCES versiones_plantilla (id),
    CONSTRAINT fk_propuestas_derivada FOREIGN KEY (derivada_de_propuesta_id) REFERENCES propuestas (id),
    CONSTRAINT ck_propuestas_operacion CHECK (operacion IN ('Crear', 'Modificar')),
    CONSTRAINT ck_propuestas_contenido CHECK (jsonb_typeof(contenido_propuesto) = 'object'),
    CONSTRAINT ck_propuestas_modificar CHECK (operacion <> 'Modificar' OR (plantilla_id IS NOT NULL AND version_origen_id IS NOT NULL)),
    CONSTRAINT ck_propuestas_retiro CHECK (NOT retirada OR (motivo_retiro IS NOT NULL AND length(motivo_retiro) >= 5))
);

CREATE INDEX ix_propuestas_consolidado ON propuestas (consolidado_id);
CREATE INDEX ix_propuestas_plantilla ON propuestas (plantilla_id) WHERE plantilla_id IS NOT NULL;

ALTER TABLE versiones_plantilla
    ADD CONSTRAINT fk_versiones_plantilla_consolidado FOREIGN KEY (consolidado_id) REFERENCES consolidados (id);

-- D4: una plantilla (por su identidad funcional) solo puede estar en un documento abierto. La aplicación inserta la
-- fila al agregar la propuesta y la borra al retirarla o al aprobar el documento.
CREATE TABLE bloqueos_plantilla (
    unidad_id           varchar(40)     NOT NULL,
    rubro_id            varchar(20)     NOT NULL,
    sub_rubro_id        varchar(20)     NOT NULL,
    tipo_documento_id   varchar(20)     NOT NULL,
    clase_documento_id  varchar(20)     NOT NULL,
    consolidado_id      varchar(40)     NOT NULL,
    propuesta_id        varchar(40)     NOT NULL,
    fecha               timestamptz     NOT NULL,
    CONSTRAINT pk_bloqueos_plantilla PRIMARY KEY (unidad_id, rubro_id, sub_rubro_id, tipo_documento_id, clase_documento_id),
    CONSTRAINT fk_bloqueos_plantilla_consolidado FOREIGN KEY (consolidado_id) REFERENCES consolidados (id),
    CONSTRAINT fk_bloqueos_plantilla_propuesta FOREIGN KEY (propuesta_id) REFERENCES propuestas (id),
    CONSTRAINT uq_bloqueos_plantilla_propuesta UNIQUE (propuesta_id)
);

-- Cada envío a verificación, con la copia congelada de lo enviado y su huella de integridad.
CREATE TABLE presentaciones (
    id                  varchar(40)     NOT NULL,
    consolidado_id      varchar(40)     NOT NULL,
    numero              integer         NOT NULL,
    usuario             varchar(60)     NOT NULL,
    fecha               timestamptz     NOT NULL,
    cantidad_plantillas integer         NOT NULL,
    huella_integridad   char(64)        NOT NULL,
    propuestas          jsonb           NOT NULL,       -- arreglo de las propuestas tal como se enviaron
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_presentaciones PRIMARY KEY (id),
    CONSTRAINT fk_presentaciones_consolidado FOREIGN KEY (consolidado_id) REFERENCES consolidados (id),
    CONSTRAINT fk_presentaciones_usuario FOREIGN KEY (usuario) REFERENCES usuarios (id),
    CONSTRAINT uq_presentaciones_numero UNIQUE (consolidado_id, numero),
    CONSTRAINT ck_presentaciones_numero CHECK (numero >= 1),
    CONSTRAINT ck_presentaciones_cantidad CHECK (cantidad_plantillas >= 1),
    CONSTRAINT ck_presentaciones_propuestas CHECK (jsonb_typeof(propuestas) = 'array')
);

CREATE TABLE devoluciones (
    id                  varchar(40)     NOT NULL,
    consolidado_id      varchar(40)     NOT NULL,
    numero_presentacion integer         NOT NULL,
    usuario             varchar(60)     NOT NULL,
    fecha               timestamptz     NOT NULL,
    justificacion       varchar(2000)   NOT NULL,
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_devoluciones PRIMARY KEY (id),
    CONSTRAINT fk_devoluciones_presentacion FOREIGN KEY (consolidado_id, numero_presentacion) REFERENCES presentaciones (consolidado_id, numero),
    CONSTRAINT fk_devoluciones_usuario FOREIGN KEY (usuario) REFERENCES usuarios (id),
    CONSTRAINT uq_devoluciones_presentacion UNIQUE (consolidado_id, numero_presentacion),
    CONSTRAINT ck_devoluciones_justificacion CHECK (length(btrim(justificacion)) > 0)
);

CREATE TABLE observaciones (
    id                  integer GENERATED ALWAYS AS IDENTITY,
    devolucion_id       varchar(40)     NOT NULL,
    orden               integer         NOT NULL,
    propuesta_id        varchar(40)     NOT NULL,
    bloque_codigo       varchar(40),
    parrafo_id          varchar(40),
    problema            varchar(2000)   NOT NULL,
    recomendacion       varchar(2000),
    CONSTRAINT pk_observaciones PRIMARY KEY (id),
    CONSTRAINT fk_observaciones_devolucion FOREIGN KEY (devolucion_id) REFERENCES devoluciones (id),
    CONSTRAINT fk_observaciones_propuesta FOREIGN KEY (propuesta_id) REFERENCES propuestas (id),
    CONSTRAINT uq_observaciones_orden UNIQUE (devolucion_id, orden)
);

/* ---------------------------------------------------------------------------------------------------------------------
   6. Auditoría: bitácora del documento e historial técnico (solo se insertan)
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE movimientos (
    id                  varchar(40)     NOT NULL,
    consolidado_id      varchar(40)     NOT NULL,
    tipo                varchar(30)     NOT NULL,
    usuario             varchar(60)     NOT NULL,
    fecha               timestamptz     NOT NULL,
    estado_origen       varchar(12),
    estado_destino      varchar(12),
    numero_presentacion integer,
    detalle             varchar(1000)   NOT NULL,
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_movimientos PRIMARY KEY (id),
    CONSTRAINT fk_movimientos_consolidado FOREIGN KEY (consolidado_id) REFERENCES consolidados (id),
    CONSTRAINT ck_movimientos_tipo CHECK (tipo IN ('Creacion', 'PropuestaAgregada', 'PropuestaModificada', 'PropuestaRetirada',
        'EnvioVerificacion', 'Devolucion', 'Aprobacion', 'Publicacion', 'CambioVigencia', 'ErrorPublicacion', 'Reasignacion')),
    CONSTRAINT ck_movimientos_estado_origen CHECK (estado_origen IS NULL OR estado_origen IN ('EnRegistro', 'Verificado', 'Aprobado')),
    CONSTRAINT ck_movimientos_estado_destino CHECK (estado_destino IS NULL OR estado_destino IN ('EnRegistro', 'Verificado', 'Aprobado'))
);

CREATE INDEX ix_movimientos_consolidado ON movimientos (consolidado_id, fecha);

CREATE TABLE historial (
    id                  varchar(40)     NOT NULL,
    fecha               timestamptz     NOT NULL,
    usuario             varchar(60)     NOT NULL,
    coleccion           varchar(40)     NOT NULL,
    entidad_id          varchar(60)     NOT NULL,
    accion              varchar(15)     NOT NULL,
    version_entidad     integer         NOT NULL,
    anterior            jsonb,                          -- estado anterior
    nuevo               jsonb,                          -- estado nuevo
    CONSTRAINT pk_historial PRIMARY KEY (id),
    CONSTRAINT ck_historial_accion CHECK (accion IN ('Creacion', 'Actualizacion', 'Eliminacion'))
);

CREATE INDEX ix_historial_entidad ON historial (coleccion, entidad_id, fecha);
CREATE INDEX ix_historial_fecha ON historial (fecha);

/* ---------------------------------------------------------------------------------------------------------------------
   7. Datos de origen de las solicitudes (para sustituir los campos al generar el documento)
   En producción pueden ser vistas o una réplica del sistema de origen; en pruebas se cargan datos ficticios.
   --------------------------------------------------------------------------------------------------------------------- */
CREATE TABLE solicitudes (
    id                  varchar(40)     NOT NULL,
    numero_solicitud    varchar(50)     NOT NULL,
    operador            varchar(200),
    beneficiario        varchar(200),
    rubro_id            varchar(20)     NOT NULL,
    sub_rubro_id        varchar(20)     NOT NULL,
    tipo_beneficio      varchar(150),
    estado              varchar(50),
    fecha_resolucion    date,
    anio_gestion        integer,
    monto_exoneracion   numeric(18, 2),
    unidad_ejecutora    varchar(150),
    departamento        varchar(100),
    municipio           varchar(100),
    es_dato_prueba      boolean         NOT NULL DEFAULT false,
    version             integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_solicitudes PRIMARY KEY (id),
    CONSTRAINT uq_solicitudes_numero UNIQUE (numero_solicitud),
    CONSTRAINT fk_solicitudes_sub_rubro FOREIGN KEY (rubro_id, sub_rubro_id) REFERENCES sub_rubros (rubro_id, id),
    CONSTRAINT ck_solicitudes_anio CHECK (anio_gestion IS NULL OR anio_gestion BETWEEN 2000 AND 2100),
    CONSTRAINT ck_solicitudes_monto CHECK (monto_exoneracion IS NULL OR monto_exoneracion >= 0)
);

CREATE TABLE detalles_bienes (
    id                      varchar(40)     NOT NULL,
    solicitud_id            varchar(40)     NOT NULL,
    codigo_arancelario      varchar(20)     NOT NULL,
    descripcion             varchar(300)    NOT NULL,
    unidad_medida           varchar(50),
    cantidad                numeric(18, 4)  NOT NULL,
    valor_estimado          numeric(18, 2),
    descripcion_arancelaria varchar(300),
    monto_autorizado        numeric(18, 2),
    es_dato_prueba          boolean         NOT NULL DEFAULT false,
    version                 integer         NOT NULL DEFAULT 1,
    CONSTRAINT pk_detalles_bienes PRIMARY KEY (id),
    CONSTRAINT fk_detalles_bienes_solicitud FOREIGN KEY (solicitud_id) REFERENCES solicitudes (id),
    CONSTRAINT ck_detalles_bienes_cantidad CHECK (cantidad >= 0),
    CONSTRAINT ck_detalles_bienes_valor CHECK (valor_estimado IS NULL OR valor_estimado >= 0),
    CONSTRAINT ck_detalles_bienes_monto CHECK (monto_autorizado IS NULL OR monto_autorizado >= 0)
);

CREATE INDEX ix_detalles_bienes_solicitud ON detalles_bienes (solicitud_id);

/* ---------------------------------------------------------------------------------------------------------------------
   8. Protección de la auditoría (regla 15: nada se borra ni se reescribe)
   --------------------------------------------------------------------------------------------------------------------- */

-- Historial, bitácora, presentaciones, devoluciones y observaciones: solo se insertan.
CREATE OR REPLACE FUNCTION fn_solo_insercion() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION '% : los registros de auditoría no se modifican ni se eliminan.', TG_TABLE_NAME
        USING ERRCODE = 'P0015';
END;
$$;

CREATE TRIGGER tr_solo_insercion BEFORE UPDATE OR DELETE ON historial FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();
CREATE TRIGGER tr_solo_insercion BEFORE UPDATE OR DELETE ON movimientos FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();
CREATE TRIGGER tr_solo_insercion BEFORE UPDATE OR DELETE ON presentaciones FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();
CREATE TRIGGER tr_solo_insercion BEFORE UPDATE OR DELETE ON devoluciones FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();
CREATE TRIGGER tr_solo_insercion BEFORE UPDATE OR DELETE ON observaciones FOR EACH ROW EXECUTE FUNCTION fn_solo_insercion();

-- Una versión publicada no se elimina, y de ella solo cambian la vigencia, el fin de vigencia y la versión de registro.
CREATE OR REPLACE FUNCTION fn_versiones_plantilla_inmutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Las versiones publicadas no se eliminan.' USING ERRCODE = 'P0015';
    END IF;
    IF NEW.plantilla_id IS DISTINCT FROM OLD.plantilla_id
       OR NEW.numero_version IS DISTINCT FROM OLD.numero_version
       OR NEW.contenido IS DISTINCT FROM OLD.contenido
       OR NEW.huella IS DISTINCT FROM OLD.huella
       OR NEW.version_anterior_id IS DISTINCT FROM OLD.version_anterior_id
       OR NEW.consolidado_id IS DISTINCT FROM OLD.consolidado_id
       OR NEW.carga_inicial IS DISTINCT FROM OLD.carga_inicial
       OR NEW.publicada_por IS DISTINCT FROM OLD.publicada_por
       OR NEW.fecha_publicacion IS DISTINCT FROM OLD.fecha_publicacion
       OR NEW.inicio_vigencia IS DISTINCT FROM OLD.inicio_vigencia
       OR (OLD.vigencia = 'NoVigente' AND NEW.vigencia = 'Vigente') THEN
        RAISE EXCEPTION 'Una versión publicada es inmutable: solo puede pasar de VIGENTE a NO VIGENTE.' USING ERRCODE = 'P0015';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_versiones_plantilla_inmutable BEFORE UPDATE OR DELETE ON versiones_plantilla
    FOR EACH ROW EXECUTE FUNCTION fn_versiones_plantilla_inmutable();

-- Un documento aprobado no cambia de estado ni se elimina; un documento solo se elimina si nunca se envió.
CREATE OR REPLACE FUNCTION fn_consolidados_aprobado() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.estado = 'Aprobado' AND (TG_OP = 'DELETE' OR NEW.estado <> 'Aprobado') THEN
        RAISE EXCEPTION 'Un Documento Consolidado aprobado no cambia de estado ni se elimina.' USING ERRCODE = 'P0017';
    END IF;
    IF TG_OP = 'DELETE' THEN
        IF OLD.numero_presentacion > 0 THEN
            RAISE EXCEPTION 'Un Documento Consolidado que ya se envió no se elimina.' USING ERRCODE = 'P0017';
        END IF;
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_consolidados_aprobado BEFORE UPDATE OR DELETE ON consolidados
    FOR EACH ROW EXECUTE FUNCTION fn_consolidados_aprobado();

/* ---------------------------------------------------------------------------------------------------------------------
   9. Permisos del usuario de la aplicación (ajustar el nombre del usuario o rol institucional)
   --------------------------------------------------------------------------------------------------------------------- */
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rol_editor_plantillas') THEN
        CREATE ROLE rol_editor_plantillas NOLOGIN;
    END IF;
END;
$$;

GRANT USAGE ON SCHEMA plantillas TO rol_editor_plantillas;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plantillas TO rol_editor_plantillas;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA plantillas TO rol_editor_plantillas;
REVOKE UPDATE, DELETE ON historial, movimientos, presentaciones, devoluciones, observaciones FROM rol_editor_plantillas;
REVOKE DELETE ON versiones_plantilla FROM rol_editor_plantillas;
-- GRANT rol_editor_plantillas TO usuario_de_la_aplicacion;

COMMIT;
