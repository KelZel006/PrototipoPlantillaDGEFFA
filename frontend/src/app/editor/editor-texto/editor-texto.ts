import {
  Component,
  ElementRef,
  OnDestroy,
  afterNextRender,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Editor, JSONContent } from '@tiptap/core';
import { TableKit } from '@tiptap/extension-table';
import { TextAlign } from '@tiptap/extension-text-align';
import { StarterKit } from '@tiptap/starter-kit';
import { identificadorUnico } from '../../compartido/identificador';
import { MAXIMO_CARACTERES, ResolverEtiqueta, contarCaracteres, documentoVacio } from '../../core/contenido';
import { Campo, NodoContenido, esCampoLista } from '../../core/modelos';
import { NodoCampo } from './nodo-campo';

type Alineacion = 'left' | 'center' | 'right' | 'justify';

interface EstadoBarra {
  negrita: boolean;
  cursiva: boolean;
  subrayado: boolean;
  vinetas: boolean;
  numerada: boolean;
  enlace: boolean;
  enTabla: boolean;
  alineacion: Alineacion;
}

const ESTADO_INICIAL: EstadoBarra = {
  negrita: false,
  cursiva: false,
  subrayado: false,
  vinetas: false,
  numerada: false,
  enlace: false,
  enTabla: false,
  alineacion: 'left',
};

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

/**
 * Editor de texto enriquecido con la barra del diseño: negrita, cursiva, subrayado, listas, alineación,
 * enlace, limpiar formato e inserción de campos dinámicos. Solo admite los elementos que acepta el servidor.
 */
@Component({
  selector: 'app-editor-texto',
  templateUrl: './editor-texto.html',
  styleUrl: './editor-texto.css',
})
export class EditorTexto implements OnDestroy {
  protected readonly uid = identificadorUnico('editor');

  /** Campos que se pueden insertar en esta sección (también se usan para mostrar sus etiquetas). */
  readonly campos = input.required<Campo[]>();
  readonly etiqueta = input.required<ResolverEtiqueta>();
  readonly etiquetaAccesible = input('Texto del párrafo');

  /** Se emite con cada cambio del contenido. */
  readonly cambio = output<NodoContenido>();
  /** Se emite cuando el cursor entra o sale de una tabla (los campos de lista solo se insertan dentro). */
  readonly enTablaCambio = output<boolean>();

  private readonly superficie = viewChild.required<ElementRef<HTMLElement>>('superficie');
  private editor: Editor | null = null;
  private contenidoPendiente: NodoContenido | null = null;

  protected readonly estado = signal<EstadoBarra>(ESTADO_INICIAL);
  protected readonly caracteres = signal(0);
  protected readonly excedido = computed(() => this.caracteres() > MAXIMO_CARACTERES);
  protected readonly maximo = MAXIMO_CARACTERES;

  protected readonly creandoTabla = signal(false);
  protected readonly filasTabla = signal(3);
  protected readonly columnasTabla = signal(3);
  protected readonly tablaConEncabezado = signal(true);
  protected readonly columnasMaximas = COLUMNAS_MAXIMAS;
  protected readonly filasMaximas = FILAS_MAXIMAS;

  protected readonly editandoEnlace = signal(false);
  protected readonly urlEnlace = signal('');
  protected readonly errorEnlace = signal<string | null>(null);

  /** Campos agrupados por categoría para el menú "Insertar campo". */
  protected readonly grupos = computed(() => {
    const porCategoria = new Map<string, Campo[]>();
    for (const campo of this.campos()) {
      porCategoria.set(campo.categoria, [...(porCategoria.get(campo.categoria) ?? []), campo]);
    }
    return [...porCategoria.entries()].map(([categoria, campos]) => ({ categoria, campos }));
  });

  constructor() {
    afterNextRender(() => this.crearEditor());
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
    this.editor = null;
  }

  /** Reemplaza el contenido sin marcarlo como cambio del usuario. */
  establecer(contenido: NodoContenido): void {
    if (!this.editor) {
      this.contenidoPendiente = contenido;
      return;
    }
    this.editor.commands.setContent(contenido as JSONContent, { emitUpdate: false });
    this.actualizarEstado();
  }

  /** Inserta un campo en la posición actual del cursor. */
  /**
   * Inserta un campo en la posición actual del cursor. Un campo de lista solo se inserta dentro de una tabla;
   * devuelve false si no se pudo insertar.
   */
  insertarCampo(campoId: string): boolean {
    if (!this.editor || (this.esLista(campoId) && !this.editor.isActive('table'))) {
      return false;
    }
    return this.editor.chain().focus().insertarCampo(campoId).run();
  }

  /** Ids de los campos de lista del catálogo recibido. */
  private readonly idsLista = computed(() => new Set(this.campos().filter(esCampoLista).map((c) => c.id)));

  protected esLista(campoId: string): boolean {
    return this.idsLista().has(campoId);
  }

  limpiar(): void {
    this.editor?.chain().clearContent(true).focus().run();
  }

  enfocar(): void {
    this.editor?.commands.focus('end');
  }

  protected alternarNegrita(): void {
    this.editor?.chain().focus().toggleBold().run();
  }

  protected alternarCursiva(): void {
    this.editor?.chain().focus().toggleItalic().run();
  }

  protected alternarSubrayado(): void {
    this.editor?.chain().focus().toggleUnderline().run();
  }

  protected alternarVinetas(): void {
    this.editor?.chain().focus().toggleBulletList().run();
  }

  protected alternarNumerada(): void {
    this.editor?.chain().focus().toggleOrderedList().run();
  }

  protected alinear(alineacion: Alineacion): void {
    this.editor?.chain().focus().setTextAlign(alineacion).run();
  }

  protected limpiarFormato(): void {
    this.editor?.chain().focus().unsetAllMarks().clearNodes().unsetTextAlign().run();
  }

  protected abrirEnlace(): void {
    this.creandoTabla.set(false);
    const actual = (this.editor?.getAttributes('link')['href'] as string | undefined) ?? '';
    this.urlEnlace.set(actual || 'https://');
    this.errorEnlace.set(null);
    this.editandoEnlace.set(true);
  }

  protected aplicarEnlace(): void {
    const url = this.urlEnlace().trim();
    if (!enlacePermitido(url)) {
      this.errorEnlace.set('Use una dirección que empiece con http://, https:// o mailto:.');
      return;
    }
    this.editor?.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
    this.editandoEnlace.set(false);
  }

  protected quitarEnlace(): void {
    this.editor?.chain().focus().extendMarkRange('link').unsetLink().run();
    this.editandoEnlace.set(false);
  }

  protected cancelarEnlace(): void {
    this.editandoEnlace.set(false);
    this.editor?.commands.focus();
  }

  /** Inserta una tabla en el cursor. No se permiten tablas dentro de otra tabla. */
  insertarTabla(filas: number, columnas: number, conEncabezado: boolean): void {
    if (!this.editor || this.editor.isActive('table')) {
      return;
    }
    const acotar = (valor: number, maximo: number) => Math.min(Math.max(Math.trunc(valor) || 1, 1), maximo);
    this.editor
      .chain()
      .focus()
      .insertTable({
        rows: acotar(filas, FILAS_MAXIMAS),
        cols: acotar(columnas, COLUMNAS_MAXIMAS),
        withHeaderRow: conEncabezado,
      })
      .run();
  }

  protected abrirTabla(): void {
    this.editandoEnlace.set(false);
    this.creandoTabla.set(true);
  }

  protected confirmarTabla(): void {
    this.insertarTabla(this.filasTabla(), this.columnasTabla(), this.tablaConEncabezado());
    this.creandoTabla.set(false);
  }

  protected cancelarTabla(): void {
    this.creandoTabla.set(false);
    this.editor?.commands.focus();
  }

  protected agregarFila(posicion: 'antes' | 'despues'): void {
    const cadena = this.editor?.chain().focus();
    (posicion === 'antes' ? cadena?.addRowBefore() : cadena?.addRowAfter())?.run();
  }

  protected agregarColumna(posicion: 'antes' | 'despues'): void {
    const cadena = this.editor?.chain().focus();
    (posicion === 'antes' ? cadena?.addColumnBefore() : cadena?.addColumnAfter())?.run();
  }

  protected eliminarFila(): void {
    this.editor?.chain().focus().deleteRow().run();
  }

  protected eliminarColumna(): void {
    this.editor?.chain().focus().deleteColumn().run();
  }

  protected alternarFilaEncabezado(): void {
    this.editor?.chain().focus().toggleHeaderRow().run();
  }

  protected combinarCeldas(): void {
    this.editor?.chain().focus().mergeOrSplit().run();
  }

  protected eliminarTabla(): void {
    this.editor?.chain().focus().deleteTable().run();
  }

  protected alElegirCampo(evento: Event): void {
    const lista = evento.target as HTMLSelectElement;
    if (lista.value) {
      this.insertarCampo(lista.value);
    }
    lista.value = '';
  }

  private crearEditor(): void {
    this.editor = new Editor({
      element: this.superficie().nativeElement,
      content: (this.contenidoPendiente ?? documentoVacio()) as JSONContent,
      extensions: [
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
        TextAlign.configure({ types: ['paragraph'], alignments: ['left', 'center', 'right', 'justify'] }),
        TableKit.configure({ table: { resizable: false, HTMLAttributes: { class: 'tabla-editor' } } }),
        NodoCampo.configure({ etiqueta: (id) => this.etiqueta()(id), esLista: (id) => this.esLista(id) }),
      ],
      editorProps: {
        attributes: {
          class: 'superficie-editor',
          role: 'textbox',
          'aria-multiline': 'true',
          'aria-label': this.etiquetaAccesible(),
        },
      },
      onUpdate: ({ editor }) => {
        const contenido = editor.getJSON() as NodoContenido;
        this.caracteres.set(contarCaracteres(contenido, this.etiqueta()));
        this.cambio.emit(contenido);
      },
      onTransaction: () => this.actualizarEstado(),
    });
    this.contenidoPendiente = null;
    this.actualizarEstado();
  }

  private actualizarEstado(): void {
    const editor = this.editor;
    if (!editor) {
      return;
    }
    const alineacion = (['center', 'right', 'justify'] as const).find((a) => editor.isActive({ textAlign: a })) ?? 'left';
    const enTablaAntes = this.estado().enTabla;
    this.estado.set({
      negrita: editor.isActive('bold'),
      cursiva: editor.isActive('italic'),
      subrayado: editor.isActive('underline'),
      vinetas: editor.isActive('bulletList'),
      numerada: editor.isActive('orderedList'),
      enlace: editor.isActive('link'),
      enTabla: editor.isActive('table'),
      alineacion,
    });
    this.caracteres.set(contarCaracteres(editor.getJSON() as NodoContenido, this.etiqueta()));
    if (this.estado().enTabla !== enTablaAntes) {
      this.enTablaCambio.emit(this.estado().enTabla);
    }
  }
}
