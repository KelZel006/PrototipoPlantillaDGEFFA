import { Component, computed, input, output } from '@angular/core';
import { identificadorUnico } from '../../compartido/identificador';
import { problemasCondicion, tieneReglas } from '../../core/condiciones';
import { Campo, GrupoCondiciones } from '../../core/modelos';
import { GrupoCondicionesComponente } from './grupo-condiciones';

/** Panel "Condiciones de visualización" del párrafo en edición. */
@Component({
  selector: 'app-constructor-condiciones',
  imports: [GrupoCondicionesComponente],
  templateUrl: './constructor-condiciones.html',
  styleUrl: './constructor-condiciones.css',
})
export class ConstructorCondiciones {
  protected readonly uid = identificadorUnico('condiciones');

  readonly condicion = input.required<GrupoCondiciones>();
  readonly mostrarSoloSiCumple = input.required<boolean>();
  readonly campos = input.required<Campo[]>();

  readonly condicionCambio = output<GrupoCondiciones>();
  readonly mostrarSoloSiCumpleCambio = output<boolean>();

  protected readonly conReglas = computed(() => tieneReglas(this.condicion()));
  protected readonly problemas = computed(() => problemasCondicion(this.condicion()));
}
