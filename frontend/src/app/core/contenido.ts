import { NodoContenido } from './modelos';

/** Límite de caracteres por párrafo (diseño de referencia). El servidor aplica la misma regla. */
export const MAXIMO_CARACTERES = 2000;

/** Busca la etiqueta legible de un campo a partir de su identificador. */
export type ResolverEtiqueta = (campoId: string) => string;

export function documentoVacio(): NodoContenido {
  return { type: 'doc', content: [{ type: 'paragraph' }] };
}

function* recorrer(nodo: NodoContenido): Generator<NodoContenido> {
  yield nodo;
  for (const hijo of nodo.content ?? []) {
    yield* recorrer(hijo);
  }
}

function campoId(nodo: NodoContenido): string | null {
  const id = nodo.attrs?.['campoId'];
  return typeof id === 'string' ? id : null;
}

/** Campos insertados, en orden de aparición y sin repetir. */
export function camposUsados(doc: NodoContenido): string[] {
  const ids: string[] = [];
  for (const nodo of recorrer(doc)) {
    const id = nodo.type === 'campo' ? campoId(nodo) : null;
    if (id && !ids.includes(id)) {
      ids.push(id);
    }
  }
  return ids;
}

/** Representación de un campo dentro del texto, por ejemplo "{Operador}". */
export function textoCampo(etiqueta: string): string {
  return `{${etiqueta}}`;
}

/**
 * Caracteres visibles: el texto escrito más cada campo contado como "{Etiqueta}".
 * Es la misma regla que aplica el servidor (ContenidoEditor.ContarCaracteres).
 */
export function contarCaracteres(doc: NodoContenido, etiqueta: ResolverEtiqueta): number {
  let total = 0;
  for (const nodo of recorrer(doc)) {
    if (nodo.type === 'text') {
      total += nodo.text?.length ?? 0;
    } else if (nodo.type === 'campo') {
      const id = campoId(nodo);
      total += id ? textoCampo(etiqueta(id)).length : 0;
    }
  }
  return total;
}

/** Verdadero si hay texto visible o algún campo. */
export function tieneContenido(doc: NodoContenido): boolean {
  for (const nodo of recorrer(doc)) {
    if (nodo.type === 'campo' || (nodo.type === 'text' && (nodo.text ?? '').trim() !== '')) {
      return true;
    }
  }
  return false;
}

/**
 * Quita atributos nulos y listas vacías que agrega el editor (por ejemplo `textAlign: null`).
 * Así se guarda un JSON mínimo y dos contenidos iguales se comparan como iguales.
 */
export function limpiarDocumento(nodo: NodoContenido): NodoContenido {
  const limpio: NodoContenido = { type: nodo.type };
  if (nodo.text !== undefined) {
    limpio.text = nodo.text;
  }
  const attrs = Object.fromEntries(Object.entries(nodo.attrs ?? {}).filter(([, valor]) => valor !== null && valor !== undefined));
  if (Object.keys(attrs).length > 0) {
    limpio.attrs = attrs;
  }
  if (nodo.marks?.length) {
    limpio.marks = nodo.marks.map((marca) => {
      const atributosMarca = Object.fromEntries(
        Object.entries(marca.attrs ?? {}).filter(([, valor]) => valor !== null && valor !== undefined),
      );
      return Object.keys(atributosMarca).length > 0 ? { type: marca.type, attrs: atributosMarca } : { type: marca.type };
    });
  }
  if (nodo.content?.length) {
    limpio.content = nodo.content.map(limpiarDocumento);
  }
  return limpio;
}

/** Texto plano para el resumen del listado; los bloques se separan con un espacio. */
export function textoPlano(doc: NodoContenido, etiqueta: ResolverEtiqueta): string {
  const partes: string[] = [];
  const visitar = (nodo: NodoContenido) => {
    if (nodo.type === 'text') {
      partes.push(nodo.text ?? '');
    } else if (nodo.type === 'campo') {
      const id = campoId(nodo);
      partes.push(id ? textoCampo(etiqueta(id)) : '');
    } else if (nodo.type === 'hardBreak') {
      partes.push(' ');
    } else if (nodo.type === 'table') {
      // Igual que en el servidor: el resumen no muestra el contenido de las celdas.
      partes.push(' [Tabla] ');
      return;
    }
    for (const hijo of nodo.content ?? []) {
      visitar(hijo);
    }
    if (nodo.type === 'paragraph' || nodo.type === 'listItem') {
      partes.push(' ');
    }
  };
  visitar(doc);
  return partes.join('').replace(/\s+/g, ' ').trim();
}
