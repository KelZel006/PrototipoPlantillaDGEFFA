import { Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { InteraccionService } from '../core/interaccion.service';

/** Diálogo modal de confirmación (por ejemplo, antes de eliminar un párrafo). */
@Component({
  selector: 'app-dialogo-confirmacion',
  template: `
    <dialog #dialogo class="dialogo" aria-labelledby="dialogo-titulo" (cancel)="responder(false, $event)">
      @if (interaccion.confirmacion(); as solicitud) {
        <h2 id="dialogo-titulo">{{ solicitud.titulo }}</h2>
        <p>{{ solicitud.mensaje }}</p>
        <div class="acciones">
          <button type="button" class="boton boton-secundario" (click)="responder(false)">Cancelar</button>
          <button
            type="button"
            class="boton"
            [class.boton-peligro]="solicitud.peligro"
            [class.boton-primario]="!solicitud.peligro"
            (click)="responder(true)"
          >
            {{ solicitud.aceptar }}
          </button>
        </div>
      }
    </dialog>
  `,
  styles: `
    .dialogo {
      width: min(440px, calc(100vw - 32px));
      padding: var(--espacio-5);
      border: 1px solid var(--borde);
      border-radius: var(--radio);
      color: var(--texto);
      box-shadow: 0 12px 32px rgb(31 42 61 / 18%);
    }
    .dialogo::backdrop {
      background: rgb(31 42 61 / 40%);
    }
    h2 {
      margin: 0 0 var(--espacio-2);
    }
    p {
      margin: 0 0 var(--espacio-5);
      color: var(--texto-secundario);
      overflow-wrap: anywhere;
    }
    .acciones {
      display: flex;
      justify-content: flex-end;
      gap: var(--espacio-2);
    }
  `,
})
export class DialogoConfirmacion {
  protected readonly interaccion = inject(InteraccionService);
  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  constructor() {
    effect(() => {
      const elemento = this.dialogo().nativeElement;
      const abierto = this.interaccion.confirmacion() !== null;
      if (abierto && !elemento.open) {
        // jsdom (pruebas) no implementa showModal.
        if (typeof elemento.showModal === 'function') {
          elemento.showModal();
        } else {
          elemento.setAttribute('open', '');
        }
      } else if (!abierto && elemento.open) {
        elemento.close?.();
        elemento.removeAttribute('open');
      }
    });
  }

  protected responder(aceptado: boolean, evento?: Event): void {
    evento?.preventDefault();
    this.interaccion.responder(aceptado);
  }
}
