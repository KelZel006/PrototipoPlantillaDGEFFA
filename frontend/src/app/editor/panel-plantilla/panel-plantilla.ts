import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { SECCIONES } from '../../core/modelos';

/**
 * Plantilla de la combinación seleccionada y el resumen de sus secciones.
 * Si la combinación no tiene plantilla, permite crearla (escritura a través de la API).
 */
@Component({
  selector: 'app-panel-plantilla',
  imports: [DatePipe],
  templateUrl: './panel-plantilla.html',
  styleUrl: './panel-plantilla.css',
})
export class PanelPlantilla {
  private readonly api = inject(ApiService);
  protected readonly contexto = inject(ContextoService);

  protected readonly creando = signal(false);
  protected readonly errorCreacion = signal<string | null>(null);

  private readonly plantillaRecurso = rxResource({
    params: () => {
      const rubro = this.contexto.rubro();
      const subRubro = this.contexto.subRubro();
      const tipo = this.contexto.tipoDocumento();
      return this.contexto.completo() && rubro && subRubro && tipo
        ? { rubroId: rubro.id, subRubroId: subRubro.id, tipoDocumentoId: tipo.id }
        : undefined;
    },
    stream: ({ params }) => this.api.plantillas(params).pipe(map((lista) => lista[0] ?? null)),
  });

  private readonly parrafosRecurso = rxResource({
    params: () => (this.plantillaRecurso.hasValue() ? this.plantillaRecurso.value()?.id : undefined),
    stream: ({ params: plantillaId }) => this.api.parrafos(plantillaId),
  });

  protected readonly cargando = computed(() => this.plantillaRecurso.isLoading() || this.parrafosRecurso.isLoading());
  protected readonly plantilla = computed(() => (this.plantillaRecurso.hasValue() ? this.plantillaRecurso.value() : null));
  protected readonly sinPlantilla = computed(
    () => this.contexto.completo() && this.plantillaRecurso.hasValue() && this.plantillaRecurso.value() === null,
  );

  protected readonly error = computed(() => {
    const fallido = [this.plantillaRecurso, this.parrafosRecurso].find((r) => r.error());
    return fallido ? mensajeError(fallido.error()) : null;
  });

  protected readonly resumenSecciones = computed(() => {
    const parrafos = this.parrafosRecurso.hasValue() ? this.parrafosRecurso.value() : [];
    return SECCIONES.map((seccion) => {
      const deSeccion = parrafos.filter((p) => p.seccion === seccion);
      return {
        seccion,
        total: deSeccion.length,
        activos: deSeccion.filter((p) => p.activo).length,
        conCondicion: deSeccion.filter((p) => !!p.condicionId).length,
      };
    });
  });

  protected crearPlantilla(): void {
    const rubro = this.contexto.rubro();
    const subRubro = this.contexto.subRubro();
    const tipo = this.contexto.tipoDocumento();
    if (!rubro || !subRubro || !tipo || this.creando()) {
      return;
    }

    this.creando.set(true);
    this.errorCreacion.set(null);
    this.api
      .crearPlantilla({
        nombre: `${tipo.nombre} – ${subRubro.nombre}`.slice(0, 200),
        rubroId: rubro.id,
        subRubroId: subRubro.id,
        tipoDocumentoId: tipo.id,
        estado: 'Borrador',
      })
      .subscribe({
        next: () => {
          this.creando.set(false);
          this.plantillaRecurso.reload();
        },
        error: (e: unknown) => {
          this.creando.set(false);
          this.errorCreacion.set(mensajeError(e));
        },
      });
  }

  protected recargar(): void {
    this.plantillaRecurso.reload();
    this.parrafosRecurso.reload();
  }
}
