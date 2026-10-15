import { HttpErrorResponse } from '@angular/common/http';
import { DetalleProblema } from './modelos';

/**
 * Igual que mensajeError, para peticiones que esperan un archivo: el detalle del error llega como Blob.
 */
export async function mensajeErrorArchivo(error: unknown): Promise<string> {
  if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
    try {
      const detalle = JSON.parse(await error.error.text()) as DetalleProblema;
      return mensajeError(new HttpErrorResponse({ status: error.status, error: detalle }));
    } catch {
      // El cuerpo no era JSON: se usa el mensaje genérico.
    }
  }
  return mensajeError(error);
}

/** Convierte un error de la API en un mensaje comprensible en español. */
export function mensajeError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Ocurrió un error inesperado.';
  }
  if (error.status === 0) {
    return 'No se pudo conectar con el servidor. Verifique que la API esté en ejecución.';
  }

  const problema = error.error as DetalleProblema | null;
  if (problema && typeof problema === 'object') {
    const detalles = problema.errors ? Object.values(problema.errors).flat() : [];
    const partes = [problema.title, problema.detail, ...detalles].filter((p): p is string => !!p);
    if (partes.length > 0) {
      return [...new Set(partes)].join(' ');
    }
  }
  return `El servidor respondió con el error ${error.status}.`;
}
