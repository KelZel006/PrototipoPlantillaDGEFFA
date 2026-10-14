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

export interface Plantilla extends Entidad {
  nombre: string;
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  estado: EstadoPlantilla;
  descripcion?: string;
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
  orden: number;
  contenido: NodoContenido;
  activo: boolean;
  mostrarSoloSiCumple: boolean;
  condicionId?: string;
  camposUsados: string[];
  fechaModificacion: string;
}

export type TipoDato = 'Texto' | 'Numero' | 'Moneda' | 'Fecha' | 'Booleano';

export interface Campo extends Entidad {
  etiqueta: string;
  categoria: string;
  tipoDato: TipoDato;
  origen: { coleccion: string; propiedad: string };
  contextos: string[];
  descripcion?: string;
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
