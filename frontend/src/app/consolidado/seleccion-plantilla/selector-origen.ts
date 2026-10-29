import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, linkedSignal, output } from '@angular/core';
import { ContextoService } from '../../core/contexto.service';
import { UnidadService } from '../../core/unidad.service';
import { PlantillaVigente } from '../../core/modelos';
import { SelectorContexto } from '../../editor/selector-contexto/selector-contexto';

/**
 * Plantilla a clonar: se elige con las mismas cuatro listas (Rubro, Sub Rubro, Tipo Documento y Documento) entre las
 * plantillas vigentes. Tiene su propia selección, independiente de la de la plantilla nueva.
 */
@Component({
  selector: 'app-selector-origen',
  imports: [DatePipe, SelectorContexto],
  providers: [ContextoService],
  template: `
    <label class="unidad">
      <span>Unidad</span>
      <select id="origen-unidad" class="control control-compacto" (change)="unidad.set($any($event.target).value)">
        @for (u of unidades.unidades(); track u.id) {
          <option [value]="u.id" [selected]="u.id === unidad()">{{ u.nombre }}</option>
        }
      </select>
    </label>
    <app-selector-contexto etiqueta="Plantilla a clonar" prefijo="origen" />
    @if (contexto.completo()) {
      @if (encontrada(); as p) {
        <p class="encontrada" role="status">
          Se copiará <strong>{{ p.nombre }}</strong> (<code>{{ p.plantillaId }}</code>, versión vigente {{ p.numeroVersion }}, desde
          {{ p.fechaPublicacion | date: 'dd/MM/yyyy' }}). La original no cambia.
        </p>
      } @else {
        <p class="no-encontrada" role="status">No hay una plantilla vigente con esa combinación. Elija otra.</p>
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--espacio-2);
    }
    .unidad {
      display: flex;
      align-items: center;
      gap: var(--espacio-2);
      font-size: var(--texto-pequeno);
      font-weight: 600;
    }
    .encontrada,
    .no-encontrada {
      margin: 0;
      padding: var(--espacio-2) var(--espacio-3);
      border-radius: var(--radio);
      font-size: var(--texto-pequeno);
    }
    .encontrada {
      background: var(--primario-suave);
    }
    .no-encontrada {
      background: var(--fondo-suave);
      color: var(--texto-secundario);
    }
  `,
})
export class SelectorOrigen {
  protected readonly contexto = inject(ContextoService);
  protected readonly unidades = inject(UnidadService);

  readonly vigentes = input.required<PlantillaVigente[]>();
  /** Unidad con que empieza la búsqueda (la del documento); se puede clonar de cualquier unidad. */
  readonly unidadInicial = input('');
  protected readonly unidad = linkedSignal(() => this.unidadInicial());
  /** Plantilla vigente que coincide con las cuatro listas; null mientras no haya una. */
  readonly cambio = output<PlantillaVigente | null>();

  protected readonly encontrada = computed(() => {
    const rubro = this.contexto.rubro();
    const subRubro = this.contexto.subRubro();
    const tipo = this.contexto.tipoDocumento();
    const clase = this.contexto.claseDocumento();
    if (!this.contexto.completo() || !rubro || !subRubro || !tipo || !clase) {
      return null;
    }
    return (
      this.vigentes().find(
        (p) =>
          (!this.unidad() || p.unidadId === this.unidad()) &&
          p.rubroId === rubro.id && p.subRubroId === subRubro.id && p.tipoDocumentoId === tipo.id && p.claseDocumentoId === clase.id,
      ) ?? null
    );
  });

  constructor() {
    effect(() => this.cambio.emit(this.encontrada()));
  }
}
