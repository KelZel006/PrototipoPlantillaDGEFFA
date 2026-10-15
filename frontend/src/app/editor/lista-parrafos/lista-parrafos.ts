import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDragPlaceholder, CdkDropList } from '@angular/cdk/drag-drop';
import { Component, computed, inject, input, output } from '@angular/core';
import { identificadorUnico } from '../../compartido/identificador';
import { CatalogoService } from '../../core/catalogo.service';
import { describirGrupo, tieneReglas } from '../../core/condiciones';
import { textoPlano } from '../../core/contenido';
import { Parrafo } from '../../core/modelos';

/** Estado de visualización de un párrafo según su condición (sin evaluar todavía los datos). */
export type EstadoCondicion = 'siempre' | 'condicionado' | 'condicion-inactiva';

export function estadoCondicion(parrafo: Parrafo): EstadoCondicion {
  if (!tieneReglas(parrafo.condicion)) {
    return 'siempre';
  }
  return parrafo.mostrarSoloSiCumple ? 'condicionado' : 'condicion-inactiva';
}

/** Listado de párrafos de la sección con sus acciones y el resumen de sus condiciones. */
@Component({
  selector: 'app-lista-parrafos',
  imports: [CdkDropList, CdkDrag, CdkDragHandle, CdkDragPlaceholder],
  templateUrl: './lista-parrafos.html',
  styleUrl: './lista-parrafos.css',
})
export class ListaParrafos {
  private readonly catalogo = inject(CatalogoService);

  protected readonly uid = identificadorUnico('listado');

  readonly parrafos = input.required<Parrafo[]>();
  readonly titulo = input.required<string>();
  /**
   * Etiqueta con la que se numeran los párrafos en el documento (por ejemplo, "CONSIDERANDO"). Se numeran solo los
   * activos; al generar, los que no cumplan su condición tampoco consumen número.
   */
  readonly etiquetaNumerada = input<string | null>(null);
  readonly editandoId = input<string | null>(null);
  /** Mientras se guarda algo, las acciones se deshabilitan. */
  readonly ocupado = input(false);

  readonly editar = output<Parrafo>();
  readonly eliminar = output<Parrafo>();
  readonly duplicar = output<Parrafo>();
  readonly alternarActivo = output<Parrafo>();
  readonly mover = output<{ parrafo: Parrafo; desplazamiento: -1 | 1 }>();
  /** Nuevo orden tras arrastrar una tarjeta: posición de origen y de destino. */
  readonly reordenar = output<{ desde: number; hasta: number }>();

  protected soltar(evento: Pick<CdkDragDrop<unknown>, 'previousIndex' | 'currentIndex'>): void {
    if (evento.previousIndex !== evento.currentIndex) {
      this.reordenar.emit({ desde: evento.previousIndex, hasta: evento.currentIndex });
    }
  }

  protected readonly elementos = computed(() => {
    let numero = 0;
    const etiqueta = this.etiquetaNumerada();
    return this.parrafos().map((parrafo) => ({
      numeracion: etiqueta && parrafo.activo ? `${etiqueta}(${++numero}):` : null,
      parrafo,
      resumen: textoPlano(parrafo.contenido, this.catalogo.etiqueta),
      estado: estadoCondicion(parrafo),
      condicion: parrafo.condicion ? describirGrupo(parrafo.condicion, this.catalogo.porId()) : '',
    }));
  });
}
