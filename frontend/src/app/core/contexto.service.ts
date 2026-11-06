import { Injectable, computed, signal } from '@angular/core';
import { Beneficio, ClaseDocumento, Rubro, Seccion, SubRubro, TipoDocumento } from './modelos';

/**
 * Selección de las cinco listas (Rubro, Sub Rubro, Tipo Documento, Documento y Beneficios). Rubro y Sub Rubro son
 * obligatorios; las demás son opcionales.
 * Vive en un servicio para conservarse mientras el usuario navega entre secciones.
 */
@Injectable({ providedIn: 'root' })
export class ContextoService {
  readonly rubro = signal<Rubro | null>(null);
  readonly subRubro = signal<SubRubro | null>(null);
  readonly tipoDocumento = signal<TipoDocumento | null>(null);
  /** Lista "Documento": Original, Modificación o Aclaratoria. */
  readonly claseDocumento = signal<ClaseDocumento | null>(null);
  /** Lista "Beneficios": ISV, ISR, ACPV o Aportación Solidaria. */
  readonly beneficio = signal<Beneficio | null>(null);

  /** Secciones con cambios sin guardar; las marca cada editor de sección. */
  readonly seccionesConCambios = signal<ReadonlySet<Seccion>>(new Set());

  /** Hay cambios sin guardar en alguna sección: se advierte antes de cambiar de plantilla o generar el PDF. */
  readonly cambiosSinGuardar = computed(() => this.seccionesConCambios().size > 0);

  /** La plantilla elegida es copia de otra (la Certificación se copia de la Resolución) y no se edita aquí. */
  readonly esCopia = computed(() => !!this.tipoDocumento()?.copiaDeTipoDocumentoId);

  marcarCambios(seccion: Seccion, conCambios: boolean): void {
    this.seccionesConCambios.update((actuales) => {
      if (actuales.has(seccion) === conCambios) {
        return actuales;
      }
      const nuevas = new Set(actuales);
      if (conCambios) {
        nuevas.add(seccion);
      } else {
        nuevas.delete(seccion);
      }
      return nuevas;
    });
  }

  /** El usuario aceptó descartar los cambios; los editores vuelven a empezar al cambiar la plantilla. */
  descartarCambios(): void {
    this.seccionesConCambios.set(new Set());
  }

  /** La combinación tiene lo obligatorio, Rubro y Sub Rubro, y es coherente (el sub rubro pertenece al rubro). */
  readonly completo = computed(() => {
    const rubro = this.rubro();
    const subRubro = this.subRubro();
    return !!rubro && !!subRubro && subRubro.rubroId === rubro.id;
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

  seleccionarClaseDocumento(clase: ClaseDocumento | null): void {
    this.claseDocumento.set(clase);
  }

  seleccionarBeneficio(beneficio: Beneficio | null): void {
    this.beneficio.set(beneficio);
  }
}
