import { Component, inject, input, signal } from '@angular/core';
import { ContextoService } from '../../core/contexto.service';
import { Plantilla, Seccion } from '../../core/modelos';
import { EditorSeccion, NombreSeccion } from '../editor-seccion/editor-seccion';

interface Pestana {
  seccion: Seccion;
  nombre: NombreSeccion;
  /** Las secciones se habilitan por fases. */
  disponible: boolean;
}

/**
 * Pestañas Cabecera, Considerando y Resuelve. Las secciones disponibles quedan montadas y solo se oculta
 * la inactiva, para que cada una conserve su borrador al cambiar de pestaña.
 */
@Component({
  selector: 'app-pestanas-secciones',
  imports: [EditorSeccion],
  templateUrl: './pestanas-secciones.html',
  styleUrl: './pestanas-secciones.css',
})
export class PestanasSecciones {
  protected readonly contexto = inject(ContextoService);

  readonly plantilla = input.required<Plantilla>();

  protected readonly pestanas: Pestana[] = [
    {
      seccion: 'Cabecera',
      nombre: { texto: 'Texto de la Cabecera', listado: 'Listado de la Cabecera', elemento: 'Párrafo de cabecera' },
      disponible: true,
    },
    {
      seccion: 'Considerando',
      nombre: {
        texto: 'Texto del Considerando',
        listado: 'Listado de Considerandos',
        elemento: 'Considerando',
        etiquetaNumerada: 'CONSIDERANDO',
      },
      disponible: true,
    },
    {
      seccion: 'Resuelve',
      nombre: { texto: 'Texto del Resuelve', listado: 'Listado de Resuelve', elemento: 'Resuelve' },
      disponible: false,
    },
  ];

  protected readonly activa = signal<Seccion>('Considerando');

  protected seleccionar(pestana: Pestana): void {
    if (pestana.disponible) {
      this.activa.set(pestana.seccion);
    }
  }
}
