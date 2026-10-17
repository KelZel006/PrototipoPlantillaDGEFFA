import { Component, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContextoService } from '../../core/contexto.service';
import { PlantillaActualService } from '../../core/plantilla-actual.service';
import { PanelPlantilla } from '../../editor/panel-plantilla/panel-plantilla';
import { PestanasSecciones } from '../../editor/pestanas-secciones/pestanas-secciones';

/**
 * Editor de una propuesta (/consolidados/:id/propuestas/:propuestaId): el mismo editor de siempre (pestañas, campos,
 * condiciones, tablas y diseño), ahora sobre el contenido propuesto. La plantilla vigente no cambia.
 */
@Component({
  selector: 'app-editor-propuesta',
  imports: [RouterLink, PanelPlantilla, PestanasSecciones],
  template: `
    <a class="volver" [routerLink]="['/consolidados', id()]">← Volver al documento</a>
    <app-panel-plantilla />
    @if (actual.plantilla(); as plantilla) {
      @if (plantilla.editable) {
        <!-- El editor (TipTap, tablas y arrastre) se descarga aparte, solo cuando hay una propuesta editable. -->
        @defer {
          <app-pestanas-secciones [plantilla]="plantilla" />
        } @loading (minimum 200ms) {
          <p class="texto-secundario" aria-live="polite">Cargando el editor…</p>
        } @error {
          <div class="aviso aviso-error" role="alert">No se pudo cargar el editor. Recargue la página.</div>
        }
      }
    }
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
  `,
})
export class EditorPropuesta {
  protected readonly actual = inject(PlantillaActualService);
  private readonly contexto = inject(ContextoService);

  /** Parámetros de la ruta. */
  readonly id = input.required<string>();
  readonly propuestaId = input.required<string>();

  constructor() {
    effect(() => {
      const propuesta = this.propuestaId();
      untracked(() => {
        // Cada propuesta empieza sin cambios pendientes de otra.
        this.contexto.descartarCambios();
        this.actual.abrir(propuesta);
      });
    });
  }
}
