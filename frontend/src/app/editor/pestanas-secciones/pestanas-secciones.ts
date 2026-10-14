import { Component, input, signal } from '@angular/core';
import { Plantilla, Seccion } from '../../core/modelos';
import { EditorSeccion, NombreSeccion } from '../editor-seccion/editor-seccion';

interface Pestana {
  seccion: Seccion;
  nombre: NombreSeccion;
  /** Las secciones se habilitan por fases; por ahora solo Considerando. */
  disponible: boolean;
}

/** Pestañas Cabecera, Considerando y Resuelve. */
@Component({
  selector: 'app-pestanas-secciones',
  imports: [EditorSeccion],
  templateUrl: './pestanas-secciones.html',
  styleUrl: './pestanas-secciones.css',
})
export class PestanasSecciones {
  readonly plantilla = input.required<Plantilla>();

  protected readonly pestanas: Pestana[] = [
    { seccion: 'Cabecera', nombre: { singular: 'Encabezado', plural: 'Encabezados' }, disponible: false },
    { seccion: 'Considerando', nombre: { singular: 'Considerando', plural: 'Considerandos' }, disponible: true },
    { seccion: 'Resuelve', nombre: { singular: 'Resuelve', plural: 'Resuelves' }, disponible: false },
  ];

  protected readonly activa = signal<Seccion>('Considerando');

  protected seleccionar(pestana: Pestana): void {
    if (pestana.disponible) {
      this.activa.set(pestana.seccion);
    }
  }

  protected pestanaActiva(): Pestana {
    return this.pestanas.find((p) => p.seccion === this.activa())!;
  }
}
