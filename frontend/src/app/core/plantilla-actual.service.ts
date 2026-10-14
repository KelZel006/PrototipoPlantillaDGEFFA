import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, map, tap } from 'rxjs';
import { ApiService } from './api.service';
import { ContextoService } from './contexto.service';
import { Plantilla } from './modelos';

/** Plantilla que corresponde a la combinación Rubro + Sub Rubro + Tipo Documento seleccionada. */
@Injectable({ providedIn: 'root' })
export class PlantillaActualService {
  private readonly api = inject(ApiService);
  private readonly contexto = inject(ContextoService);

  private readonly recurso = rxResource({
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

  readonly plantilla = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));
  readonly cargando = this.recurso.isLoading;
  readonly error = this.recurso.error;
  /** La combinación está completa, se consultó y no tiene plantilla. */
  readonly sinPlantilla = computed(
    () => this.contexto.completo() && this.recurso.hasValue() && this.recurso.value() === null,
  );

  crear(): Observable<Plantilla> {
    const rubro = this.contexto.rubro();
    const subRubro = this.contexto.subRubro();
    const tipo = this.contexto.tipoDocumento();
    if (!rubro || !subRubro || !tipo) {
      throw new Error('La combinación de listas no está completa.');
    }
    return this.api
      .crearPlantilla({
        nombre: `${tipo.nombre} – ${subRubro.nombre}`.slice(0, 200),
        rubroId: rubro.id,
        subRubroId: subRubro.id,
        tipoDocumentoId: tipo.id,
        estado: 'Borrador',
      })
      .pipe(tap(() => this.recurso.reload()));
  }

  recargar(): void {
    this.recurso.reload();
  }
}
