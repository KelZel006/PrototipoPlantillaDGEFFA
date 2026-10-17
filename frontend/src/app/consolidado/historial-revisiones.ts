import { DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { DetalleConsolidado, Observacion } from '../core/modelos';

const ORDINALES = ['Primera', 'Segunda', 'Tercera', 'Cuarta', 'Quinta', 'Sexta', 'Séptima', 'Octava', 'Novena', 'Décima'];

interface Evento {
  fecha: string;
  tipo: 'presentacion' | 'devolucion';
  titulo: string;
  usuario: string;
  texto?: string;
  observaciones: Observacion[];
}

/**
 * Historial de revisiones del documento: cada presentación y cada devolución, en orden y con su número («Primera
 * presentación», «Primera devolución», «Segunda presentación»…). Las justificaciones anteriores no se sobrescriben.
 */
@Component({
  selector: 'app-historial-revisiones',
  imports: [DatePipe],
  template: `
    @if (eventos().length === 0) {
      <p class="texto-secundario sin-margen">Todavía no se ha enviado a verificación.</p>
    } @else {
      <ol class="linea">
        @for (e of eventos(); track $index) {
          <li [class]="e.tipo">
            <div class="cabecera">
              <strong>{{ e.titulo }}</strong>
              <span class="texto-secundario">{{ e.fecha | date: 'dd/MM/yyyy HH:mm' }} · {{ e.usuario }}</span>
            </div>
            @if (e.texto) {
              <p>{{ e.texto }}</p>
            }
            @if (e.observaciones.length) {
              <ul class="observaciones">
                @for (o of e.observaciones; track $index) {
                  <li>
                    <strong>{{ nombrePlantilla(o.propuestaId) }}{{ o.bloqueCodigo ? ' · ' + o.bloqueCodigo : '' }}:</strong>
                    {{ o.problema }}
                    @if (o.recomendacion) {
                      <em>Recomendación: {{ o.recomendacion }}</em>
                    }
                  </li>
                }
              </ul>
            }
          </li>
        }
      </ol>
    }
  `,
  styles: `
    .sin-margen {
      margin: 0;
    }
    .linea {
      display: flex;
      flex-direction: column;
      gap: var(--espacio-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .linea > li {
      padding: var(--espacio-2) var(--espacio-3);
      border-left: 3px solid var(--primario);
      background: var(--fondo-suave);
      border-radius: 0 var(--radio) var(--radio) 0;
    }
    .linea > li.devolucion {
      border-left-color: var(--prueba);
      background: var(--prueba-suave);
    }
    .cabecera {
      display: flex;
      flex-wrap: wrap;
      gap: var(--espacio-3);
      align-items: baseline;
    }
    p {
      margin: var(--espacio-1) 0 0;
    }
    .observaciones {
      margin: var(--espacio-1) 0 0;
    }
    em {
      display: block;
    }
  `,
})
export class HistorialRevisiones {
  readonly detalle = input.required<DetalleConsolidado>();

  protected readonly eventos = computed<Evento[]>(() => {
    const d = this.detalle();
    const presentaciones: Evento[] = d.presentaciones.map((p) => ({
      fecha: p.fecha,
      tipo: 'presentacion',
      titulo: `${ordinal(p.numero)} presentación`,
      usuario: p.usuario,
      texto: `${p.cantidadPlantillas} ${p.cantidadPlantillas === 1 ? 'plantilla enviada' : 'plantillas enviadas'} a verificación.`,
      observaciones: [],
    }));
    const devoluciones: Evento[] = d.devoluciones.map((v, i) => ({
      fecha: v.fecha,
      tipo: 'devolucion',
      titulo: `${ordinal(i + 1)} devolución`,
      usuario: v.usuario,
      texto: v.justificacion,
      observaciones: v.observaciones,
    }));
    return [...presentaciones, ...devoluciones].sort((a, b) => a.fecha.localeCompare(b.fecha));
  });

  protected nombrePlantilla(propuestaId: string): string {
    return this.detalle().propuestas.find((p) => p.id === propuestaId)?.nombre ?? propuestaId;
  }
}

function ordinal(numero: number): string {
  return ORDINALES[numero - 1] ?? `${numero}.ª`;
}
