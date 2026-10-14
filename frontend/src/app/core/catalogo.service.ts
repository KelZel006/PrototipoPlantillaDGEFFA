import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, catchError, of, shareReplay } from 'rxjs';
import { ApiService } from './api.service';
import { mensajeError } from './errores';
import { Campo } from './modelos';

/** Catálogo de campos dinámicos y operadores por tipo de dato, cargados una vez desde la API. */
@Injectable({ providedIn: 'root' })
export class CatalogoService {
  private readonly api = inject(ApiService);

  private readonly camposRecurso = rxResource({ stream: () => this.api.campos() });
  private readonly operadoresRecurso = rxResource({ stream: () => this.api.operadores() });
  private readonly valores = new Map<string, Observable<string[]>>();

  readonly campos = computed(() => (this.camposRecurso.hasValue() ? this.camposRecurso.value() : []));
  readonly operadores = computed(() => (this.operadoresRecurso.hasValue() ? this.operadoresRecurso.value() : null));
  readonly porId = computed(() => new Map(this.campos().map((c) => [c.id, c] as const)));
  readonly listo = computed(() => this.camposRecurso.hasValue() && this.operadoresRecurso.hasValue());
  readonly error = computed(() => {
    const fallido = [this.camposRecurso, this.operadoresRecurso].find((r) => r.error());
    return fallido ? mensajeError(fallido.error()) : null;
  });

  /** Campos que se pueden insertar en un contexto, por ejemplo "considerando". */
  camposDeContexto(contexto: string): Campo[] {
    return this.campos().filter((c) => c.contextos.includes(contexto));
  }

  /** Campos que pueden usarse en la condición de un párrafo: datos generales de la solicitud. */
  readonly camposCondicionParrafo = computed(() => this.campos().filter((c) => c.origen.coleccion === 'solicitudes'));

  etiqueta = (campoId: string): string => this.porId().get(campoId)?.etiqueta ?? campoId;

  /** Sugerencias de valores para un campo (se piden una sola vez por campo). */
  valoresDe(campoId: string): Observable<string[]> {
    let valores = this.valores.get(campoId);
    if (!valores) {
      valores = this.api.valoresCampo(campoId).pipe(
        catchError(() => of([])),
        shareReplay(1),
      );
      this.valores.set(campoId, valores);
    }
    return valores;
  }

  recargar(): void {
    this.camposRecurso.reload();
    this.operadoresRecurso.reload();
  }
}
