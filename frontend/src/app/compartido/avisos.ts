import { Component, inject } from '@angular/core';
import { InteraccionService } from '../core/interaccion.service';

/** Avisos breves de resultado (guardado, eliminado, errores) en la esquina inferior. */
@Component({
  selector: 'app-avisos',
  template: `
    <div class="avisos" aria-live="polite">
      @for (aviso of interaccion.avisos(); track aviso.id) {
        <div class="aviso-flotante" [class.aviso-flotante-error]="aviso.tipo === 'error'" [attr.role]="aviso.tipo === 'error' ? 'alert' : 'status'">
          <span>{{ aviso.texto }}</span>
          <button type="button" aria-label="Cerrar aviso" (click)="interaccion.cerrarAviso(aviso.id)">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .avisos {
      position: fixed;
      right: var(--espacio-4);
      bottom: var(--espacio-4);
      z-index: 10;
      display: flex;
      flex-direction: column;
      gap: var(--espacio-2);
      max-width: min(420px, calc(100vw - 32px));
    }
    .aviso-flotante {
      display: flex;
      align-items: flex-start;
      gap: var(--espacio-3);
      padding: var(--espacio-3) var(--espacio-4);
      border-left: 4px solid var(--exito);
      border-radius: var(--radio);
      background: var(--superficie);
      box-shadow: 0 8px 24px rgb(31 42 61 / 16%);
    }
    .aviso-flotante-error {
      border-left-color: var(--error);
    }
    .aviso-flotante span {
      flex: 1;
    }
    button {
      border: 0;
      background: transparent;
      color: var(--texto-secundario);
      font-size: 1.25rem;
      line-height: 1;
      cursor: pointer;
    }
  `,
})
export class Avisos {
  protected readonly interaccion = inject(InteraccionService);
}
