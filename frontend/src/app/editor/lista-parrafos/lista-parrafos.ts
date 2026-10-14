import { Component, computed, inject, input, output } from '@angular/core';
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
  templateUrl: './lista-parrafos.html',
  styleUrl: './lista-parrafos.css',
})
export class ListaParrafos {
  private readonly catalogo = inject(CatalogoService);

  readonly parrafos = input.required<Parrafo[]>();
  readonly titulo = input.required<string>();
  readonly editandoId = input<string | null>(null);
  /** Mientras se guarda algo, las acciones se deshabilitan. */
  readonly ocupado = input(false);

  readonly editar = output<Parrafo>();
  readonly eliminar = output<Parrafo>();
  readonly duplicar = output<Parrafo>();
  readonly alternarActivo = output<Parrafo>();
  readonly mover = output<{ parrafo: Parrafo; desplazamiento: -1 | 1 }>();

  protected readonly elementos = computed(() =>
    this.parrafos().map((parrafo) => ({
      parrafo,
      resumen: textoPlano(parrafo.contenido, this.catalogo.etiqueta),
      estado: estadoCondicion(parrafo),
      condicion: parrafo.condicion ? describirGrupo(parrafo.condicion, this.catalogo.porId()) : '',
    })),
  );
}
