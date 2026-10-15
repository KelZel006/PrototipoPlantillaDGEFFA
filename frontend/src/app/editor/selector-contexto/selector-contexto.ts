import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';

/** Las tres listas superiores: Rubro, Sub Rubro (depende del rubro) y Tipo Documento. */
@Component({
  selector: 'app-selector-contexto',
  templateUrl: './selector-contexto.html',
  styleUrl: './selector-contexto.css',
})
export class SelectorContexto {
  private readonly api = inject(ApiService);
  protected readonly contexto = inject(ContextoService);
  private readonly interaccion = inject(InteraccionService);

  private readonly rubrosRecurso = rxResource({ stream: () => this.api.rubros() });
  private readonly tiposRecurso = rxResource({ stream: () => this.api.tiposDocumento() });
  private readonly subRubrosRecurso = rxResource({
    params: () => this.contexto.rubro()?.id,
    stream: ({ params: rubroId }) => this.api.subRubros(rubroId),
  });

  protected readonly rubros = computed(() =>
    this.rubrosRecurso.hasValue() ? this.rubrosRecurso.value().filter((r) => r.activo) : [],
  );
  protected readonly tiposDocumento = computed(() =>
    this.tiposRecurso.hasValue() ? this.tiposRecurso.value().filter((t) => t.activo) : [],
  );
  protected readonly subRubros = computed(() =>
    this.subRubrosRecurso.hasValue() ? this.subRubrosRecurso.value().filter((s) => s.activo) : [],
  );

  protected readonly cargandoSubRubros = this.subRubrosRecurso.isLoading;

  protected readonly error = computed(() => {
    const fallido = [this.rubrosRecurso, this.tiposRecurso, this.subRubrosRecurso].find((r) => r.error());
    return fallido ? mensajeError(fallido.error()) : null;
  });

  protected async alCambiarRubro(evento: Event): Promise<void> {
    const id = await this.confirmarCambio(evento, this.contexto.rubro()?.id);
    if (id !== null) {
      this.contexto.seleccionarRubro(this.rubros().find((r) => r.id === id) ?? null);
    }
  }

  protected async alCambiarSubRubro(evento: Event): Promise<void> {
    const id = await this.confirmarCambio(evento, this.contexto.subRubro()?.id);
    if (id !== null) {
      this.contexto.seleccionarSubRubro(this.subRubros().find((s) => s.id === id) ?? null);
    }
  }

  protected async alCambiarTipoDocumento(evento: Event): Promise<void> {
    const id = await this.confirmarCambio(evento, this.contexto.tipoDocumento()?.id);
    if (id !== null) {
      this.contexto.seleccionarTipoDocumento(this.tiposDocumento().find((t) => t.id === id) ?? null);
    }
  }

  /**
   * Cambiar de combinación cambia la plantilla en edición. Si hay cambios sin guardar, se pide confirmación;
   * si el usuario cancela, la lista vuelve a su valor anterior. Devuelve el nuevo id o null si se canceló.
   */
  private async confirmarCambio(evento: Event, actual: string | undefined): Promise<string | null> {
    const lista = evento.target as HTMLSelectElement;
    const nuevo = lista.value;
    if (!this.contexto.cambiosSinGuardar()) {
      return nuevo;
    }
    const aceptado = await this.interaccion.confirmar({
      titulo: 'Cambios sin guardar',
      mensaje: 'El párrafo que está editando tiene cambios sin guardar. Si cambia de plantilla, se perderán.',
      aceptar: 'Descartar cambios',
      peligro: true,
    });
    if (!aceptado) {
      lista.value = actual ?? '';
      return null;
    }
    this.contexto.descartarCambios();
    return nuevo;
  }

  protected recargar(): void {
    this.rubrosRecurso.reload();
    this.tiposRecurso.reload();
    this.subRubrosRecurso.reload();
  }
}
