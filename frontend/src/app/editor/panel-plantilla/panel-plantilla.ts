import { DatePipe } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError, mensajeErrorArchivo } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { INTERLINEADOS, Interlineado, NOMBRE_ESTADO } from '../../core/modelos';
import { PlantillaActualService } from '../../core/plantilla-actual.service';
import { DisenoDocumento } from '../diseno-documento/diseno-documento';

/**
 * Propuesta abierta en el editor: su nombre, la operación (CREAR o MODIFICAR), el documento consolidado al que
 * pertenece, la vista previa en PDF y el diseño del documento.
 */
@Component({
  selector: 'app-panel-plantilla',
  imports: [DatePipe, DisenoDocumento, RouterLink],
  templateUrl: './panel-plantilla.html',
  styleUrl: './panel-plantilla.css',
})
export class PanelPlantilla {
  private readonly api = inject(ApiService);
  private readonly interaccion = inject(InteraccionService);
  private readonly contexto = inject(ContextoService);
  protected readonly actual = inject(PlantillaActualService);

  protected readonly error = computed(() => (this.actual.error() ? mensajeError(this.actual.error()) : null));

  private readonly tiposRecurso = rxResource({ stream: () => this.api.tiposDocumento() });

  /** Tipo de documento de la propuesta (por ejemplo, para avisar que la Certificación va con texto corrido). */
  protected readonly tipo = computed(() => {
    const plantilla = this.actual.plantilla();
    const tipos = this.tiposRecurso.hasValue() ? this.tiposRecurso.value() : [];
    return plantilla ? (tipos.find((t) => t.id === plantilla.tipoDocumentoId) ?? null) : null;
  });

  protected readonly nombreEstado = NOMBRE_ESTADO;

  private readonly solicitudesRecurso = rxResource({
    params: () => this.actual.plantilla()?.id,
    stream: () => this.api.solicitudes(),
  });

  private readonly bienesRecurso = rxResource({
    params: () => this.actual.plantilla()?.id,
    stream: () => this.api.detallesBienes(),
  });

  /** Cantidad de registros de la lista de bienes por solicitud: indica cuántas filas generará una tabla de lista. */
  private readonly bienesPorSolicitud = computed(() => {
    const conteo = new Map<string, number>();
    for (const bien of this.bienesRecurso.hasValue() ? this.bienesRecurso.value() : []) {
      conteo.set(bien.solicitudId, (conteo.get(bien.solicitudId) ?? 0) + 1);
    }
    return conteo;
  });

  /** Solicitudes de prueba; primero las del mismo rubro y sub rubro que la plantilla. */
  protected readonly solicitudes = computed(() => {
    const plantilla = this.actual.plantilla();
    const lista = this.solicitudesRecurso.hasValue() ? this.solicitudesRecurso.value() : [];
    return lista
      .map((s) => ({
        ...s,
        mismaCombinacion: !!plantilla && s.rubroId === plantilla.rubroId && s.subRubroId === plantilla.subRubroId,
        bienes: this.bienesPorSolicitud().get(s.id) ?? 0,
      }))
      .sort((a, b) => Number(b.mismaCombinacion) - Number(a.mismaCombinacion));
  });

  /** Por defecto, la primera solicitud de la misma combinación; '' significa "sin datos". */
  protected readonly solicitudElegida = linkedSignal(
    () => this.solicitudes().find((s) => s.mismaCombinacion)?.id ?? '',
  );
  protected readonly generando = signal(false);
  protected readonly interlineados = INTERLINEADOS;
  protected readonly interlineado = signal<Interlineado>('1.5');

  protected async generarPdf(): Promise<void> {
    const plantilla = this.actual.plantilla();
    if (!plantilla || this.generando()) {
      return;
    }
    if (
      this.contexto.cambiosSinGuardar() &&
      !(await this.interaccion.confirmar({
        titulo: 'Hay cambios sin guardar',
        mensaje: 'El PDF se genera con lo guardado. Los cambios que está editando no se incluirán.',
        aceptar: 'Generar de todos modos',
      }))
    ) {
      return;
    }

    // La ventana se abre durante el clic para que el navegador no la bloquee; luego se le asigna el PDF.
    const ventana = abrirVentana();
    this.generando.set(true);
    this.api.generarPdf(plantilla.id, this.solicitudElegida() || null, this.interlineado()).subscribe({
      next: (respuesta) => {
        this.generando.set(false);
        this.mostrarPdf(respuesta, ventana);
      },
      error: async (error: unknown) => {
        this.generando.set(false);
        ventana?.close();
        this.interaccion.avisar('error', `No se pudo generar el PDF. ${await mensajeErrorArchivo(error)}`, 8000);
      },
    });
  }

  private mostrarPdf(respuesta: HttpResponse<Blob>, ventana: Window | null): void {
    const archivo = respuesta.body;
    if (!archivo) {
      ventana?.close();
      this.interaccion.avisar('error', 'El servidor no devolvió el PDF.', 8000);
      return;
    }
    const url = URL.createObjectURL(archivo);
    if (ventana) {
      ventana.location.href = url;
    } else {
      // El navegador bloqueó la ventana: se descarga el archivo.
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = nombreArchivo(respuesta) ?? 'propuesta.pdf';
      enlace.click();
    }
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
    this.interaccion.avisar('exito', 'Vista previa en PDF generada.');
  }
}

function abrirVentana(): Window | null {
  try {
    return window.open('', '_blank');
  } catch {
    return null;
  }
}

/** Nombre sugerido por el servidor en Content-Disposition. */
function nombreArchivo(respuesta: HttpResponse<Blob>): string | null {
  const disposicion = respuesta.headers.get('Content-Disposition') ?? '';
  const coincidencia = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposicion);
  return coincidencia ? decodeURIComponent(coincidencia[1]) : null;
}
