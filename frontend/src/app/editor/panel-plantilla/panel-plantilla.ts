import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { PlantillaActualService } from '../../core/plantilla-actual.service';

/**
 * Plantilla de la combinación seleccionada. Si la combinación no tiene plantilla, permite crearla.
 */
@Component({
  selector: 'app-panel-plantilla',
  imports: [DatePipe],
  templateUrl: './panel-plantilla.html',
  styleUrl: './panel-plantilla.css',
})
export class PanelPlantilla {
  protected readonly contexto = inject(ContextoService);
  protected readonly actual = inject(PlantillaActualService);

  protected readonly creando = signal(false);
  protected readonly errorCreacion = signal<string | null>(null);
  protected readonly error = computed(() => (this.actual.error() ? mensajeError(this.actual.error()) : null));

  protected crearPlantilla(): void {
    if (this.creando()) {
      return;
    }
    this.creando.set(true);
    this.errorCreacion.set(null);
    this.actual.crear().subscribe({
      next: () => this.creando.set(false),
      error: (e: unknown) => {
        this.creando.set(false);
        this.errorCreacion.set(mensajeError(e));
      },
    });
  }
}
