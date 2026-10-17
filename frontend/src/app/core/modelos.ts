/** Modelos que intercambia el frontend con la API REST (mismos nombres que el backend, en camelCase). */

export interface Entidad {
  id: string;
  version: number;
}

/** Registro nuevo: el servidor asigna el identificador, la versión y las fechas. */
export type Nuevo<T extends Entidad> = Omit<T, 'id' | 'version' | 'fechaModificacion'> & Partial<Entidad>;

export interface Rubro extends Entidad {
  nombre: string;
  activo: boolean;
}

export interface SubRubro extends Entidad {
  rubroId: string;
  nombre: string;
  activo: boolean;
}

export interface TipoDocumento extends Entidad {
  nombre: string;
  /** El documento se genera con el texto corrido (por ejemplo, la Certificación). */
  textoCorrido?: boolean;
  /**
   * Si se indica, las plantillas de este tipo acompañan a las de ese tipo (la Certificación, a la Resolución): en un
   * Documento Consolidado, la propuesta de la Resolución lleva consigo la de su Certificación.
   */
  copiaDeTipoDocumentoId?: string | null;
  activo: boolean;
}

/** Lista "Documento": Original, Modificación o Aclaratoria. */
export interface ClaseDocumento extends Entidad {
  nombre: string;
  activo: boolean;
}

export type AlineacionLogo = 'Izquierda' | 'Centro' | 'Derecha';

/**
 * Plantilla que se edita: la propuesta de un Documento Consolidado (desde la Fase 3 las plantillas vigentes no se
 * editan directamente). El identificador es el de la propuesta.
 */
export interface PlantillaEdicion extends Entidad {
  nombre: string;
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  claseDocumentoId: string;
  /** Logo del encabezado del documento; null si no tiene. */
  logoId?: string | null;
  alineacionLogo?: AlineacionLogo;
  /** Texto del pie de página del documento (varias líneas). */
  piePagina?: string | null;
  /** Espacio en centímetros antes de cada bloque de firma. */
  espacioFirmasCm?: number;
  fechaModificacion: string;
  operacion: OperacionPropuesta;
  consolidadoId: string;
  consolidadoNumero: string;
  estadoConsolidado: EstadoConsolidado;
  /** El usuario actual es el analista dueño y el consolidado está EN REGISTRO. */
  editable: boolean;
  retirada: boolean;
  /** Si es la Certificación que acompaña a una Resolución, la propuesta de esa Resolución. */
  derivadaDePropuestaId?: string | null;
  /** Plantilla vigente que se modifica; null en una creación. */
  plantillaId?: string | null;
  /** Bloques del documento, en el orden del tipo documental: son las pestañas del editor. */
  bloques: BloqueConfigurado[];
  /** Nombres de los bloques obligatorios que todavía no tienen párrafos activos. */
  obligatoriosVacios: string[];
}

/**
 * Código del bloque del documento (CABECERA, VISTOS, CONSIDERANDO, POR_TANTO…). Los bloques de cada tipo documental
 * los define el catálogo (decisión D2).
 */
export type Seccion = string;

export type NumeracionBloque = 'Ninguna' | 'Considerando' | 'Ordinal';

/** Bloque que usa un tipo documental, con los datos del catálogo. */
export interface BloqueConfigurado {
  codigo: Seccion;
  nombre: string;
  titulo?: string | null;
  numeracion: NumeracionBloque;
  esFirmas: boolean;
  /** Contexto de los campos que se pueden insertar (por ejemplo, «considerando»). */
  contextoCampos: string;
  obligatorio: boolean;
  orden: number;
}

/** Nodo del documento del editor (formato de TipTap/ProseMirror). */
export interface NodoContenido {
  type: string;
  text?: string;
  attrs?: Record<string, unknown>;
  marks?: { type: string; attrs?: Record<string, unknown> }[];
  content?: NodoContenido[];
}

/** Párrafo de una propuesta. Su versión es la del párrafo dentro de la propuesta. */
export interface Parrafo extends Entidad {
  propuestaId: string;
  /** Código del bloque al que pertenece. */
  bloque: Seccion;
  /** Posición en la sección. Con 0, el servidor lo coloca al final. */
  orden: number;
  contenido: NodoContenido;
  activo: boolean;
  /** Con condición, el párrafo solo se muestra si se cumple. Sin condición se muestra siempre. */
  mostrarSoloSiCumple: boolean;
  condicion?: GrupoCondiciones | null;
  /** Lo calcula el servidor a partir del contenido. */
  camposUsados: string[];
}

export type OperadorLogico = 'Y' | 'O';

export type OperadorComparacion =
  | 'IgualA'
  | 'DistintoDe'
  | 'Contiene'
  | 'NoContiene'
  | 'EsNulo'
  | 'NoEsNulo'
  | 'MayorQue'
  | 'MenorQue'
  | 'MayorOIgualQue'
  | 'MenorOIgualQue';

export interface ReglaCondicion {
  campoId: string;
  operador: OperadorComparacion;
  valor?: string | null;
}

export interface GrupoCondiciones {
  operadorLogico: OperadorLogico;
  reglas: ReglaCondicion[];
  grupos: GrupoCondiciones[];
}

/** Operadores permitidos por tipo de dato (los define el servidor). */
export type OperadoresPorTipo = Record<TipoDato, OperadorComparacion[]>;

export type TipoDato = 'Texto' | 'Numero' | 'Moneda' | 'Fecha' | 'Booleano';

export interface Campo extends Entidad {
  etiqueta: string;
  categoria: string;
  tipoDato: TipoDato;
  origen: { coleccion: string; propiedad: string };
  contextos: string[];
  descripcion?: string;
}

/** Solicitud ficticia con la que se prueba la generación del documento. */
export interface Solicitud extends Entidad {
  numeroSolicitud: string;
  operador?: string;
  beneficiario?: string;
  rubroId: string;
  subRubroId: string;
  estado?: string;
  esDatoPrueba: boolean;
}

/**
 * Un campo de lista tiene un valor por registro (por ejemplo, por cada bien de la solicitud). Solo se usa dentro
 * de una tabla: su fila se repite una vez por registro al generar el documento.
 */
export function esCampoLista(campo: Pick<Campo, 'origen'>): boolean {
  return campo.origen.coleccion === 'detallesBienes';
}

/** Interlineados que se pueden elegir al generar el PDF (los acepta el servidor). */
export type Interlineado = string;

/** De 0.8 a 2.0, de 0.1 en 0.1 (los mismos que acepta el servidor). */
export const INTERLINEADOS: readonly { valor: Interlineado; nombre: string }[] = Array.from({ length: 13 }, (_, i) => {
  const valor = ((8 + i) / 10).toFixed(1);
  const nombre = valor === '1.0' ? '1.0 (sencillo)' : valor === '2.0' ? '2.0 (doble)' : valor;
  return { valor, nombre };
});

/** Límites del espacio entre firmas, en centímetros (los mismos que valida el servidor). */
export const ESPACIO_FIRMAS = { minimo: 0, maximo: 10, predeterminado: 1.2 } as const;

/** Registro de la lista de bienes de una solicitud (alimenta las filas de las tablas). */
export interface DetalleBien extends Entidad {
  solicitudId: string;
  codigoArancelario: string;
  descripcionArancelaria?: string | null;
  montoAutorizado?: number | null;
}

export interface EstadoColeccion {
  nombre: string;
  disponible: boolean;
  registros: number;
  error?: string;
}

export interface Salud {
  estado: 'correcto' | 'con errores';
  almacenamiento: string;
  colecciones: EstadoColeccion[];
}

/** Respuesta de error estándar de la API (ProblemDetails). */
export interface DetalleProblema {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

// ---------- Documento Consolidado ----------

export type Rol = 'Analista' | 'OficialJuridico' | 'Administrador';

/** Usuario de prueba con sus roles (solo simula el flujo: no es autenticación). */
export interface Usuario extends Entidad {
  nombreCompleto: string;
  roles: Rol[];
  activo: boolean;
}

export type EstadoConsolidado = 'EnRegistro' | 'Verificado' | 'Aprobado';

export type OperacionPropuesta = 'Crear' | 'Modificar';

export type Vigencia = 'Vigente' | 'NoVigente';

export interface DocumentoConsolidado extends Entidad {
  numero: string;
  anio: number;
  secuencia: number;
  estado: EstadoConsolidado;
  analistaId: string;
  fechaCreacion: string;
  fechaActualizacion: string;
  numeroPresentacion: number;
  fechaEnvio?: string | null;
  enviadoPor?: string | null;
  revisorId?: string | null;
  fechaRevision?: string | null;
  aprobadorId?: string | null;
  fechaAprobacion?: string | null;
}

/** Fila del listado de consolidados (pantalla A). */
export interface ResumenConsolidado {
  id: string;
  numero: string;
  anio: number;
  estado: EstadoConsolidado;
  analistaId: string;
  fechaCreacion: string;
  fechaActualizacion: string;
  tiposDocumento: string[];
  totalPlantillas: number;
}

export interface Propuesta extends Entidad {
  consolidadoId: string;
  operacion: OperacionPropuesta;
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  claseDocumentoId: string;
  nombre: string;
  plantillaId?: string | null;
  versionOrigenId?: string | null;
  derivadaDePropuestaId?: string | null;
  retirada: boolean;
  motivoRetiro?: string | null;
  fechaCreacion: string;
  fechaModificacion: string;
  /** Contenido de la plantilla en el consolidado (aquí solo se usan los códigos de sus bloques). */
  contenidoPropuesto?: { bloques: { codigo: string }[] };
}

/** Observación de una devolución: plantilla, bloque opcional, problema y recomendación. */
export interface DatosObservacion {
  propuestaId: string;
  bloqueCodigo?: string | null;
  problema: string;
  recomendacion?: string | null;
}

export type TipoAjuste =
  | 'ParrafoAgregado'
  | 'ParrafoEliminado'
  | 'ParrafoModificado'
  | 'ParrafoMovido'
  | 'ConfiguracionModificada'
  | 'DisenoModificado'
  | 'BloqueAgregado'
  | 'BloqueEliminado';

/** Un ajuste de la propuesta respecto de la versión original, por ejemplo «NUEVO CONSIDERANDO (posición 2)». */
export interface Ajuste {
  tipo: TipoAjuste;
  bloqueCodigo: string;
  parrafoId?: string | null;
  etiqueta: string;
}

/** «Tipos de documento incluidos»: cada tipo una sola vez. */
export interface ResumenTipo {
  tipoDocumentoId: string;
  tipoDocumento: string;
  operacion: string;
  plantillas: number;
  ajustes: number;
}

export interface Observacion {
  propuestaId: string;
  bloqueCodigo?: string | null;
  parrafoId?: string | null;
  problema: string;
  recomendacion?: string | null;
}

export interface Devolucion extends Entidad {
  consolidadoId: string;
  numeroPresentacion: number;
  usuario: string;
  fecha: string;
  justificacion: string;
  observaciones: Observacion[];
}

export interface Presentacion extends Entidad {
  consolidadoId: string;
  numero: number;
  usuario: string;
  fecha: string;
  cantidadPlantillas: number;
}

export interface Movimiento extends Entidad {
  consolidadoId: string;
  tipo: string;
  usuario: string;
  fecha: string;
  estadoOrigen?: EstadoConsolidado | null;
  estadoDestino?: EstadoConsolidado | null;
  numeroPresentacion?: number | null;
  detalle: string;
}

export interface DetalleConsolidado {
  consolidado: DocumentoConsolidado;
  tiposIncluidos: ResumenTipo[];
  propuestas: Propuesta[];
  /** Ajustes de cada propuesta no retirada, por identificador de propuesta. */
  ajustes: Record<string, Ajuste[]>;
  presentaciones: Presentacion[];
  devoluciones: Devolucion[];
  movimientos: Movimiento[];
}

/** Plantilla vigente que se puede elegir para MODIFICAR. */
export interface PlantillaVigente {
  plantillaId: string;
  nombre: string;
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  tipoDocumento: string;
  claseDocumentoId: string;
  versionId: string;
  numeroVersion: number;
  fechaPublicacion: string;
  /** Número PLAN del consolidado abierto que ya la incluye; null si está libre. */
  enConsolidado?: string | null;
}

export const NOMBRE_ESTADO: Record<EstadoConsolidado, string> = {
  EnRegistro: 'EN REGISTRO',
  Verificado: 'VERIFICADO',
  Aprobado: 'APROBADO',
};

/** Aclaración del estado VERIFICADO (decisión D6): está pendiente, no revisado. */
export const DETALLE_ESTADO: Record<EstadoConsolidado, string> = {
  EnRegistro: 'en preparación por el analista',
  Verificado: 'pendiente de revisión jurídica',
  Aprobado: 'cambios publicados',
};

// ---------- Comparación (Fase 4) ----------

export type TipoSegmento = 'Igual' | 'Agregado' | 'Eliminado' | 'Sustituido';

export interface Segmento {
  tipo: TipoSegmento;
  texto: string;
}

export type EstadoBloqueComparado = 'SinCambios' | 'Modificado' | 'Nuevo' | 'Eliminado';

export type EstadoParrafoComparado = 'SinCambios' | 'Agregado' | 'Eliminado' | 'Modificado';

export interface ParrafoComparado {
  parrafoId: string;
  estado: EstadoParrafoComparado;
  movido: boolean;
  posicionOriginal?: number | null;
  posicionPropuesta?: number | null;
  /** Texto original con sus diferencias; null si el párrafo no existía. */
  original?: Segmento[] | null;
  /** Texto propuesto con sus diferencias; null si el párrafo se eliminó. */
  propuesto?: Segmento[] | null;
  soloFormato: boolean;
  configuracion: string[];
  ajustes: string[];
}

export interface BloqueComparado {
  codigo: string;
  nombre: string;
  estado: EstadoBloqueComparado;
  parrafos: ParrafoComparado[];
}

export interface CampoDiseno {
  campo: string;
  original?: string | null;
  propuesto?: string | null;
}

export interface ComparacionPropuesta {
  propuestaId: string;
  operacion: OperacionPropuesta;
  bloques: BloqueComparado[];
  diseno: CampoDiseno[];
  ajustes: Ajuste[];
}
