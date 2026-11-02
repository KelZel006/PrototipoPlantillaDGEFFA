import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

/**
 * Muestra un PDF generado por el servidor en una pestaña nueva. La pestaña se abre antes de pedir el archivo para que
 * el navegador no la bloquee; si aun así la bloquea, el PDF se descarga. Devuelve el error del servidor, si lo hubo.
 */
export function abrirPdf(peticion: Observable<HttpResponse<Blob>>, nombre: string): Promise<unknown | null> {
  let ventana: Window | null = null;
  try {
    ventana = window.open('', '_blank');
  } catch {
    ventana = null;
  }
  return new Promise((resolver) => {
    peticion.subscribe({
      next: (respuesta) => {
        const archivo = respuesta.body;
        if (!archivo) {
          ventana?.close();
          resolver(null);
          return;
        }
        const url = URL.createObjectURL(archivo);
        if (ventana) {
          ventana.location.href = url;
        } else {
          const enlace = document.createElement('a');
          enlace.href = url;
          enlace.download = `${nombre}.pdf`;
          enlace.click();
        }
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
        resolver(null);
      },
      error: (error: unknown) => {
        ventana?.close();
        resolver(error);
      },
    });
  });
}
