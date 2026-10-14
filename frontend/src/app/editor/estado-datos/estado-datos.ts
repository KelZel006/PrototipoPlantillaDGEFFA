import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { mensajeError } from '../../core/errores';

/** Diagnóstico del almacenamiento JSON: qué colecciones se pueden leer y cuántos registros tienen. */
@Component({
  selector: 'app-estado-datos',
  templateUrl: './estado-datos.html',
  styleUrl: './estado-datos.css',
})
export class EstadoDatos {
  private readonly api = inject(ApiService);

  private readonly saludRecurso = rxResource({ stream: () => this.api.salud() });

  protected readonly salud = computed(() => (this.saludRecurso.hasValue() ? this.saludRecurso.value() : null));
  protected readonly cargando = this.saludRecurso.isLoading;
  protected readonly error = computed(() => (this.saludRecurso.error() ? mensajeError(this.saludRecurso.error()) : null));

  protected recargar(): void {
    this.saludRecurso.reload();
  }
}
