import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ContextoService } from '../../core/contexto.service';
import { numerarConsiderando, numerarResuelve } from '../../core/numeracion';
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
        numerar: numerarConsiderando,
      },
      disponible: true,
    },
    {
      seccion: 'PorTanto',
      nombre: { texto: 'Texto del Por Tanto', listado: 'Listado del Por Tanto', elemento: 'Párrafo del por tanto' },
      disponible: true,
    },
    {
      seccion: 'Resuelve',
      nombre: {
        texto: 'Texto del Resuelve',
        listado: 'Listado de Resolución',
        elemento: 'Punto resolutivo',
        numerar: numerarResuelve,
      },
      disponible: true,
    },
    {
      seccion: 'Firmas',
      nombre: { texto: 'Texto de Firmas', listado: 'Listado de Firmas', elemento: 'Bloque de firma' },
      disponible: true,
    },
  ];

  /** Nombre visible de cada sección en su pestaña. */
  protected readonly titulos: Record<Seccion, string> = {
    Cabecera: 'Cabecera',
    Considerando: 'Considerando',
    PorTanto: 'Por Tanto',
    Resuelve: 'Resuelve',
    Firmas: 'Firmas',
  };

  /** Siempre se empieza por la primera pestaña, Cabecera. */
  protected readonly activa = signal<Seccion>('Cabecera');
  private readonly idPlantilla = computed(() => this.plantilla().id);

  constructor() {
    // Al cambiar de plantilla se vuelve a la primera pestaña.
    effect(() => {
      this.idPlantilla();
      untracked(() => this.activa.set('Cabecera'));
    });
  }

  protected seleccionar(pestana: Pestana): void {
    if (pestana.disponible) {
      this.activa.set(pestana.seccion);
    }
  }
}
