import { Injectable, effect, signal } from '@angular/core';

const CLAVE_CAMPOS_VISIBLES = 'editor-resoluciones.camposVisibles';

/**
 * Preferencias de la interfaz de este navegador (por ejemplo, si el panel de campos está oculto).
 * Si el almacenamiento local no está disponible, se usan los valores predeterminados.
 */
@Injectable({ providedIn: 'root' })
export class PreferenciasService {
  readonly camposVisibles = signal(leer(CLAVE_CAMPOS_VISIBLES) !== 'no');

  constructor() {
    effect(() => escribir(CLAVE_CAMPOS_VISIBLES, this.camposVisibles() ? 'si' : 'no'));
  }
}

function leer(clave: string): string | null {
  try {
    return localStorage.getItem(clave);
  } catch {
    return null;
  }
}

function escribir(clave: string, valor: string): void {
  try {
    localStorage.setItem(clave, valor);
  } catch {
    // Sin almacenamiento local la preferencia solo dura mientras la página esté abierta.
  }
}
