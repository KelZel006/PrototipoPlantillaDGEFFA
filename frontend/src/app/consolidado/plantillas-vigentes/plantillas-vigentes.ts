import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { UnidadService } from '../../core/unidad.service';
import { mensajeError, mensajeErrorArchivo } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { PlantillaConVersiones, VersionResumen } from '../../core/modelos';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { InsigniaVigencia } from '../insignia-vigencia';

/**
 * Pantalla F: consolidado de plantillas (/vigentes). Cada plantilla con su versión vigente y, al desplegarla, todas sus
 * versiones anteriores (NO VIGENTES), con el Documento Consolidado que publicó cada una y su PDF.
 */
@Component({
  selector: 'app-plantillas-vigentes',
  imports: [DatePipe, RouterLink, InsigniaVigencia],
  templateUrl: './plantillas-vigentes.html',
  styleUrl: './plantillas-vigentes.css',
})
export class PlantillasVigentes {
  protected readonly unidades = inject(UnidadService);
  /** Unidad de la ruta (/unidades/:unidad/vigentes). */
  readonly unidad = input<string>();
  protected readonly unidadMostrada = computed(() => this.unidad() ?? this.unidades.actual());

  constructor() {
    effect(() => this.unidades.seleccionar(this.unidad()));
  }

  private readonly api = inject(ApiService);
  private readonly interaccion = inject(InteraccionService);
  private readonly usuario = inject(UsuarioPruebaService);

  private readonly recurso = rxResource({ params: () => this.usuario.id(), stream: () => this.api.consolidadoDePlantillas() });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));
  // Solo las plantillas de la unidad.
  private readonly todas = computed(() =>
    (this.recurso.hasValue() ? this.recurso.value() : []).filter((p) => p.unidadId === this.unidadMostrada()),
  );

  protected readonly busqueda = signal('');
  protected readonly tipo = signal('');
  protected readonly tipos = computed(() => [...new Set(this.todas().map((p) => p.tipoDocumento))].sort());
  protected readonly filtradas = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    return this.todas().filter(
      (p) =>
        (!this.tipo() || p.tipoDocumento === this.tipo()) &&
        (!texto || [p.nombre, p.plantillaId, p.rubroId, p.subRubroId].some((v) => v.toLowerCase().includes(texto))),
    );
  });

  protected readonly abierta = signal<string | null>(null);

  protected vigente(plantilla: PlantillaConVersiones): VersionResumen | undefined {
    return plantilla.versiones.find((v) => v.vigencia === 'Vigente');
  }

  protected alternar(plantillaId: string): void {
    this.abierta.update((actual) => (actual === plantillaId ? null : plantillaId));
  }

  protected readonly generando = signal<string | null>(null);

  protected verPdf(plantilla: PlantillaConVersiones, version: VersionResumen): void {
    if (this.generando()) {
      return;
    }
    // La ventana se abre durante el clic para que el navegador no la bloquee.
    let ventana: Window | null = null;
    try {
      ventana = window.open('', '_blank');
    } catch {
      ventana = null;
    }
    this.generando.set(version.id);
    this.api.pdfVersion(version.id).subscribe({
      next: (respuesta) => {
        this.generando.set(null);
        if (!respuesta.body) {
          ventana?.close();
          return;
        }
        const url = URL.createObjectURL(respuesta.body);
        if (ventana) {
          ventana.location.href = url;
        } else {
          const enlace = document.createElement('a');
          enlace.href = url;
          enlace.download = `${plantilla.plantillaId}_v${version.numeroVersion}.pdf`;
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

  protected recargar(): void {
    this.recurso.reload();
  }
}
