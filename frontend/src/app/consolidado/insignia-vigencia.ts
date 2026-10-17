import { Component, input } from '@angular/core';
import { Vigencia } from '../core/modelos';

/**
 * Vigencia de una versión publicada (VIGENTE / NO VIGENTE). Es una etiqueta rectangular con borde, distinta de la
 * píldora del estado del Documento Consolidado, para no confundirlos (regla 19).
 */
@Component({
  selector: 'app-insignia-vigencia',
  template: `<span [class]="'vigencia vigencia-' + vigencia()">{{ vigencia() === 'Vigente' ? 'VIGENTE' : 'NO VIGENTE' }}</span>`,
  styles: `
    .vigencia {
      display: inline-block;
      padding: 1px 8px;
      border: 1.5px solid currentColor;
      border-radius: 3px;
      font-size: var(--texto-pequeno);
      font-weight: 700;
      letter-spacing: 0.04em;
      white-space: nowrap;
    }
    .vigencia-Vigente {
      color: var(--exito);
    }
    .vigencia-NoVigente {
      color: var(--texto-secundario);
      text-decoration: line-through;
    }
  `,
})
export class InsigniaVigencia {
  readonly vigencia = input.required<Vigencia>();
}
