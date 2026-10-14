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
import { TextAlign } from '@tiptap/extension-text-align';
import { StarterKit } from '@tiptap/starter-kit';
import { MAXIMO_CARACTERES, ResolverEtiqueta, contarCaracteres, documentoVacio } from '../../core/contenido';
import { Campo, NodoContenido } from '../../core/modelos';
import { NodoCampo } from './nodo-campo';

type Alineacion = 'left' | 'center' | 'right' | 'justify';

interface EstadoBarra {
  negrita: boolean;
  cursiva: boolean;
  subrayado: boolean;
  vinetas: boolean;
  numerada: boolean;
  enlace: boolean;
  alineacion: Alineacion;
}

const ESTADO_INICIAL: EstadoBarra = {
  negrita: false,
  cursiva: false,
  subrayado: false,
  vinetas: false,
  numerada: false,
  enlace: false,
  alineacion: 'left',
};

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
  /** Campos que se pueden insertar en esta sección (también se usan para mostrar sus etiquetas). */
  readonly campos = input.required<Campo[]>();
  readonly etiqueta = input.required<ResolverEtiqueta>();
  readonly etiquetaAccesible = input('Texto del párrafo');

  /** Se emite con cada cambio del contenido. */
  readonly cambio = output<NodoContenido>();

  private readonly superficie = viewChild.required<ElementRef<HTMLElement>>('superficie');
  private editor: Editor | null = null;
  private contenidoPendiente: NodoContenido | null = null;

  protected readonly estado = signal<EstadoBarra>(ESTADO_INICIAL);
  protected readonly caracteres = signal(0);
  protected readonly excedido = computed(() => this.caracteres() > MAXIMO_CARACTERES);
  protected readonly maximo = MAXIMO_CARACTERES;

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
  insertarCampo(campoId: string): void {
    this.editor?.chain().focus().insertarCampo(campoId).run();
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
        NodoCampo.configure({ etiqueta: (id) => this.etiqueta()(id) }),
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
    this.estado.set({
      negrita: editor.isActive('bold'),
      cursiva: editor.isActive('italic'),
      subrayado: editor.isActive('underline'),
      vinetas: editor.isActive('bulletList'),
      numerada: editor.isActive('orderedList'),
      enlace: editor.isActive('link'),
      alineacion,
    });
    this.caracteres.set(contarCaracteres(editor.getJSON() as NodoContenido, this.etiqueta()));
  }
}
