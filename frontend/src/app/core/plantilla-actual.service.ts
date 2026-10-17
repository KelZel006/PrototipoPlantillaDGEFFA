import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from './api.service';
import { PlantillaEdicion } from './modelos';

/** Plantilla que se está editando: la propuesta abierta en el editor (su identidad, diseño y si se puede editar). */
@Injectable({ providedIn: 'root' })
export class PlantillaActualService {
  private readonly api = inject(ApiService);

  /** Propuesta abierta; la fija la pantalla del editor según la ruta. */
  readonly propuestaId = signal<string | null>(null);

  private readonly recurso = rxResource({
    params: () => this.propuestaId() ?? undefined,
    stream: ({ params }) => this.api.edicion(params),
  });

  readonly plantilla = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));
  readonly cargando = this.recurso.isLoading;
  readonly error = this.recurso.error;

  abrir(propuestaId: string | null): void {
    this.propuestaId.set(propuestaId);
  }

  /** Usa la versión que devolvió el servidor tras modificar la propuesta (por ejemplo, al cargar el logo). */
  reemplazar(plantilla: PlantillaEdicion): void {
    this.recurso.set(plantilla);
  }

  recargar(): void {
    this.recurso.reload();
  }
}
