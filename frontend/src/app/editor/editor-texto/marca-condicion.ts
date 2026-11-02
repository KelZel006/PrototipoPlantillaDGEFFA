import { Mark, mergeAttributes } from '@tiptap/core';
import { GrupoCondiciones } from '../../core/modelos';

declare module '@tiptap/core' {
  interface Commands<ReturnType> {
    condicion: {
      /** Condiciona el texto seleccionado: solo aparecerá en el documento si se cumple la condición. */
      condicionarTexto: (condicion: GrupoCondiciones) => ReturnType;
      /** Quita la condición del texto condicionado donde está el cursor. */
      quitarCondicionTexto: () => ReturnType;
    };
  }
}

export interface OpcionesMarcaCondicion {
  /** Describe la condición en palabras, para el aviso que aparece al pasar el cursor. */
  describir: (condicion: GrupoCondiciones) => string;
}

/**
 * Texto condicionado: un fragmento del párrafo (texto y campos) que solo aparece en el documento si se cumple su
 * condición. Se guarda como la marca «condicion» con la condición en sus atributos, con la misma forma que la condición
 * del párrafo; el servidor la valida y la evalúa al generar el documento.
 */
export const MarcaCondicion = Mark.create<OpcionesMarcaCondicion>({
  name: 'condicion',
  // Lo que se escribe justo después del fragmento no queda condicionado.
  inclusive: false,
  // Convive con la negrita, la cursiva, los enlaces y el tamaño del texto.
  excludes: '',

  addOptions() {
    return { describir: () => '' };
  },

  addAttributes() {
    return {
      condicion: {
        default: null,
        parseHTML: (elemento: HTMLElement) => {
          try {
            return JSON.parse(elemento.getAttribute('data-condicion') ?? 'null');
          } catch {
            return null;
          }
        },
        renderHTML: (atributos: { condicion?: GrupoCondiciones | null }) =>
          atributos.condicion ? { 'data-condicion': JSON.stringify(atributos.condicion) } : {},
      },
    };
  },

  parseHTML() {
    return [{ tag: 'span[data-condicion]' }];
  },

  renderHTML({ mark, HTMLAttributes }) {
    const condicion = mark.attrs['condicion'] as GrupoCondiciones | null;
    const descripcion = condicion ? this.options.describir(condicion) : '';
    return [
      'span',
      mergeAttributes(HTMLAttributes, {
        class: 'texto-condicional',
        title: descripcion ? `Solo aparece si: ${descripcion}` : 'Texto condicionado',
      }),
      0,
    ];
  },

  addCommands() {
    return {
      condicionarTexto:
        (condicion) =>
        ({ commands }) =>
          commands.setMark(this.name, { condicion }),
      quitarCondicionTexto:
        () =>
        ({ chain }) =>
          chain().extendMarkRange(this.name).unsetMark(this.name).run(),
    };
  },
});
