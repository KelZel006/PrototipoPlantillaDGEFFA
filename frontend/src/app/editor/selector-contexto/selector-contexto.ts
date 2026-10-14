import { Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';

/** Las tres listas superiores: Rubro, Sub Rubro (depende del rubro) y Tipo Documento. */
@Component({
  selector: 'app-selector-contexto',
  templateUrl: './selector-contexto.html',
  styleUrl: './selector-contexto.css',
})
export class SelectorContexto {
  private readonly api = inject(ApiService);
  protected readonly contexto = inject(ContextoService);

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

  protected alCambiarRubro(id: string): void {
    this.contexto.seleccionarRubro(this.rubros().find((r) => r.id === id) ?? null);
  }

  protected alCambiarSubRubro(id: string): void {
    this.contexto.seleccionarSubRubro(this.subRubros().find((s) => s.id === id) ?? null);
  }

  protected alCambiarTipoDocumento(id: string): void {
    this.contexto.seleccionarTipoDocumento(this.tiposDocumento().find((t) => t.id === id) ?? null);
  }

  protected recargar(): void {
    this.rubrosRecurso.reload();
    this.tiposRecurso.reload();
    this.subRubrosRecurso.reload();
  }
}
