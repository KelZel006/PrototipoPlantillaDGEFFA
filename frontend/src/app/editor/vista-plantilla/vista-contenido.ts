import { Component, ElementRef, OnDestroy, afterNextRender, effect, input, viewChild } from '@angular/core';
import { Editor, JSONContent } from '@tiptap/core';
import { ResolverEtiqueta } from '../../core/contenido';
import { GrupoCondiciones, NodoContenido } from '../../core/modelos';
import { extensionesEditor } from '../editor-texto/extensiones';

/**
 * Contenido de un párrafo tal como se ve en el editor (formato, campos, tablas y textos condicionados), sin poder
 * modificarlo.
 */
@Component({
  selector: 'app-vista-contenido',
  template: `<div #superficie></div>`,
  styleUrl: '../editor-texto/editor-texto.css',
})
export class VistaContenido implements OnDestroy {
  readonly contenido = input.required<NodoContenido>();
  readonly etiqueta = input.required<ResolverEtiqueta>();
  readonly esLista = input<(campoId: string) => boolean>(() => false);
  readonly describirCondicion = input<(condicion: GrupoCondiciones) => string>(() => '');

  private readonly superficie = viewChild.required<ElementRef<HTMLElement>>('superficie');
  private editor: Editor | null = null;

  constructor() {
    afterNextRender(() => {
      this.editor = new Editor({
        element: this.superficie().nativeElement,
        content: this.contenido() as JSONContent,
        editable: false,
        extensions: extensionesEditor({
          etiqueta: (id) => this.etiqueta()(id),
          esLista: (id) => this.esLista()(id),
          describir: (condicion) => this.describirCondicion()(condicion),
        }),
        editorProps: { attributes: { class: 'superficie-editor superficie-lectura', 'aria-readonly': 'true' } },
      });
    });
    effect(() => {
      const contenido = this.contenido();
      this.editor?.commands.setContent(contenido as JSONContent, { emitUpdate: false });
    });
  }

  ngOnDestroy(): void {
    this.editor?.destroy();
    this.editor = null;
  }
}
