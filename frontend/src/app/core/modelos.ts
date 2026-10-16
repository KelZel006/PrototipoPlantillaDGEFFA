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
  activo: boolean;
}

/** Lista "Documento": Original, Modificación o Aclaratoria. */
export interface ClaseDocumento extends Entidad {
  nombre: string;
  activo: boolean;
}

export type EstadoPlantilla = 'Borrador' | 'Publicada';

export type AlineacionLogo = 'Izquierda' | 'Centro' | 'Derecha';

export interface Plantilla extends Entidad {
  nombre: string;
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  claseDocumentoId: string;
  estado: EstadoPlantilla;
  descripcion?: string;
  /** Logo del encabezado del documento; null si no tiene. */
  logoId?: string | null;
  alineacionLogo?: AlineacionLogo;
  /** Texto del pie de página del documento (varias líneas). */
  piePagina?: string | null;
  /** Espacio en centímetros antes de cada bloque de firma. */
  espacioFirmasCm?: number;
  fechaModificacion: string;
}

/** Secciones del documento, en el orden en que se generan. */
export type Seccion = 'Cabecera' | 'Considerando' | 'PorTanto' | 'Resuelve' | 'Firmas';

export const SECCIONES: readonly Seccion[] = ['Cabecera', 'Considerando', 'PorTanto', 'Resuelve', 'Firmas'];

/** Nodo del documento del editor (formato de TipTap/ProseMirror). */
export interface NodoContenido {
  type: string;
  text?: string;
  attrs?: Record<string, unknown>;
  marks?: { type: string; attrs?: Record<string, unknown> }[];
  content?: NodoContenido[];
}

export interface Parrafo extends Entidad {
  plantillaId: string;
  seccion: Seccion;
  /** Posición en la sección. Con 0, el servidor lo coloca al final. */
  orden: number;
  contenido: NodoContenido;
  activo: boolean;
  /** Con condición, el párrafo solo se muestra si se cumple. Sin condición se muestra siempre. */
  mostrarSoloSiCumple: boolean;
  condicion?: GrupoCondiciones | null;
  /** Lo calcula el servidor a partir del contenido. */
  camposUsados: string[];
  fechaModificacion: string;
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
