import { Injectable, signal } from '@angular/core';

export interface SolicitudConfirmacion {
  titulo: string;
  mensaje: string;
  aceptar: string;
  peligro?: boolean;
}

export interface Aviso {
  id: number;
  tipo: 'exito' | 'error';
  texto: string;
}

/** Diálogos de confirmación (se responden con una promesa) y avisos breves de la aplicación. */
@Injectable({ providedIn: 'root' })
export class InteraccionService {
  readonly confirmacion = signal<SolicitudConfirmacion | null>(null);
  readonly avisos = signal<Aviso[]>([]);

  private resolver: ((aceptado: boolean) => void) | null = null;
  private siguienteAviso = 1;

  confirmar(solicitud: SolicitudConfirmacion): Promise<boolean> {
    this.resolver?.(false);
    this.confirmacion.set(solicitud);
    return new Promise((resolver) => (this.resolver = resolver));
  }

  responder(aceptado: boolean): void {
    const resolver = this.resolver;
    this.resolver = null;
    this.confirmacion.set(null);
    resolver?.(aceptado);
  }

  avisar(tipo: Aviso['tipo'], texto: string, duracionMs = 4000): void {
    const aviso = { id: this.siguienteAviso++, tipo, texto };
    this.avisos.update((lista) => [...lista, aviso]);
    if (duracionMs > 0) {
      setTimeout(() => this.cerrarAviso(aviso.id), duracionMs);
    }
  }

  cerrarAviso(id: number): void {
    this.avisos.update((lista) => lista.filter((a) => a.id !== id));
  }
}
