import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Campo,
  Nuevo,
  Parrafo,
  Plantilla,
  Rubro,
  Salud,
  Seccion,
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

  campos(): Observable<Campo[]> {
    return this.http.get<Campo[]>(`${this.base}/campos`);
  }

  salud(): Observable<Salud> {
    return this.http.get<Salud>(`${this.base}/salud`);
  }
}
