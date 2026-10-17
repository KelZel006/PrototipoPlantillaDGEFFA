import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ContextoService } from '../../core/contexto.service';
import { numeradorDe } from '../../core/numeracion';
import { BloqueConfigurado, PlantillaEdicion, Seccion } from '../../core/modelos';
import { EditorSeccion, NombreSeccion } from '../editor-seccion/editor-seccion';

interface Pestana {
  seccion: Seccion;
  titulo: string;
  nombre: NombreSeccion;
  bloque: BloqueConfigurado;
}

/** Textos propios de los cinco bloques originales; los demás bloques del catálogo usan textos generales. */
const TEXTOS: Record<string, Omit<NombreSeccion, 'numerar'>> = {
  CABECERA: { texto: 'Texto de la Cabecera', listado: 'Listado de la Cabecera', elemento: 'Párrafo de cabecera' },
  CONSIDERANDO: { texto: 'Texto del Considerando', listado: 'Listado de Considerandos', elemento: 'Considerando' },
  POR_TANTO: { texto: 'Texto del Por Tanto', listado: 'Listado del Por Tanto', elemento: 'Párrafo del por tanto' },
  RESUELVE: { texto: 'Texto del Resuelve', listado: 'Listado de Resolución', elemento: 'Punto resolutivo' },
  FIRMAS: { texto: 'Texto de Firmas', listado: 'Listado de Firmas', elemento: 'Bloque de firma' },
};

/**
 * Una pestaña por cada bloque del tipo documental (decisión D2), en su orden. Las secciones quedan montadas y solo se
 * oculta la inactiva, para que cada una conserve su borrador al cambiar de pestaña.
 */
@Component({
  selector: 'app-pestanas-secciones',
  imports: [EditorSeccion],
  templateUrl: './pestanas-secciones.html',
  styleUrl: './pestanas-secciones.css',
})
export class PestanasSecciones {
  protected readonly contexto = inject(ContextoService);

  readonly plantilla = input.required<PlantillaEdicion>();

  protected readonly pestanas = computed<Pestana[]>(() =>
    this.plantilla().bloques.map((bloque) => ({
      seccion: bloque.codigo,
      titulo: bloque.nombre,
      bloque,
      nombre: {
        ...(TEXTOS[bloque.codigo] ?? {
          texto: `Texto de ${bloque.nombre}`,
          listado: `Listado de ${bloque.nombre}`,
          elemento: `Párrafo de ${bloque.nombre.toLowerCase()}`,
        }),
        numerar: numeradorDe(bloque.numeracion) ?? undefined,
      },
    })),
  );

  /** Siempre se empieza por la primera pestaña. */
  protected readonly activa = signal<Seccion>('');
  private readonly idPlantilla = computed(() => this.plantilla().id);

  constructor() {
    // Al cambiar de plantilla se vuelve a la primera pestaña.
    effect(() => {
      this.idPlantilla();
      untracked(() => this.activa.set(this.pestanas()[0]?.seccion ?? ''));
    });
  }

  protected seleccionar(pestana: Pestana): void {
    this.activa.set(pestana.seccion);
  }
}
