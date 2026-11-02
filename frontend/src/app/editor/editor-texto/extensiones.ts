import { Extensions } from '@tiptap/core';
import { TableKit } from '@tiptap/extension-table';
import { TextAlign } from '@tiptap/extension-text-align';
import { FontSize, TextStyle } from '@tiptap/extension-text-style';
import { StarterKit } from '@tiptap/starter-kit';
import { GrupoCondiciones } from '../../core/modelos';
import { MarcaCondicion } from './marca-condicion';
import { NodoCampo } from './nodo-campo';

/** Tamaños de texto que se pueden elegir; coinciden con los del servidor (ContenidoEditor.TamanosTexto). */
export const TAMANOS_TEXTO = ['8pt', '9pt', '10pt', '11pt', '12pt', '13pt', '14pt', '16pt', '18pt', '20pt', '24pt'];

/**
 * Tamaño del texto. Igual que FontSize de TipTap, pero al pegar contenido (por ejemplo, desde Word) solo conserva los
 * tamaños de la lista; los demás se descartan para que el servidor no rechace el párrafo.
 */
const TamanoTexto = FontSize.extend({
  addGlobalAttributes() {
    return [
      {
        types: ['textStyle'],
        attributes: {
          fontSize: {
            default: null,
            parseHTML: (elemento: HTMLElement) => {
              const tamano = elemento.style.fontSize.replace(/\s+/g, '');
              return TAMANOS_TEXTO.includes(tamano) ? tamano : null;
            },
            renderHTML: (atributos: { fontSize?: string | null }) =>
              atributos.fontSize ? { style: `font-size: ${atributos.fontSize}` } : {},
          },
        },
      },
    ];
  },
});

/** Límites de una tabla; coinciden con los del servidor. */
export const COLUMNAS_MAXIMAS = 12;
export const FILAS_MAXIMAS = 100;

const PROTOCOLOS_PERMITIDOS = ['http:', 'https:', 'mailto:'];

/** Comprueba que un enlace use http, https o mailto (el servidor aplica la misma regla). */
export function enlacePermitido(url: string): boolean {
  try {
    return PROTOCOLOS_PERMITIDOS.includes(new URL(url).protocol);
  } catch {
    return false;
  }
}

export interface OpcionesExtensiones {
  /** Etiqueta de un campo, para mostrarlo como {Etiqueta}. */
  etiqueta: (campoId: string) => string;
  /** Si el campo es de lista (se distingue y solo va en tablas). */
  esLista: (campoId: string) => boolean;
  /** Describe la condición de un texto condicionado. */
  describir: (condicion: GrupoCondiciones) => string;
}

/**
 * Extensiones del editor: solo los elementos que acepta el servidor. Las usan el editor y la vista de solo lectura de
 * la plantilla, para que el texto se vea igual en los dos.
 */
export function extensionesEditor(opciones: OpcionesExtensiones): Extensions {
  return [
    StarterKit.configure({
      heading: false,
      blockquote: false,
      code: false,
      codeBlock: false,
      horizontalRule: false,
      strike: false,
      link: {
        openOnClick: false,
        autolink: false,
        linkOnPaste: false,
        protocols: [],
        isAllowedUri: (url) => enlacePermitido(url),
      },
    }),
    TextAlign.configure({
      types: ['paragraph'],
      alignments: ['left', 'center', 'right', 'justify'],
      // El documento va justificado: es la alineación de todo párrafo nuevo.
      defaultAlignment: 'justify',
    }),
    TextStyle,
    TamanoTexto,
    TableKit.configure({ table: { resizable: false, HTMLAttributes: { class: 'tabla-editor' } } }),
    NodoCampo.configure({ etiqueta: opciones.etiqueta, esLista: opciones.esLista }),
    MarcaCondicion.configure({ describir: opciones.describir }),
  ];
}
