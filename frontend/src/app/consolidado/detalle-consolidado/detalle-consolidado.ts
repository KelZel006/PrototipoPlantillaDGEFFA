import { DatePipe } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { Component, ElementRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { mensajeError, mensajeErrorArchivo } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { Propuesta } from '../../core/modelos';
import { UnidadService } from '../../core/unidad.service';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { HistorialRevisiones } from '../historial-revisiones';
import { AvisoCampos } from '../aviso-campos';
import { InsigniaEstado } from '../insignia-estado';
import { OperacionSeleccion, SeleccionPlantilla } from '../seleccion-plantilla/seleccion-plantilla';

/**
 * Pantalla B: registro del Documento Consolidado. Muestra cada tipo documental una sola vez con sus plantillas y
 * ajustes, permite CREAR y MODIFICAR plantillas, editarlas, verlas en PDF y retirarlas. Los cambios se guardan al
 * guardar cada párrafo o el diseño; nada cambia las plantillas vigentes hasta la aprobación.
 */
@Component({
  selector: 'app-detalle-consolidado',
  imports: [DatePipe, RouterLink, InsigniaEstado, SeleccionPlantilla, HistorialRevisiones, AvisoCampos],
  templateUrl: './detalle-consolidado.html',
  styleUrl: './detalle-consolidado.css',
})
export class DetalleConsolidado {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly interaccion = inject(InteraccionService);
  protected readonly usuario = inject(UsuarioPruebaService);
  protected readonly unidades = inject(UnidadService);

  /** Identificador de la ruta (/consolidados/:id). */
  readonly id = input.required<string>();
  /** Con <c>?agregar=crear</c> (desde «+ Nueva Plantilla») se abre el diálogo para crear la plantilla. */
  readonly agregar = input<string | undefined>();

  private readonly recurso = rxResource({
    params: () => ({ id: this.id(), usuario: this.usuario.id() }),
    stream: ({ params }) => this.api.consolidado(params.id),
  });
  private readonly tiposRecurso = rxResource({ stream: () => this.api.tiposDocumento() });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));
  protected readonly detalle = computed(() => (this.recurso.hasValue() ? this.recurso.value() : null));

  /** Solo el analista dueño modifica, y solo EN REGISTRO (el servidor también lo valida). */
  protected readonly editable = computed(() => {
    const detalle = this.detalle();
    return (
      !!detalle &&
      detalle.consolidado.estado === 'EnRegistro' &&
      detalle.consolidado.analistaId === this.usuario.id() &&
      this.usuario.tiene('Analista')
    );
  });

  /** Las diferencias las revisa el Oficial Jurídico, solo con el documento VERIFICADO (el servidor también lo valida). */
  protected readonly puedeComparar = computed(
    () => this.detalle()?.consolidado.estado === 'Verificado' && this.usuario.tiene('OficialJuridico'),
  );

  protected readonly activas = computed(() => this.detalle()?.propuestas.filter((p) => !p.retirada) ?? []);
  protected readonly retiradas = computed(() => this.detalle()?.propuestas.filter((p) => p.retirada) ?? []);
  protected readonly ultimaDevolucion = computed(() => {
    const detalle = this.detalle();
    return detalle?.consolidado.estado === 'EnRegistro' ? (detalle.devoluciones.at(-1) ?? null) : null;
  });

  private readonly nombresTipo = computed(
    () => new Map((this.tiposRecurso.hasValue() ? this.tiposRecurso.value() : []).map((t) => [t.id, t.nombre] as const)),
  );

  protected nombreTipo(id: string): string {
    return this.nombresTipo().get(id) ?? id;
  }

  /** Propuestas de un tipo documental (al expandir su fila del resumen). */
  protected propuestasDeTipo(tipoDocumentoId: string): Propuesta[] {
    return this.activas().filter((p) => p.tipoDocumentoId === tipoDocumentoId);
  }

  protected ajustes(propuestaId: string) {
    return this.detalle()?.ajustes[propuestaId] ?? [];
  }

  protected nombrePropuesta(id: string | null | undefined): string {
    return this.detalle()?.propuestas.find((p) => p.id === id)?.nombre ?? '';
  }

  /** Propuesta que acompaña a esta (la Certificación de una Resolución), si hay. */
  protected acompanante(propuesta: Propuesta): Propuesta | null {
    return this.activas().find((p) => p.derivadaDePropuestaId === propuesta.id) ?? null;
  }

  protected readonly tipoAbierto = signal<string | null>(null);

  protected alternarTipo(tipoDocumentoId: string): void {
    this.tipoAbierto.update((actual) => (actual === tipoDocumentoId ? null : tipoDocumentoId));
  }

  // ---------- Agregar (pantalla C) ----------

  protected readonly seleccion = signal<OperacionSeleccion | null>(null);

  constructor() {
    // El menú marca la unidad del documento.
    effect(() => this.unidades.seleccionar(this.detalle()?.consolidado.unidadId));
    effect(() => {
      if (this.agregar() !== 'crear' || !this.detalle()) {
        return;
      }
      if (untracked(this.editable)) {
        this.seleccion.set('Crear');
      }
      // Se quita de la dirección para que al recargar no se vuelva a abrir.
      void this.router.navigate([], { queryParams: { agregar: null }, queryParamsHandling: 'merge', replaceUrl: true });
    });
  }

  protected alAgregar(propuestas: Propuesta[]): void {
    this.seleccion.set(null);
    this.recurso.reload();
    const principal = propuestas[0];
    const extra = propuestas.length > 1 ? ` y ${propuestas.length - 1} que la acompaña` : '';
    this.interaccion.avisar('exito', `Se agregó «${principal?.nombre}»${extra}.`);
    if (principal) {
      void this.router.navigate(['/consolidados', this.id(), 'propuestas', principal.id]);
    }
  }

  // ---------- Envío a verificación ----------

  protected readonly enviando = signal(false);

  /** EN REGISTRO → VERIFICADO. El documento sale de la bandeja del analista y pasa a la del Oficial Jurídico. */
  protected async enviar(): Promise<void> {
    const detalle = this.detalle();
    if (!detalle || this.enviando()) {
      return;
    }
    const plantillas = this.activas().length;
    const aceptado = await this.interaccion.confirmar({
      titulo: `Enviar ${detalle.consolidado.numero} a verificación`,
      mensaje: `Se enviarán ${plantillas} ${plantillas === 1 ? 'plantilla' : 'plantillas'} al Oficial Jurídico. Mientras se revisa, no podrá modificar el documento y dejará de verlo en su bandeja.`,
      aceptar: 'Enviar a verificación',
    });
    if (!aceptado) {
      return;
    }
    this.enviando.set(true);
    this.api.enviarAVerificacion(detalle.consolidado.id).subscribe({
      next: (consolidado) => {
        this.enviando.set(false);
        this.interaccion.avisar('exito', `${consolidado.numero} se envió a verificación.`);
        void this.router.navigate(['/consolidados']);
      },
      error: (error: unknown) => {
        this.enviando.set(false);
        this.interaccion.avisar('error', mensajeError(error), 10000);
      },
    });
  }

  // ---------- Vista previa ----------

  protected readonly generando = signal<string | null>(null);

  protected vistaPrevia(propuesta: Propuesta): void {
    if (this.generando()) {
      return;
    }
    const ventana = abrirVentana();
    this.generando.set(propuesta.id);
    this.api.generarPdf(propuesta.id, null).subscribe({
      next: (respuesta: HttpResponse<Blob>) => {
        this.generando.set(null);
        const archivo = respuesta.body;
        if (!archivo) {
          ventana?.close();
          return;
        }
        const url = URL.createObjectURL(archivo);
        if (ventana) {
          ventana.location.href = url;
        } else {
          const enlace = document.createElement('a');
          enlace.href = url;
          enlace.download = `${propuesta.nombre}.pdf`;
          enlace.click();
        }
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: async (error: unknown) => {
        this.generando.set(null);
        ventana?.close();
        this.interaccion.avisar('error', `No se pudo generar el PDF. ${await mensajeErrorArchivo(error)}`, 8000);
      },
    });
  }

  // ---------- Retiro ----------

  private readonly dialogoRetiro = viewChild.required<ElementRef<HTMLDialogElement>>('dialogoRetiro');
  protected readonly retirando = signal<Propuesta | null>(null);
  protected readonly motivo = signal('');
  protected readonly enviandoRetiro = signal(false);
  protected readonly errorRetiro = signal<string | null>(null);

  protected abrirRetiro(propuesta: Propuesta): void {
    this.retirando.set(propuesta);
    this.motivo.set('');
    this.errorRetiro.set(null);
    const elemento = this.dialogoRetiro().nativeElement;
    if (typeof elemento.showModal === 'function') {
      elemento.showModal();
    } else {
      elemento.setAttribute('open', '');
    }
  }

  protected cerrarRetiro(evento?: Event): void {
    evento?.preventDefault();
    if (this.enviandoRetiro()) {
      return;
    }
    this.retirando.set(null);
    const elemento = this.dialogoRetiro().nativeElement;
    elemento.close?.();
    elemento.removeAttribute('open');
  }

  protected confirmarRetiro(): void {
    const propuesta = this.retirando();
    const motivo = this.motivo().trim();
    if (!propuesta || motivo.length < 5 || this.enviandoRetiro()) {
      return;
    }
    this.enviandoRetiro.set(true);
    this.api.retirarPropuesta(propuesta.id, motivo).subscribe({
      next: (retiradas) => {
        this.enviandoRetiro.set(false);
        this.cerrarRetiro();
        this.recurso.reload();
        this.interaccion.avisar('exito', retiradas.length > 1 ? 'Se retiraron la plantilla y la que la acompaña.' : 'Se retiró la plantilla.');
      },
      error: (error: unknown) => {
        this.enviandoRetiro.set(false);
        this.errorRetiro.set(mensajeError(error));
      },
    });
  }

  protected recargar(): void {
    this.recurso.reload();
  }
}

function abrirVentana(): Window | null {
  try {
    return window.open('', '_blank');
  } catch {
    return null;
  }
}
