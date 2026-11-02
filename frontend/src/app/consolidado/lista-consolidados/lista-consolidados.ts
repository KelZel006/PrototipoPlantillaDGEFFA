import { Component, computed, effect, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { mensajeError } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { PlantillaDelResumen, Propuesta, ResumenConsolidado } from '../../core/modelos';
import { UnidadService } from '../../core/unidad.service';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { InsigniaEstado } from '../insignia-estado';
import { SeleccionPlantilla } from '../seleccion-plantilla/seleccion-plantilla';

/** Fila de la tabla: una plantilla de un documento (o el documento, si todavía no tiene plantillas). */
export interface FilaDocumento {
  consolidado: ResumenConsolidado;
  plantilla: PlantillaDelResumen | null;
}

/**
 * Acción del botón de la izquierda de cada fila, según el rol y el estado del documento. Sin enlace, el botón abre la
 * creación de la plantilla del documento vacío; sin enlace ni «agregar», la fila no tiene acción.
 */
interface AccionFila {
  texto: string;
  titulo: string;
  enlace: unknown[] | null;
  agregar?: boolean;
  principal: boolean;
}

/** Opción de un filtro: el identificador y el nombre del catálogo. */
interface Opcion {
  id: string;
  nombre: string;
}

export const ITEMS_POR_PAGINA = [5, 10, 25, 50];

/**
 * Pantalla A: Documentos Plantillas de la unidad. Una fila por plantilla de cada documento, con filtros por Rubro, Sub
 * Rubro, Tipo Plantilla y Tipo Documento, y paginación. El servidor decide qué entra en cada bandeja: el analista ve solo
 * los suyos (sin los que ya envió a verificación) y el Oficial Jurídico, los VERIFICADOS y APROBADOS.
 */
@Component({
  selector: 'app-lista-consolidados',
  imports: [RouterLink, InsigniaEstado, SeleccionPlantilla],
  templateUrl: './lista-consolidados.html',
  styleUrl: './lista-consolidados.css',
})
export class ListaConsolidados {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly interaccion = inject(InteraccionService);
  protected readonly usuario = inject(UsuarioPruebaService);
  protected readonly unidades = inject(UnidadService);

  /** Unidad de la ruta (/unidades/:unidad/consolidados). */
  readonly unidad = input<string>();
  /** Unidad mostrada: la de la ruta o, sin ella, la abierta. */
  protected readonly unidadMostrada = computed(() => this.unidad() ?? this.unidades.actual());

  constructor() {
    effect(() => this.unidades.seleccionar(this.unidad()));
  }

  // Se vuelve a consultar al cambiar de usuario de prueba: cambia la bandeja.
  private readonly recurso = rxResource({
    params: () => this.usuario.id(),
    stream: () => this.api.consolidados(),
  });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));

  /** Una fila por plantilla de cada documento de la unidad, del documento más reciente al más antiguo. */
  private readonly filas = computed<FilaDocumento[]>(() =>
    (this.recurso.hasValue() ? this.recurso.value() : [])
      .filter((c) => c.unidadId === this.unidadMostrada())
      .flatMap((consolidado): FilaDocumento[] =>
        (consolidado.plantillas ?? []).length
          ? consolidado.plantillas.map((plantilla) => ({ consolidado, plantilla }))
          : [{ consolidado, plantilla: null }],
      ),
  );

  // Filtros: los mismos parámetros que identifican una plantilla.
  protected readonly rubro = signal('');
  protected readonly subRubro = signal('');
  protected readonly tipoPlantilla = signal('');
  protected readonly tipoDocumento = signal('');

  private opciones(valores: (Opcion | null)[]): Opcion[] {
    const unicas = new Map<string, string>();
    for (const valor of valores) {
      if (valor) {
        unicas.set(valor.id, valor.nombre);
      }
    }
    return [...unicas].map(([id, nombre]) => ({ id, nombre })).sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
  }

  private readonly plantillas = computed(() => this.filas().map((f) => f.plantilla).filter((p) => p !== null));
  protected readonly rubros = computed(() => this.opciones(this.plantillas().map((p) => ({ id: p.rubroId, nombre: p.rubro }))));
  /** El sub rubro depende del rubro elegido. */
  protected readonly subRubros = computed(() =>
    this.opciones(
      this.plantillas()
        .filter((p) => p.rubroId === this.rubro())
        .map((p) => ({ id: p.subRubroId, nombre: p.subRubro })),
    ),
  );
  protected readonly tiposPlantilla = computed(() =>
    this.opciones(this.plantillas().map((p) => ({ id: p.tipoDocumentoId, nombre: p.tipoDocumento }))),
  );
  protected readonly tiposDocumento = computed(() =>
    this.opciones(this.plantillas().map((p) => ({ id: p.claseDocumentoId, nombre: p.claseDocumento }))),
  );

  protected readonly hayFiltros = computed(() => !!(this.rubro() || this.subRubro() || this.tipoPlantilla() || this.tipoDocumento()));

  protected readonly filtradas = computed(() =>
    this.filas().filter(({ plantilla: p }) =>
      !this.hayFiltros()
        ? true
        : !!p &&
          (!this.rubro() || p.rubroId === this.rubro()) &&
          (!this.subRubro() || p.subRubroId === this.subRubro()) &&
          (!this.tipoPlantilla() || p.tipoDocumentoId === this.tipoPlantilla()) &&
          (!this.tipoDocumento() || p.claseDocumentoId === this.tipoDocumento()),
    ),
  );

  protected elegirRubro(id: string): void {
    this.rubro.set(id);
    // El sub rubro depende del rubro: al cambiar de rubro se descarta.
    this.subRubro.set('');
  }

  protected limpiar(): void {
    for (const filtro of [this.rubro, this.subRubro, this.tipoPlantilla, this.tipoDocumento]) {
      filtro.set('');
    }
  }

  // Paginación.
  protected readonly itemsPorPagina = ITEMS_POR_PAGINA;
  protected readonly porPagina = signal(10);
  /** Vuelve a la primera página cuando cambian los filtros, la unidad o el tamaño de página. */
  protected readonly pagina = linkedSignal({
    source: () => [this.filtradas().length, this.porPagina(), this.rubro(), this.subRubro(), this.tipoPlantilla(), this.tipoDocumento()],
    computation: () => 1,
  });
  protected readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.filtradas().length / this.porPagina())));
  protected readonly paginas = computed(() => Array.from({ length: this.totalPaginas() }, (_, i) => i + 1));
  protected readonly visibles = computed(() => {
    const inicio = (this.pagina() - 1) * this.porPagina();
    return this.filtradas().slice(inicio, inicio + this.porPagina());
  });
  protected readonly rango = computed(() => {
    const total = this.filtradas().length;
    const inicio = total === 0 ? 0 : (this.pagina() - 1) * this.porPagina() + 1;
    return { inicio, fin: Math.min(total, this.pagina() * this.porPagina()), total };
  });

  protected irA(pagina: number): void {
    this.pagina.set(Math.min(Math.max(1, pagina), this.totalPaginas()));
  }

  /**
   * Botón de la izquierda: el analista dueño modifica su plantilla mientras el documento está EN REGISTRO; el Oficial
   * Jurídico revisa lo VERIFICADO; los demás la consultan.
   */
  protected accion(fila: FilaDocumento): AccionFila {
    const { consolidado, plantilla } = fila;
    const editable =
      consolidado.estado === 'EnRegistro' && consolidado.analistaId === this.usuario.id() && this.usuario.tiene('Analista');
    if (consolidado.estado === 'Verificado' && this.usuario.tiene('OficialJuridico')) {
      return { texto: 'Revisar', titulo: 'Revisión jurídica del documento', enlace: ['/consolidados', consolidado.id, 'revision'], principal: true };
    }
    if (!plantilla) {
      return editable
        ? { texto: 'Agregar', titulo: 'El documento todavía no tiene plantillas: agregue una', enlace: null, agregar: true, principal: true }
        : { texto: 'Sin plantillas', titulo: 'El documento todavía no tiene plantillas', enlace: null, principal: false };
    }
    const enlace = ['/consolidados', consolidado.id, 'propuestas', plantilla.propuestaId];
    return editable
      ? { texto: 'Modificar', titulo: `Modificar «${plantilla.nombre}»`, enlace, principal: true }
      : { texto: 'Ver', titulo: `Ver «${plantilla.nombre}»`, enlace, principal: false };
  }

  /** Solo el analista dueño elimina su documento, y solo EN REGISTRO (el servidor también lo valida). */
  protected puedeEliminar(consolidado: ResumenConsolidado): boolean {
    return consolidado.estado === 'EnRegistro' && consolidado.analistaId === this.usuario.id() && this.usuario.tiene('Analista');
  }

  protected readonly eliminando = signal<string | null>(null);

  /** Eliminación lógica: el documento pasa a ELIMINADO con todas sus plantillas y sale de la bandeja. */
  protected async eliminar(consolidado: ResumenConsolidado): Promise<void> {
    if (this.eliminando()) {
      return;
    }
    const total = consolidado.plantillas?.length ?? 0;
    const plantillas = total === 0 ? 'sin plantillas' : total === 1 ? 'con su plantilla' : `con sus ${total} plantillas`;
    const aceptado = await this.interaccion.confirmar({
      titulo: `Eliminar ${consolidado.numero}`,
      mensaje: `El documento ${plantillas} pasará al estado ELIMINADO y dejará de verse en su bandeja. No se borra: queda registrado en el historial.`,
      aceptar: 'Eliminar',
    });
    if (!aceptado) {
      return;
    }
    this.eliminando.set(consolidado.id);
    this.api.eliminarConsolidado(consolidado.id).subscribe({
      next: (eliminado) => {
        this.eliminando.set(null);
        this.interaccion.avisar('exito', `${eliminado.numero} quedó ELIMINADO.`);
        this.recurso.reload();
      },
      error: (error: unknown) => {
        this.eliminando.set(null);
        this.interaccion.avisar('error', mensajeError(error), 8000);
      },
    });
  }

  protected readonly creando = signal(false);
  /** Documento al que se agrega la plantilla: abre el diálogo para crearla (desde cero o clonando otra). */
  protected readonly agregandoEn = signal<Pick<ResumenConsolidado, 'id' | 'unidadId'> | null>(null);

  protected agregar(consolidado: Pick<ResumenConsolidado, 'id' | 'unidadId'>): void {
    this.agregandoEn.set(consolidado);
  }

  protected alAgregar(propuestas: Propuesta[]): void {
    this.agregandoEn.set(null);
    const principal = propuestas[0];
    const extra = propuestas.length > 1 ? ` y ${propuestas.length - 1} que la acompaña` : '';
    this.interaccion.avisar('exito', `Se agregó «${principal?.nombre}»${extra}.`);
    if (principal) {
      void this.router.navigate(['/consolidados', principal.consolidadoId, 'propuestas', principal.id]);
    }
  }

  protected crear(): void {
    if (this.creando()) {
      return;
    }
    this.creando.set(true);
    this.api.crearConsolidado(this.unidadMostrada()).subscribe({
      next: (consolidado) => {
        this.creando.set(false);
        this.interaccion.avisar('exito', `Se creó el documento ${consolidado.numero} de ${this.unidades.nombre(consolidado.unidadId)}.`);
        // El documento queda en la lista (con «Agregar», si se cierra el diálogo sin crear la plantilla).
        this.recurso.reload();
        this.agregar(consolidado);
      },
      error: (error: unknown) => {
        this.creando.set(false);
        this.interaccion.avisar('error', mensajeError(error), 8000);
      },
    });
  }

  protected recargar(): void {
    this.recurso.reload();
  }
}
