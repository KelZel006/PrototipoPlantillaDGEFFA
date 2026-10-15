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
  activo: boolean;
}

export type EstadoPlantilla = 'Borrador' | 'Publicada';

export type AlineacionLogo = 'Izquierda' | 'Centro' | 'Derecha';

export interface Plantilla extends Entidad {
  nombre: string;
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  estado: EstadoPlantilla;
  descripcion?: string;
  /** Logo del encabezado del documento; null si no tiene. */
  logoId?: string | null;
  alineacionLogo?: AlineacionLogo;
  /** Texto del pie de página del documento (varias líneas). */
  piePagina?: string | null;
  fechaModificacion: string;
}

export type Seccion = 'Cabecera' | 'Considerando' | 'Resuelve';

export const SECCIONES: readonly Seccion[] = ['Cabecera', 'Considerando', 'Resuelve'];

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
export const INTERLINEADOS = [
  { valor: '1', nombre: 'Sencillo' },
  { valor: '1.15', nombre: '1.15' },
  { valor: '1.5', nombre: '1.5' },
  { valor: '2', nombre: 'Doble' },
] as const;

export type Interlineado = (typeof INTERLINEADOS)[number]['valor'];

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
