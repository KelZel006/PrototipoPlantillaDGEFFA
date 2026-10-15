import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { identificadorUnico } from '../../compartido/identificador';
import { ApiService } from '../../core/api.service';
import { mensajeError } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { AlineacionLogo, ESPACIO_FIRMAS, Plantilla } from '../../core/modelos';
import { PlantillaActualService } from '../../core/plantilla-actual.service';

/** Límites que también aplica el servidor. */
export const TAMANO_MAXIMO_LOGO = 1024 * 1024;
export const MAXIMO_PIE = 500;
const TIPOS_LOGO = ['image/png', 'image/jpeg'];

/** Logo del encabezado, su alineación y el texto del pie de página de la plantilla. */
@Component({
  selector: 'app-diseno-documento',
  templateUrl: './diseno-documento.html',
  styleUrl: './diseno-documento.css',
})
export class DisenoDocumento {
  private readonly api = inject(ApiService);
  private readonly interaccion = inject(InteraccionService);
  private readonly actual = inject(PlantillaActualService);

  readonly plantilla = input.required<Plantilla>();
  /** Solo el identificador: una versión nueva de la misma plantilla no debe reiniciar el formulario. */
  private readonly idPlantilla = computed(() => this.plantilla().id);

  protected readonly uid = identificadorUnico('diseno');
  protected readonly maximoPie = MAXIMO_PIE;
  protected readonly alineaciones: { valor: AlineacionLogo; nombre: string }[] = [
    { valor: 'Izquierda', nombre: 'Izquierda' },
    { valor: 'Centro', nombre: 'Centro' },
    { valor: 'Derecha', nombre: 'Derecha' },
  ];

  // Borrador del pie y de la alineación: se reinicia solo al cambiar de plantilla, para no perder lo escrito
  // cuando la plantilla cambia de versión por otra acción (por ejemplo, al cargar el logo).
  protected readonly pie = signal('');
  protected readonly alineacion = signal<AlineacionLogo>('Centro');
  protected readonly espacioFirmas = signal<number>(ESPACIO_FIRMAS.predeterminado);
  private readonly base = signal({
    pie: '',
    alineacion: 'Centro' as AlineacionLogo,
    espacioFirmas: ESPACIO_FIRMAS.predeterminado as number,
  });
  protected readonly limitesEspacio = ESPACIO_FIRMAS;

  protected readonly sucio = computed(
    () =>
      this.pie() !== this.base().pie ||
      this.alineacion() !== this.base().alineacion ||
      this.espacioFirmas() !== this.base().espacioFirmas,
  );
  protected readonly excedido = computed(() => this.pie().length > MAXIMO_PIE);
  protected readonly espacioInvalido = computed(() => {
    const espacio = this.espacioFirmas();
    return !Number.isFinite(espacio) || espacio < ESPACIO_FIRMAS.minimo || espacio > ESPACIO_FIRMAS.maximo;
  });
  protected readonly urlLogo = computed(() => {
    const id = this.plantilla().logoId;
    return id ? this.api.urlArchivo(id) : null;
  });

  /**
   * Se abre al entrar a una plantilla sin diseño; después lo controla el usuario. No se recalcula con cada cambio,
   * para que el panel no se cierre al cargar el logo.
   */
  protected readonly abierto = signal(false);

  protected readonly guardando = signal(false);
  protected readonly cargandoLogo = signal(false);
  protected readonly errorLogo = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.idPlantilla();
      untracked(() => {
        const plantilla = this.plantilla();
        this.abierto.set(!plantilla.logoId && !plantilla.piePagina);
        this.reiniciar(plantilla);
      });
    });
  }

  protected alElegirLogo(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const archivo = entrada.files?.[0];
    entrada.value = '';
    if (!archivo) {
      return;
    }
    // Primera validación en el navegador; el servidor vuelve a comprobar el contenido real del archivo.
    if (!TIPOS_LOGO.includes(archivo.type)) {
      this.errorLogo.set('El logo debe ser una imagen PNG o JPEG.');
      return;
    }
    if (archivo.size > TAMANO_MAXIMO_LOGO) {
      this.errorLogo.set('El logo no puede superar 1 MB.');
      return;
    }

    this.errorLogo.set(null);
    this.cargandoLogo.set(true);
    this.api.cargarLogo(this.plantilla().id, archivo).subscribe({
      next: (plantilla) => {
        this.cargandoLogo.set(false);
        this.actual.reemplazar(plantilla);
        this.interaccion.avisar('exito', 'Logo actualizado.');
      },
      error: (error: unknown) => {
        this.cargandoLogo.set(false);
        this.errorLogo.set(mensajeError(error));
      },
    });
  }

  protected async quitarLogo(): Promise<void> {
    const aceptado = await this.interaccion.confirmar({
      titulo: 'Quitar logo',
      mensaje: 'El documento se generará sin logo en el encabezado.',
      aceptar: 'Quitar logo',
      peligro: true,
    });
    if (!aceptado) {
      return;
    }
    this.cargandoLogo.set(true);
    this.api.quitarLogo(this.plantilla().id).subscribe({
      next: (plantilla) => {
        this.cargandoLogo.set(false);
        this.actual.reemplazar(plantilla);
        this.interaccion.avisar('exito', 'Logo quitado.');
      },
      error: (error: unknown) => {
        this.cargandoLogo.set(false);
        this.errorLogo.set(mensajeError(error));
      },
    });
  }

  protected guardar(): void {
    if (!this.sucio() || this.excedido() || this.espacioInvalido() || this.guardando()) {
      return;
    }
    this.guardando.set(true);
    const pie = this.pie().trim();
    this.api
      .actualizarPlantilla({
        ...this.plantilla(),
        piePagina: pie === '' ? null : pie,
        alineacionLogo: this.alineacion(),
        espacioFirmasCm: Math.round(this.espacioFirmas() * 10) / 10,
      })
      .subscribe({
        next: (plantilla) => {
          this.guardando.set(false);
          this.actual.reemplazar(plantilla);
          this.reiniciar(plantilla);
          this.interaccion.avisar('exito', 'Diseño del documento guardado.');
        },
        error: (error: unknown) => {
          this.guardando.set(false);
          this.interaccion.avisar('error', mensajeError(error), 8000);
        },
      });
  }

  protected descartar(): void {
    this.reiniciar(this.plantilla());
  }

  private reiniciar(plantilla: Plantilla): void {
    const base = {
      pie: plantilla.piePagina ?? '',
      alineacion: plantilla.alineacionLogo ?? 'Centro',
      espacioFirmas: plantilla.espacioFirmasCm ?? ESPACIO_FIRMAS.predeterminado,
    };
    this.base.set(base);
    this.pie.set(base.pie);
    this.alineacion.set(base.alineacion);
    this.espacioFirmas.set(base.espacioFirmas);
  }
}
