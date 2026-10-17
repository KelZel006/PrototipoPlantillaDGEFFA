import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { mensajeError } from '../../core/errores';
import { EstadoBloqueComparado, Segmento } from '../../core/modelos';

type Vista = 'Ambos' | 'Original' | 'Consolidado';

const NOMBRE_ESTADO: Record<EstadoBloqueComparado, string> = {
  SinCambios: 'Sin cambios',
  Modificado: 'Modificado',
  Nuevo: 'NUEVO BLOQUE',
  Eliminado: 'BLOQUE ELIMINADO',
};

/**
 * Comparación lado a lado de la plantilla en el consolidado con su versión original, para la revisión del Oficial
 * Jurídico (solo con el documento VERIFICADO). Cada bloque muestra el texto original a la izquierda y el del
 * consolidado a la derecha, con lo agregado, eliminado y sustituido resaltado. Los bloques sin cambios aparecen
 * plegados.
 */
@Component({
  selector: 'app-comparacion-propuesta',
  templateUrl: './comparacion-propuesta.html',
  styleUrl: './comparacion-propuesta.css',
})
export class ComparacionPropuesta {
  private readonly api = inject(ApiService);

  readonly propuestaId = input.required<string>();

  private readonly recurso = rxResource({
    params: () => this.propuestaId(),
    stream: ({ params }) => this.api.comparacion(params),
  });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));
  protected readonly comparacion = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));
  protected readonly nombreEstado = NOMBRE_ESTADO;

  /** En pantallas angostas conviene ver una sola columna. */
  protected readonly vista = signal<Vista>('Ambos');
  protected readonly vistas: Vista[] = ['Ambos', 'Original', 'Consolidado'];

  protected readonly conCambios = computed(
    () => this.comparacion()?.bloques.filter((b) => b.estado !== 'SinCambios').length ?? 0,
  );

  protected claseSegmento(segmento: Segmento, lado: 'original' | 'propuesto'): string {
    return segmento.tipo === 'Sustituido' ? `sustituido-${lado}` : segmento.tipo.toLowerCase();
  }

  recargar(): void {
    this.recurso.reload();
  }
}
