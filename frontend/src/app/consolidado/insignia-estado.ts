import { Component, input } from '@angular/core';
import { DETALLE_ESTADO, EstadoConsolidado, NOMBRE_ESTADO } from '../core/modelos';

/**
 * Estado del Documento Consolidado (EN REGISTRO, VERIFICADO, APROBADO). Tiene forma de píldora con un punto, distinta
 * de la marca de vigencia de las plantillas, para no confundirlos (regla 19).
 */
@Component({
  selector: 'app-insignia-estado',
  template: `
    <span class="estado" [class]="'estado estado-' + estado()">
      <span class="punto" aria-hidden="true"></span>{{ nombres[estado()] }}
    </span>
    @if (detalle()) {
      <span class="detalle">{{ detalles[estado()] }}</span>
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--espacio-2);
    }
    .estado {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 2px 10px;
      border-radius: 999px;
      font-size: var(--texto-pequeno);
      font-weight: 700;
      letter-spacing: 0.02em;
      white-space: nowrap;
    }
    .punto {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: currentColor;
    }
    .estado-EnRegistro {
      background: var(--prueba-suave);
      color: var(--prueba);
    }
    .estado-Verificado {
      background: var(--primario-suave);
      color: var(--primario-oscuro);
    }
    .estado-Aprobado {
      background: var(--exito-suave);
      color: var(--exito);
    }
    .detalle {
      font-size: var(--texto-pequeno);
      color: var(--texto-secundario);
    }
  `,
})
export class InsigniaEstado {
  readonly estado = input.required<EstadoConsolidado>();
  /** Muestra la aclaración, por ejemplo «pendiente de revisión jurídica». */
  readonly detalle = input(false);

  protected readonly nombres = NOMBRE_ESTADO;
  protected readonly detalles = DETALLE_ESTADO;
}
