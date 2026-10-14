import { Injectable, computed, signal } from '@angular/core';
import { Rubro, SubRubro, TipoDocumento } from './modelos';

/**
 * Selección de las tres listas (Rubro, Sub Rubro y Tipo Documento).
 * Vive en un servicio para conservarse mientras el usuario navega entre secciones.
 */
@Injectable({ providedIn: 'root' })
export class ContextoService {
  readonly rubro = signal<Rubro | null>(null);
  readonly subRubro = signal<SubRubro | null>(null);
  readonly tipoDocumento = signal<TipoDocumento | null>(null);

  /** Lo marca el editor cuando hay cambios sin guardar, para advertir antes de cambiar de plantilla. */
  readonly cambiosSinGuardar = signal(false);

  /** La combinación está completa y es coherente (el sub rubro pertenece al rubro). */
  readonly completo = computed(() => {
    const rubro = this.rubro();
    const subRubro = this.subRubro();
    return !!rubro && !!subRubro && !!this.tipoDocumento() && subRubro.rubroId === rubro.id;
  });

  seleccionarRubro(rubro: Rubro | null): void {
    if (rubro?.id === this.rubro()?.id) {
      return;
    }
    this.rubro.set(rubro);
    // El sub rubro depende del rubro: al cambiar de rubro se descarta.
    this.subRubro.set(null);
  }

  seleccionarSubRubro(subRubro: SubRubro | null): void {
    if (subRubro && subRubro.rubroId !== this.rubro()?.id) {
      return;
    }
    this.subRubro.set(subRubro);
  }

  seleccionarTipoDocumento(tipo: TipoDocumento | null): void {
    this.tipoDocumento.set(tipo);
  }
}
