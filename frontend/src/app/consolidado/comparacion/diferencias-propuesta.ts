import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ComparacionPropuesta } from './comparacion-propuesta';

/**
 * Diferencias de una plantilla del consolidado con su versión vigente (/consolidados/:id/propuestas/:propuestaId/diferencias).
 * Solo para el Oficial Jurídico con el documento VERIFICADO.
 */
@Component({
  selector: 'app-diferencias-propuesta',
  imports: [RouterLink, ComparacionPropuesta],
  template: `
    <a class="volver" [routerLink]="['/consolidados', id()]">← Volver al documento</a>
    <section class="tarjeta">
      <header class="encabezado-tarjeta">
        <div>
          <h2>Diferencias con la versión vigente</h2>
          @if (edicion(); as e) {
            <p class="texto-secundario sin-margen">{{ e.nombre }} · {{ e.consolidadoNumero }}</p>
          }
        </div>
      </header>
      <app-comparacion-propuesta [propuestaId]="propuestaId()" />
    </section>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--espacio-4);
    }
    .volver {
      align-self: flex-start;
      font-weight: 600;
    }
    .sin-margen {
      margin: 0;
    }
    a.boton {
      text-decoration: none;
    }
  `,
})
export class DiferenciasPropuesta {
  private readonly api = inject(ApiService);

  /** Parámetros de la ruta. */
  readonly id = input.required<string>();
  readonly propuestaId = input.required<string>();

  private readonly recurso = rxResource({ params: () => this.propuestaId(), stream: ({ params }) => this.api.edicion(params) });
  protected readonly edicion = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));
}
