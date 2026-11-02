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
import { identificadorUnico } from '../../compartido/identificador';
import { copiarGrupo, grupoVacio, normalizarCondicion, problemasCondicion, tieneReglas } from '../../core/condiciones';
import { MAXIMO_CARACTERES, ResolverEtiqueta, contarCaracteres, documentoVacio } from '../../core/contenido';
import { Campo, GrupoCondiciones, NodoContenido, esCampoLista } from '../../core/modelos';
import { GrupoCondicionesComponente } from '../constructor-condiciones/grupo-condiciones';
import { COLUMNAS_MAXIMAS, FILAS_MAXIMAS, TAMANOS_TEXTO, enlacePermitido, extensionesEditor } from './extensiones';

export { COLUMNAS_MAXIMAS, FILAS_MAXIMAS, TAMANOS_TEXTO, enlacePermitido } from './extensiones';

type Alineacion = 'left' | 'center' | 'right' | 'justify';

interface EstadoBarra {
  negrita: boolean;
  cursiva: boolean;
  subrayado: boolean;
  vinetas: boolean;
  numerada: boolean;
  enlace: boolean;
  enTabla: boolean;
  /** El cursor está en un texto condicionado. */
  condicionado: boolean;
  alineacion: Alineacion;
  /** Tamaño del texto en el cursor (por ejemplo, "12pt"); vacío es el tamaño normal. */
  tamano: string;
}

/** Texto condicionado del párrafo y su posición en el editor, para abrir su condición o quitarla desde la lista. */
export interface FragmentoCondicionado {
  desde: number;
  hasta: number;
  texto: string;
  condicion: GrupoCondiciones;
}

const ESTADO_INICIAL: EstadoBarra = {
  negrita: false,
  cursiva: false,
  subrayado: false,
  vinetas: false,
  numerada: false,
  enlace: false,
  enTabla: false,
  condicionado: false,
  alineacion: 'justify',
  tamano: '',
};

/**
 * Editor de texto enriquecido con la barra del diseño: negrita, cursiva, subrayado, listas, alineación,
 * enlace, limpiar formato e inserción de campos dinámicos. Solo admite los elementos que acepta el servidor.
 */
@Component({
  selector: 'app-editor-texto',
  imports: [GrupoCondicionesComponente],
  templateUrl: './editor-texto.html',
  styleUrl: './editor-texto.css',
})
export class EditorTexto implements OnDestroy {
  protected readonly uid = identificadorUnico('editor');

  /** Campos que se pueden insertar en esta sección (también se usan para mostrar sus etiquetas). */
  readonly campos = input.required<Campo[]>();
  readonly etiqueta = input.required<ResolverEtiqueta>();
  readonly etiquetaAccesible = input('Texto del párrafo');
  /** Campos que se pueden usar en las condiciones de un texto (los mismos que en la condición del párrafo). */
  readonly camposCondicion = input<Campo[]>([]);
  /** Describe una condición en palabras (para el resaltado y el resumen). */
  readonly describirCondicion = input<(condicion: GrupoCondiciones) => string>(() => '');

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

  protected readonly tamanos = TAMANOS_TEXTO;

  protected readonly creandoTabla = signal(false);
  protected readonly filasTabla = signal(3);
  protected readonly columnasTabla = signal(3);
  protected readonly tablaConEncabezado = signal(true);
  protected readonly columnasMaximas = COLUMNAS_MAXIMAS;
  protected readonly filasMaximas = FILAS_MAXIMAS;

  // Texto condicionado: un fragmento que solo aparece si se cumple su condición.
  protected readonly editandoCondicion = signal(false);
  protected readonly condicionTexto = signal<GrupoCondiciones>(grupoVacio());
  protected readonly errorCondicion = signal<string | null>(null);
  /** Textos condicionados del párrafo, con su posición; se listan debajo del editor. */
  protected readonly condicionados = signal<FragmentoCondicionado[]>([]);

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

  /** Aplica el tamaño elegido al texto seleccionado (o al que se escriba). Vacío vuelve al tamaño normal. */
  protected cambiarTamano(tamano: string): void {
    const cadena = this.editor?.chain().focus();
    (TAMANOS_TEXTO.includes(tamano) ? cadena?.setFontSize(tamano) : cadena?.unsetFontSize())?.run();
  }

  protected limpiarFormato(): void {
    this.editor?.chain().focus().unsetAllMarks().clearNodes().unsetTextAlign().run();
  }

  /**
   * Abre el panel de condición: si el cursor está en un texto condicionado, para cambiar su condición; si no, para
   * condicionar el texto seleccionado.
   */
  protected abrirCondicion(): void {
    const editor = this.editor;
    if (!editor) {
      return;
    }
    this.creandoTabla.set(false);
    this.editandoEnlace.set(false);
    this.errorCondicion.set(null);
    if (editor.isActive('condicion')) {
      editor.chain().extendMarkRange('condicion').run();
      const actual = editor.getAttributes('condicion')['condicion'] as GrupoCondiciones | null;
      this.condicionTexto.set(actual ? copiarGrupo(actual) : grupoVacio());
    } else if (editor.state.selection.empty) {
      this.condicionTexto.set(grupoVacio());
      this.errorCondicion.set('Seleccione primero el texto que quiere condicionar.');
    } else {
      this.condicionTexto.set(grupoVacio());
    }
    this.editandoCondicion.set(true);
  }

  protected aplicarCondicion(): void {
    const editor = this.editor;
    const condicion = normalizarCondicion(this.condicionTexto());
    if (!editor) {
      return;
    }
    if (editor.state.selection.empty && !editor.isActive('condicion')) {
      this.errorCondicion.set('Seleccione primero el texto que quiere condicionar.');
      return;
    }
    const problemas = problemasCondicion(this.condicionTexto());
    if (!condicion || !tieneReglas(condicion) || problemas.length > 0) {
      this.errorCondicion.set(problemas[0] ?? 'Agregue al menos una condición completa.');
      return;
    }
    const cadena = editor.chain().focus();
    (editor.isActive('condicion') ? cadena.extendMarkRange('condicion') : cadena).condicionarTexto(condicion).run();
    this.editandoCondicion.set(false);
  }

  /** Desde la lista de abajo: selecciona el texto condicionado y abre arriba su condición, para cambiarla o quitarla. */
  protected editarFragmento(fragmento: FragmentoCondicionado): void {
    this.editor?.chain().focus().setTextSelection({ from: fragmento.desde, to: fragmento.hasta }).run();
    this.abrirCondicion();
  }

  /** Desde la lista de abajo: quita la condición de ese texto, que pasa a salir siempre. */
  protected quitarFragmento(fragmento: FragmentoCondicionado): void {
    this.editor?.chain().focus().setTextSelection({ from: fragmento.desde, to: fragmento.hasta }).unsetMark('condicion').run();
    this.editandoCondicion.set(false);
  }

  protected quitarCondicion(): void {
    this.editor?.chain().focus().quitarCondicionTexto().run();
    this.editandoCondicion.set(false);
  }

  protected cancelarCondicion(): void {
    this.editandoCondicion.set(false);
    this.editor?.commands.focus();
  }

  protected abrirEnlace(): void {
    this.creandoTabla.set(false);
    this.editandoCondicion.set(false);
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
    this.editandoCondicion.set(false);
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
      extensions: extensionesEditor({
        etiqueta: (id) => this.etiqueta()(id),
        esLista: (id) => this.esLista(id),
        describir: (condicion) => this.describirCondicion()(condicion),
      }),
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

  /**
   * Textos condicionados del documento del editor, en orden. Los nodos seguidos con la misma condición forman un solo
   * fragmento; los campos se muestran como {Etiqueta}.
   */
  private leerCondicionados(editor: Editor): FragmentoCondicionado[] {
    const lista: (FragmentoCondicionado & { clave: string })[] = [];
    editor.state.doc.descendants((nodo, posicion) => {
      if (!nodo.isInline) {
        return true;
      }
      const marca = nodo.marks.find((m) => m.type.name === 'condicion');
      const condicion = marca?.attrs['condicion'] as GrupoCondiciones | null | undefined;
      if (!condicion) {
        return false;
      }
      const clave = JSON.stringify(condicion);
      const texto = nodo.isText ? (nodo.text ?? '') : `{${this.etiqueta()(String(nodo.attrs['campoId'] ?? ''))}}`;
      const ultimo = lista.at(-1);
      if (ultimo && ultimo.clave === clave && ultimo.hasta === posicion) {
        ultimo.hasta = posicion + nodo.nodeSize;
        ultimo.texto += texto;
      } else {
        lista.push({ desde: posicion, hasta: posicion + nodo.nodeSize, texto, condicion, clave });
      }
      return false;
    });
    return lista.map(({ desde, hasta, texto, condicion }) => ({ desde, hasta, texto: texto.replace(/\s+/g, ' ').trim(), condicion }));
  }

  private actualizarEstado(): void {
    const editor = this.editor;
    if (!editor) {
      return;
    }
    const alineacion = (['center', 'right', 'left'] as const).find((a) => editor.isActive({ textAlign: a })) ?? 'justify';
    const enTablaAntes = this.estado().enTabla;
    this.estado.set({
      negrita: editor.isActive('bold'),
      cursiva: editor.isActive('italic'),
      subrayado: editor.isActive('underline'),
      vinetas: editor.isActive('bulletList'),
      numerada: editor.isActive('orderedList'),
      enlace: editor.isActive('link'),
      enTabla: editor.isActive('table'),
      condicionado: editor.isActive('condicion'),
      alineacion,
      tamano: (editor.getAttributes('textStyle')['fontSize'] as string | null | undefined) ?? '',
    });
    this.caracteres.set(contarCaracteres(editor.getJSON() as NodoContenido, this.etiqueta()));
    this.condicionados.set(this.leerCondicionados(editor));
    if (this.estado().enTabla !== enTablaAntes) {
      this.enTablaCambio.emit(this.estado().enTabla);
    }
  }
}
