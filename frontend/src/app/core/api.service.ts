import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Campo,
  Nuevo,
  OperadoresPorTipo,
  Parrafo,
  Plantilla,
  Rubro,
  Salud,
  Seccion,
  Solicitud,
  SubRubro,
  TipoDocumento,
} from './modelos';

/**
 * Único punto de acceso del frontend a los datos. Angular nunca lee ni escribe los archivos JSON:
 * todo pasa por la API del backend, que valida cada operación.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  rubros(): Observable<Rubro[]> {
    return this.http.get<Rubro[]>(`${this.base}/rubros`);
  }

  subRubros(rubroId: string): Observable<SubRubro[]> {
    return this.http.get<SubRubro[]>(`${this.base}/subrubros`, { params: { rubroId } });
  }

  tiposDocumento(): Observable<TipoDocumento[]> {
    return this.http.get<TipoDocumento[]>(`${this.base}/tipos-documento`);
  }

  plantillas(filtro: { rubroId: string; subRubroId: string; tipoDocumentoId: string }): Observable<Plantilla[]> {
    return this.http.get<Plantilla[]>(`${this.base}/plantillas`, { params: filtro });
  }

  crearPlantilla(plantilla: Nuevo<Plantilla>): Observable<Plantilla> {
    return this.http.post<Plantilla>(`${this.base}/plantillas`, plantilla);
  }

  parrafos(plantillaId: string, seccion?: Seccion): Observable<Parrafo[]> {
    const params: Record<string, string> = { plantillaId };
    if (seccion) {
      params['seccion'] = seccion;
    }
    return this.http.get<Parrafo[]>(`${this.base}/parrafos`, { params });
  }

  crearParrafo(parrafo: Nuevo<Parrafo>): Observable<Parrafo> {
    return this.http.post<Parrafo>(`${this.base}/parrafos`, parrafo);
  }

  actualizarParrafo(parrafo: Parrafo): Observable<Parrafo> {
    return this.http.put<Parrafo>(`${this.base}/parrafos/${encodeURIComponent(parrafo.id)}`, parrafo);
  }

  /** Se envía la versión para no borrar un párrafo que otra persona modificó. */
  eliminarParrafo(parrafo: Parrafo): Observable<void> {
    return this.http.delete<void>(`${this.base}/parrafos/${encodeURIComponent(parrafo.id)}`, {
      params: { version: parrafo.version },
    });
  }

  duplicarParrafo(id: string): Observable<Parrafo> {
    return this.http.post<Parrafo>(`${this.base}/parrafos/${encodeURIComponent(id)}/duplicar`, null);
  }

  /** Guarda el orden completo de una sección en una sola operación. */
  reordenarParrafos(plantillaId: string, seccion: Seccion, parrafos: Parrafo[]): Observable<Parrafo[]> {
    return this.http.post<Parrafo[]>(`${this.base}/parrafos/reordenar`, {
      plantillaId,
      seccion,
      elementos: parrafos.map((p) => ({ id: p.id, version: p.version })),
    });
  }

  campos(): Observable<Campo[]> {
    return this.http.get<Campo[]>(`${this.base}/campos`);
  }

  operadores(): Observable<OperadoresPorTipo> {
    return this.http.get<OperadoresPorTipo>(`${this.base}/campos/operadores`);
  }

  /** Valores que tiene el campo en los datos de prueba (sugerencias para las condiciones). */
  valoresCampo(campoId: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/campos/${encodeURIComponent(campoId)}/valores`);
  }

  solicitudes(): Observable<Solicitud[]> {
    return this.http.get<Solicitud[]>(`${this.base}/solicitudes`);
  }

  /**
   * PDF de prueba de la plantilla (Cabecera, Considerando y Resuelve). Con una solicitud se sustituyen
   * los campos y se aplican las condiciones.
   */
  generarPdf(plantillaId: string, solicitudId: string | null): Observable<HttpResponse<Blob>> {
    const params: Record<string, string> = solicitudId ? { solicitudId } : {};
    return this.http.get(`${this.base}/plantillas/${encodeURIComponent(plantillaId)}/pdf`, {
      params,
      responseType: 'blob',
      observe: 'response',
    });
  }

  salud(): Observable<Salud> {
    return this.http.get<Salud>(`${this.base}/salud`);
  }
}
