import { Component, computed, input } from '@angular/core';
import { AlertaCampo } from '../core/modelos';

/** Aviso al aprobador: el documento modificó un campo vigilado (por ejemplo, Operador). */
@Component({
  selector: 'app-aviso-campos',
  template: `
    @for (grupo of grupos(); track grupo.campo) {
      <section class="aviso-campos" role="alert">
        <strong>⚠ Se modificó el campo {{ grupo.campo }}</strong>
        <p>Revise estos cambios antes de aprobar:</p>
        <ul>
          @for (a of grupo.alertas; track $index) {
            <li>
              <span class="plantilla">{{ a.plantilla }}</span> · {{ a.ubicacion }}: {{ a.detalle }}
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    .aviso-campos {
      display: flex;
      flex-direction: column;
      gap: var(--espacio-1);
      margin-bottom: var(--espacio-4);
      padding: var(--espacio-3) var(--espacio-4);
      border: 1px solid var(--prueba);
      border-left-width: 4px;
      border-radius: var(--radio);
      background: var(--prueba-suave);
      color: var(--texto);
    }
    strong {
      color: var(--prueba);
    }
    p,
    ul {
      margin: 0;
    }
    ul {
      padding-left: var(--espacio-5);
    }
    .plantilla {
      font-weight: 600;
    }
  `,
})
export class AvisoCampos {
  readonly alertas = input<AlertaCampo[]>([]);

  protected readonly grupos = computed(() => {
    const porCampo = new Map<string, AlertaCampo[]>();
    for (const alerta of this.alertas()) {
      porCampo.set(alerta.campo, [...(porCampo.get(alerta.campo) ?? []), alerta]);
    }
    return [...porCampo].map(([campo, alertas]) => ({ campo, alertas }));
  });
}
