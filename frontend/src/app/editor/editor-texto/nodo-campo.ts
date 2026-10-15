import { Node, mergeAttributes } from '@tiptap/core';
import { ResolverEtiqueta, textoCampo } from '../../core/contenido';

export interface OpcionesNodoCampo {
  etiqueta: ResolverEtiqueta;
  /** Indica si el campo es de tipo lista (se muestra distinto: su fila de tabla se repite por registro). */
  esLista: (campoId: string) => boolean;
}

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    campo: {
      /** Inserta un campo dinámico en la posición del cursor. */
      insertarCampo: (campoId: string) => ReturnType;
    };
  }
}

/**
 * Campo dinámico dentro del texto. Es un bloque indivisible: se inserta, se mueve o se borra entero,
 * y en el documento solo se guarda su identificador (`campoId`), nunca su valor.
 */
export const NodoCampo = Node.create<OpcionesNodoCampo>({
  name: 'campo',
  group: 'inline',
  inline: true,
  atom: true,
  selectable: true,

  addOptions() {
    return { etiqueta: (id: string) => id, esLista: () => false };
  },

  addAttributes() {
    return {
      campoId: {
        default: null,
        parseHTML: (elemento) => elemento.getAttribute('data-campo-id'),
        renderHTML: (atributos) => ({ 'data-campo-id': atributos['campoId'] }),
      },
    };
  },

  parseHTML() {
    return [{ tag: 'span[data-campo-id]' }];
  },

  renderHTML({ node, HTMLAttributes }) {
    const etiqueta = this.options.etiqueta(node.attrs['campoId']);
    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        class: this.options.esLista(node.attrs['campoId']) ? 'campo-dinamico campo-lista' : 'campo-dinamico',
        contenteditable: 'false',
        title: `Campo dinámico: ${etiqueta} (${node.attrs['campoId']})`,
      }),
      textoCampo(etiqueta),
    ];
  },

  renderText({ node }) {
    return textoCampo(this.options.etiqueta(node.attrs['campoId']));
  },

  addCommands() {
    return {
      insertarCampo:
        (campoId: string) =>
        ({ chain }) =>
          chain()
            .insertContent([
              { type: this.name, attrs: { campoId } },
              { type: 'text', text: ' ' },
            ])
            .run(),
    };
  },
});
