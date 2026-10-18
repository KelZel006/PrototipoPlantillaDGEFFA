import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AlineacionLogo,
  Ajuste,
  Campo,
  ClaseDocumento,
  ComparacionPropuesta,
  DatosObservacion,
  DetalleBien,
  Devolucion,
  DetalleConsolidado,
  DocumentoConsolidado,
  Interlineado,
  OperadoresPorTipo,
  Parrafo,
  PlantillaConVersiones,
  PlantillaEdicion,
  PlantillaVigente,
  Propuesta,
  ResultadoAprobacion,
  ResumenConsolidado,
  Rubro,
  Salud,
  Seccion,
  Solicitud,
  SubRubro,
  TipoDocumento,
  Usuario,
} from './modelos';

/** Datos de un párrafo que se envían al crear o actualizar (el identificador va en la ruta). */
export type DatosParrafo = Pick<Parrafo, 'bloque' | 'orden' | 'contenido' | 'activo' | 'mostrarSoloSiCumple' | 'unirConAnterior' | 'condicion'> & {
  version?: number;
};

/** Identidad de una plantilla nueva (operación CREAR). */
export interface SolicitudCrear {
  rubroId: string;
  subRubroId: string;
  tipoDocumentoId: string;
  claseDocumentoId: string;
  nombre?: string | null;
}

/**
 * Único punto de acceso del frontend a los datos. Angular nunca lee ni escribe los archivos JSON:
 * todo pasa por la API del backend, que valida cada operación y los permisos del usuario.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  // ---------- Catálogos ----------

  rubros(): Observable<Rubro[]> {
    return this.http.get<Rubro[]>(`${this.base}/rubros`);
  }

  subRubros(rubroId: string): Observable<SubRubro[]> {
    return this.http.get<SubRubro[]>(`${this.base}/subrubros`, { params: { rubroId } });
  }

  tiposDocumento(): Observable<TipoDocumento[]> {
    return this.http.get<TipoDocumento[]>(`${this.base}/tipos-documento`);
  }

  clasesDocumento(): Observable<ClaseDocumento[]> {
    return this.http.get<ClaseDocumento[]>(`${this.base}/clases-documento`);
  }

  usuarios(): Observable<Usuario[]> {
    return this.http.get<Usuario[]>(`${this.base}/usuarios`);
  }

  // ---------- Documentos Consolidados ----------

  consolidados(): Observable<ResumenConsolidado[]> {
    return this.http.get<ResumenConsolidado[]>(`${this.base}/consolidados`);
  }

  consolidado(id: string): Observable<DetalleConsolidado> {
    return this.http.get<DetalleConsolidado>(`${this.base}/consolidados/${encodeURIComponent(id)}`);
  }

  /** El número PLAN, el analista y las fechas los asigna el servidor. */
  crearConsolidado(): Observable<DocumentoConsolidado> {
    return this.http.post<DocumentoConsolidado>(`${this.base}/consolidados`, null);
  }

  /** Operación CREAR. Si es una Resolución, el servidor agrega también su Certificación. */
  agregarCrear(consolidadoId: string, solicitud: SolicitudCrear): Observable<Propuesta[]> {
    return this.http.post<Propuesta[]>(`${this.base}/consolidados/${encodeURIComponent(consolidadoId)}/propuestas/crear`, solicitud);
  }

  /** Operación MODIFICAR de una plantilla vigente. Si es una Resolución, también su Certificación. */
  agregarModificar(consolidadoId: string, plantillaId: string): Observable<Propuesta[]> {
    return this.http.post<Propuesta[]>(`${this.base}/consolidados/${encodeURIComponent(consolidadoId)}/propuestas/modificar`, {
      plantillaId,
    });
  }

  /** EN REGISTRO → VERIFICADO: el documento pasa a la bandeja del Oficial Jurídico. */
  enviarAVerificacion(consolidadoId: string): Observable<DocumentoConsolidado> {
    return this.http.post<DocumentoConsolidado>(`${this.base}/consolidados/${encodeURIComponent(consolidadoId)}/enviar`, null);
  }

  /** VERIFICADO → EN REGISTRO, con justificación obligatoria y observaciones. */
  devolverARegistro(consolidadoId: string, justificacion: string, observaciones: DatosObservacion[]): Observable<Devolucion> {
    return this.http.post<Devolucion>(`${this.base}/consolidados/${encodeURIComponent(consolidadoId)}/devolver`, {
      justificacion,
      observaciones,
    });
  }

  /** VERIFICADO → APROBADO: publica todas las plantillas en una sola operación. */
  aprobar(consolidadoId: string): Observable<ResultadoAprobacion> {
    return this.http.post<ResultadoAprobacion>(`${this.base}/consolidados/${encodeURIComponent(consolidadoId)}/aprobar`, null);
  }

  /** Consolidado de plantillas: cada plantilla con todas sus versiones (pantalla F). */
  consolidadoDePlantillas(): Observable<PlantillaConVersiones[]> {
    return this.http.get<PlantillaConVersiones[]>(`${this.base}/consolidado-plantillas`);
  }

  /** PDF de una versión publicada (vigente o no). */
  pdfVersion(versionId: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/versiones-plantilla/${encodeURIComponent(versionId)}/pdf`, {
      responseType: 'blob',
      observe: 'response',
    });
  }

  retirarPropuesta(propuestaId: string, motivo: string): Observable<Propuesta[]> {
    return this.http.post<Propuesta[]>(`${this.propuesta(propuestaId)}/retirar`, { motivo });
  }

  plantillasVigentes(): Observable<PlantillaVigente[]> {
    return this.http.get<PlantillaVigente[]>(`${this.base}/plantillas-vigentes`);
  }

  // ---------- Edición de una propuesta ----------

  edicion(propuestaId: string): Observable<PlantillaEdicion> {
    return this.http.get<PlantillaEdicion>(`${this.propuesta(propuestaId)}/edicion`);
  }

  ajustes(propuestaId: string): Observable<Ajuste[]> {
    return this.http.get<Ajuste[]>(`${this.propuesta(propuestaId)}/ajustes`);
  }

  /** Comparación con la versión original: bloques, párrafos con diferencias palabra por palabra y diseño. */
  comparacion(propuestaId: string): Observable<ComparacionPropuesta> {
    return this.http.get<ComparacionPropuesta>(`${this.propuesta(propuestaId)}/comparacion`);
  }

  actualizarDiseno(
    propuestaId: string,
    diseno: { alineacionLogo: AlineacionLogo; piePagina: string | null; espacioFirmasCm: number },
  ): Observable<PlantillaEdicion> {
    return this.http.put<PlantillaEdicion>(`${this.propuesta(propuestaId)}/diseno`, diseno);
  }

  /** Carga o reemplaza el logo del encabezado (PNG o JPEG, hasta 1 MB). */
  cargarLogo(propuestaId: string, archivo: File): Observable<PlantillaEdicion> {
    const formulario = new FormData();
    formulario.append('archivo', archivo, archivo.name);
    return this.http.post<PlantillaEdicion>(`${this.propuesta(propuestaId)}/logo`, formulario);
  }

  quitarLogo(propuestaId: string): Observable<PlantillaEdicion> {
    return this.http.delete<PlantillaEdicion>(`${this.propuesta(propuestaId)}/logo`);
  }

  /** Dirección de un archivo cargado (por ejemplo, el logo) para mostrarlo. */
  urlArchivo(archivoId: string): string {
    return `${this.base}/archivos/${encodeURIComponent(archivoId)}`;
  }

  parrafos(propuestaId: string, bloque?: Seccion): Observable<Parrafo[]> {
    const params: Record<string, string> = bloque ? { bloque } : {};
    return this.http.get<Parrafo[]>(`${this.propuesta(propuestaId)}/parrafos`, { params });
  }

  crearParrafo(propuestaId: string, datos: DatosParrafo): Observable<Parrafo> {
    return this.http.post<Parrafo>(`${this.propuesta(propuestaId)}/parrafos`, datos);
  }

  actualizarParrafo(parrafo: Parrafo): Observable<Parrafo> {
    return this.http.put<Parrafo>(`${this.propuesta(parrafo.propuestaId)}/parrafos/${encodeURIComponent(parrafo.id)}`, parrafo);
  }

  /** Se envía la versión para no borrar un párrafo que cambió en otra pestaña. */
  eliminarParrafo(parrafo: Parrafo): Observable<void> {
    return this.http.delete<void>(`${this.propuesta(parrafo.propuestaId)}/parrafos/${encodeURIComponent(parrafo.id)}`, {
      params: { version: parrafo.version },
    });
  }

  duplicarParrafo(parrafo: Parrafo): Observable<Parrafo> {
    return this.http.post<Parrafo>(`${this.propuesta(parrafo.propuestaId)}/parrafos/${encodeURIComponent(parrafo.id)}/duplicar`, null);
  }

  /** Guarda el orden completo de una sección en una sola operación. */
  reordenarParrafos(propuestaId: string, bloque: Seccion, parrafos: Parrafo[]): Observable<Parrafo[]> {
    return this.http.post<Parrafo[]>(`${this.propuesta(propuestaId)}/parrafos/reordenar`, {
      bloque,
      elementos: parrafos.map((p) => ({ id: p.id, version: p.version })),
    });
  }

  /**
   * Vista previa en PDF de la propuesta (Cabecera, Considerando, Por Tanto, Resuelve y Firmas). Con una solicitud se
   * sustituyen los campos y se aplican las condiciones.
   */
  generarPdf(
    propuestaId: string,
    solicitudId: string | null,
    interlineado: Interlineado = '1.5',
  ): Observable<HttpResponse<Blob>> {
    const params: Record<string, string> = solicitudId ? { solicitudId, interlineado } : { interlineado };
    return this.http.get(`${this.propuesta(propuestaId)}/pdf`, {
      params,
      responseType: 'blob',
      observe: 'response',
    });
  }

  // ---------- Campos y datos de prueba ----------

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

  detallesBienes(): Observable<DetalleBien[]> {
    return this.http.get<DetalleBien[]>(`${this.base}/detalles-bienes`);
  }

  solicitudes(): Observable<Solicitud[]> {
    return this.http.get<Solicitud[]>(`${this.base}/solicitudes`);
  }

  salud(): Observable<Salud> {
    return this.http.get<Salud>(`${this.base}/salud`);
  }

  private propuesta(id: string): string {
    return `${this.base}/propuestas/${encodeURIComponent(id)}`;
  }
}
