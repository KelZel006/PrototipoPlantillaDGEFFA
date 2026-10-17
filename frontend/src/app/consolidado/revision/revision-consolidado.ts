import { Component, ElementRef, computed, inject, input, linkedSignal, signal, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { mensajeError } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { DatosObservacion, Propuesta } from '../../core/modelos';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { ComparacionPropuesta } from '../comparacion/comparacion-propuesta';
import { HistorialRevisiones } from '../historial-revisiones';
import { InsigniaEstado } from '../insignia-estado';

interface ObservacionBorrador {
  propuestaId: string;
  bloqueCodigo: string;
  problema: string;
  recomendacion: string;
}

/**
 * Pantalla D: revisión jurídica (/consolidados/:id/revision). A la izquierda, las plantillas del consolidado con sus
 * ajustes; al centro, la comparación lado a lado de la seleccionada. Desde aquí se devuelve a registro (pantalla E, con
 * justificación obligatoria) o se aprueba (Fase 6). Solo para el Oficial Jurídico, con el documento VERIFICADO.
 */
@Component({
  selector: 'app-revision-consolidado',
  imports: [RouterLink, ComparacionPropuesta, HistorialRevisiones, InsigniaEstado],
  templateUrl: './revision-consolidado.html',
  styleUrl: './revision-consolidado.css',
})
export class RevisionConsolidado {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly interaccion = inject(InteraccionService);
  protected readonly usuario = inject(UsuarioPruebaService);

  /** Identificador de la ruta. */
  readonly id = input.required<string>();

  private readonly recurso = rxResource({
    params: () => ({ id: this.id(), usuario: this.usuario.id() }),
    stream: ({ params }) => this.api.consolidado(params.id),
  });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));
  protected readonly detalle = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));
  protected readonly activas = computed(() => this.detalle()?.propuestas.filter((p) => !p.retirada) ?? []);

  /** La revisión es del Oficial Jurídico y solo con el documento VERIFICADO (el servidor también lo valida). */
  protected readonly puedeRevisar = computed(
    () => this.detalle()?.consolidado.estado === 'Verificado' && this.usuario.tiene('OficialJuridico'),
  );

  protected readonly seleccionada = linkedSignal<Propuesta | null>(() => this.activas()[0] ?? null);

  protected ajustes(propuestaId: string) {
    return this.detalle()?.ajustes[propuestaId] ?? [];
  }

  // ---------- Pantalla E: devolución ----------

  private readonly dialogo = viewChild<ElementRef<HTMLDialogElement>>('dialogoDevolucion');
  protected readonly justificacion = signal('');
  protected readonly observaciones = signal<ObservacionBorrador[]>([]);
  protected readonly enviando = signal(false);
  protected readonly errorDevolucion = signal<string | null>(null);
  protected readonly minimoJustificacion = 10;

  protected readonly justificacionValida = computed(() => this.justificacion().trim().length >= this.minimoJustificacion);
  protected readonly observacionesValidas = computed(() =>
    this.observaciones().every((o) => o.propuestaId && o.problema.trim().length > 0),
  );

  protected bloquesDe(propuestaId: string): string[] {
    return this.activas().find((p) => p.id === propuestaId)?.contenidoPropuesto?.bloques.map((b) => b.codigo) ?? [];
  }

  protected abrirDevolucion(): void {
    this.justificacion.set('');
    this.observaciones.set([]);
    this.errorDevolucion.set(null);
    const elemento = this.dialogo()?.nativeElement;
    if (elemento && typeof elemento.showModal === 'function') {
      elemento.showModal();
    } else {
      elemento?.setAttribute('open', '');
    }
  }

  protected cerrarDevolucion(evento?: Event): void {
    evento?.preventDefault();
    if (this.enviando()) {
      return;
    }
    const elemento = this.dialogo()?.nativeElement;
    elemento?.close?.();
    elemento?.removeAttribute('open');
  }

  protected agregarObservacion(): void {
    const propuesta = this.seleccionada()?.id ?? this.activas()[0]?.id ?? '';
    this.observaciones.update((lista) => [...lista, { propuestaId: propuesta, bloqueCodigo: '', problema: '', recomendacion: '' }]);
  }

  protected cambiarObservacion(indice: number, cambios: Partial<ObservacionBorrador>): void {
    this.observaciones.update((lista) =>
      lista.map((o, i) => (i === indice ? { ...o, ...cambios, ...(cambios.propuestaId ? { bloqueCodigo: '' } : {}) } : o)),
    );
  }

  protected quitarObservacion(indice: number): void {
    this.observaciones.update((lista) => lista.filter((_, i) => i !== indice));
  }

  protected async devolver(): Promise<void> {
    const detalle = this.detalle();
    if (!detalle || !this.justificacionValida() || !this.observacionesValidas() || this.enviando()) {
      return;
    }
    const aceptado = await this.interaccion.confirmar({
      titulo: `Devolver ${detalle.consolidado.numero} a registro`,
      mensaje: `El documento vuelve a la bandeja de ${detalle.consolidado.analistaId} con su justificación${
        this.observaciones().length ? ` y ${this.observaciones().length} observaciones` : ''
      }. La devolución queda registrada y no se puede modificar.`,
      aceptar: 'Devolver a registro',
      peligro: true,
    });
    if (!aceptado) {
      return;
    }
    const observaciones: DatosObservacion[] = this.observaciones().map((o) => ({
      propuestaId: o.propuestaId,
      bloqueCodigo: o.bloqueCodigo || null,
      problema: o.problema.trim(),
      recomendacion: o.recomendacion.trim() || null,
    }));
    this.enviando.set(true);
    this.api.devolverARegistro(detalle.consolidado.id, this.justificacion().trim(), observaciones).subscribe({
      next: () => {
        this.enviando.set(false);
        this.cerrarDevolucion();
        this.interaccion.avisar('exito', `${detalle.consolidado.numero} se devolvió a registro.`);
        void this.router.navigate(['/consolidados']);
      },
      error: (error: unknown) => {
        this.enviando.set(false);
        this.errorDevolucion.set(mensajeError(error));
      },
    });
  }

  // ---------- Aprobación ----------

  protected readonly aprobando = signal(false);

  /**
   * VERIFICADO → APROBADO. El servidor publica todas las plantillas en una sola operación: las modificadas quedan con
   * una versión nueva VIGENTE (la anterior pasa a NO VIGENTE) y las nuevas, con su versión 1.
   */
  protected async aprobar(): Promise<void> {
    const detalle = this.detalle();
    if (!detalle || this.aprobando()) {
      return;
    }
    const plantillas = this.activas().length;
    const aceptado = await this.interaccion.confirmar({
      titulo: `Aprobar ${detalle.consolidado.numero}`,
      mensaje: `Se publicarán ${plantillas} ${plantillas === 1 ? 'plantilla' : 'plantillas'} como VIGENTES. Las versiones que reemplazan pasan a NO VIGENTES y se conservan. El documento quedará APROBADO y no podrá modificarse.`,
      aceptar: 'Aprobar y publicar',
    });
    if (!aceptado) {
      return;
    }
    this.aprobando.set(true);
    this.api.aprobar(detalle.consolidado.id).subscribe({
      next: (resultado) => {
        this.aprobando.set(false);
        this.interaccion.avisar(
          'exito',
          `${resultado.consolidado.numero} aprobado: ${resultado.publicadas.length} ${resultado.publicadas.length === 1 ? 'plantilla publicada' : 'plantillas publicadas'}.`,
          6000,
        );
        void this.router.navigate(['/consolidados', detalle.consolidado.id]);
      },
      error: (error: unknown) => {
        this.aprobando.set(false);
        this.interaccion.avisar('error', `No se aprobó. ${mensajeError(error)}`, 10000);
        this.recurso.reload();
      },
    });
  }

  protected recargar(): void {
    this.recurso.reload();
  }
}
