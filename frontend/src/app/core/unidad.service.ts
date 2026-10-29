import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from './api.service';

const CLAVE = 'editor-resoluciones.unidad';
/** Unidad que se abre si el usuario no eligió otra. */
export const UNIDAD_PREDETERMINADA = 'exoneraciones';

function leer(): string | null {
  try {
    return localStorage.getItem(CLAVE);
  } catch {
    return null;
  }
}

/**
 * Unidades del menú (Exoneraciones, Dispensas, Registro Exonerados, Notas de Crédito y Devoluciones) y la que el usuario
 * tiene abierta. La unidad abierta se recuerda en el navegador.
 */
@Injectable({ providedIn: 'root' })
export class UnidadService {
  private readonly api = inject(ApiService);
  private readonly recurso = rxResource({ stream: () => this.api.unidades() });

  readonly unidades = computed(() =>
    (this.recurso.hasValue() ? this.recurso.value() : []).filter((u) => u.activo).sort((a, b) => a.orden - b.orden),
  );
  readonly actual = signal<string>(leer() ?? UNIDAD_PREDETERMINADA);
  readonly nombreActual = computed(() => this.nombre(this.actual()));

  nombre(id: string | null | undefined): string {
    return this.unidades().find((u) => u.id === id)?.nombre ?? id ?? '';
  }

  seleccionar(id: string | null | undefined): void {
    if (!id || id === this.actual()) {
      return;
    }
    this.actual.set(id);
    try {
      localStorage.setItem(CLAVE, id);
    } catch {
      // Sin almacenamiento del navegador: la unidad se recuerda solo durante la sesión.
    }
  }
}
