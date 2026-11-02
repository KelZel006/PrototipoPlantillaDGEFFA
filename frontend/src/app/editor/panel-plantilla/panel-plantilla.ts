import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { rxResource } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs';
import { abrirPdf } from '../../compartido/abrir-pdf';
import { ApiService } from '../../core/api.service';
import { mensajeError, mensajeErrorArchivo } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { NOMBRE_ESTADO } from '../../core/modelos';
import { PlantillaActualService } from '../../core/plantilla-actual.service';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { DisenoDocumento } from '../diseno-documento/diseno-documento';

/**
 * Propuesta abierta en el editor: su «Nombre Plantilla» (la referencia que escribió el analista al agregarla, en negrita), su
 * nombre técnico, la operación (CREAR o MODIFICAR), el documento consolidado al que pertenece y el diseño del documento.
 * Mientras el documento está EN REGISTRO, su analista lo envía a verificación desde aquí.
 */
@Component({
  selector: 'app-panel-plantilla',
  imports: [DatePipe, DisenoDocumento],
  templateUrl: './panel-plantilla.html',
  styleUrl: './panel-plantilla.css',
})
export class PanelPlantilla {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly interaccion = inject(InteraccionService);
  protected readonly usuario = inject(UsuarioPruebaService);
  protected readonly actual = inject(PlantillaActualService);

  protected readonly error = computed(() => (this.actual.error() ? mensajeError(this.actual.error()) : null));
  protected readonly nombreEstado = NOMBRE_ESTADO;

  private readonly tiposRecurso = rxResource({ stream: () => this.api.tiposDocumento() });

  /** Tipo de documento de la propuesta (por ejemplo, para avisar que la Certificación va con texto corrido). */
  protected readonly tipo = computed(() => {
    const plantilla = this.actual.plantilla();
    const tipos = this.tiposRecurso.hasValue() ? this.tiposRecurso.value() : [];
    return plantilla ? (tipos.find((t) => t.id === plantilla.tipoDocumentoId) ?? null) : null;
  });

  // ---------- Vista previa en PDF (perfil Analista) ----------

  protected readonly generando = signal(false);

  /**
   * Genera el PDF de la plantilla tal como está, con los datos de la primera solicitud de prueba de su misma combinación
   * (rubro y sub rubro); si no hay ninguna, sin datos.
   */
  protected async vistaPrevia(): Promise<void> {
    const plantilla = this.actual.plantilla();
    if (!plantilla || this.generando()) {
      return;
    }
    this.generando.set(true);
    const peticion = this.api.solicitudes().pipe(
      switchMap((solicitudes) => {
        const solicitud = solicitudes.find((s) => s.rubroId === plantilla.rubroId && s.subRubroId === plantilla.subRubroId);
        return this.api.generarPdf(plantilla.id, solicitud?.id ?? null);
      }),
    );
    const error = await abrirPdf(peticion, plantilla.nombreReferencia || plantilla.nombre);
    this.generando.set(false);
    if (error) {
      this.interaccion.avisar('error', `No se pudo generar el PDF. ${await mensajeErrorArchivo(error)}`, 8000);
    }
  }

  // ---------- Documento consolidado (solo para su analista, EN REGISTRO) ----------

  /** El detalle del documento se pide solo cuando la plantilla es editable: el envío cuenta sus plantillas. */
  // Al guardar un párrafo o el nombre cambia la plantilla, pero no el documento: no se vuelve a pedir.
  private readonly claveDocumento = computed(
    () => {
      const plantilla = this.actual.plantilla();
      return plantilla?.editable ? { id: plantilla.consolidadoId, usuario: this.usuario.id() } : undefined;
    },
    { equal: (a, b) => a?.id === b?.id && a?.usuario === b?.usuario },
  );
  private readonly documento = rxResource({
    params: () => this.claveDocumento(),
    stream: ({ params }) => this.api.consolidado(params.id),
  });
  private readonly detalle = computed(() => (this.documento.hasValue() ? (this.documento.value() ?? null) : null));

  protected readonly activas = computed(() => this.detalle()?.propuestas.filter((p) => !p.retirada) ?? []);

  // ---------- Envío a verificación ----------

  protected readonly enviando = signal(false);

  /** EN REGISTRO → VERIFICADO. El documento sale de la bandeja del analista y pasa a la del Oficial Jurídico. */
  protected async enviar(): Promise<void> {
    const plantilla = this.actual.plantilla();
    if (!plantilla || this.enviando()) {
      return;
    }
    const total = this.activas().length || 1;
    const aceptado = await this.interaccion.confirmar({
      titulo: `Enviar ${plantilla.consolidadoNumero} a verificación`,
      mensaje: `Se enviarán ${total} ${total === 1 ? 'plantilla' : 'plantillas'} al Oficial Jurídico. Mientras se revisa, no podrá modificar el documento y dejará de verlo en su bandeja.`,
      aceptar: 'Enviar a verificación',
    });
    if (!aceptado) {
      return;
    }
    this.enviando.set(true);
    this.api.enviarAVerificacion(plantilla.consolidadoId).subscribe({
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
}
