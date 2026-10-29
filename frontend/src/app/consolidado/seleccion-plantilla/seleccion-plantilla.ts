import { DatePipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { PlantillaVigente, Propuesta } from '../../core/modelos';
import { SelectorContexto } from '../../editor/selector-contexto/selector-contexto';
import { SelectorOrigen } from './selector-origen';

export type OperacionSeleccion = 'Crear' | 'Modificar';

/** CREAR desde cero (vacía) o clonando una plantilla vigente. */
export type ModoCrear = 'Cero' | 'Clonar';

/**
 * Pantalla C: agregar una plantilla al consolidado.
 * <ul>
 * <li>CREAR: Rubro, Sub Rubro, Tipo Documento y Documento de una plantilla que todavía no existe, y su nombre. Puede
 * empezar vacía o como copia de una plantilla vigente, elegida con las mismas cuatro listas.</li>
 * <li>MODIFICAR: el catálogo de plantillas vigentes, con su tipo, código y versión. Las que ya están en otro documento
 * abierto aparecen deshabilitadas (D4).</li>
 * </ul>
 * Una Resolución trae consigo su Certificación (D3).
 */
@Component({
  selector: 'app-seleccion-plantilla',
  imports: [DatePipe, SelectorContexto, SelectorOrigen],
  templateUrl: './seleccion-plantilla.html',
  styleUrl: './seleccion-plantilla.css',
})
export class SeleccionPlantilla {
  private readonly api = inject(ApiService);
  protected readonly contexto = inject(ContextoService);

  readonly consolidadoId = input.required<string>();
  /** Unidad del documento: MODIFICAR solo lista las plantillas de esa unidad. */
  readonly unidadId = input<string>('');
  /** Operación con que se abre el diálogo; null lo cierra. */
  readonly operacion = input<OperacionSeleccion | null>(null);
  readonly cerrar = output<void>();
  readonly agregado = output<Propuesta[]>();

  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  protected readonly pestana = signal<OperacionSeleccion>('Crear');
  protected readonly nombre = signal('');
  protected readonly modo = signal<ModoCrear>('Cero');
  /** Plantilla vigente elegida para clonar. */
  protected readonly origen = signal<PlantillaVigente | null>(null);
  protected readonly busqueda = signal('');
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly tiposRecurso = rxResource({ stream: () => this.api.tiposDocumento() });
  private readonly vigentesRecurso = rxResource({
    params: () => (this.operacion() ? this.consolidadoId() : undefined),
    stream: () => this.api.plantillasVigentes(),
  });

  protected readonly cargandoVigentes = this.vigentesRecurso.isLoading;
  protected readonly todasVigentes = computed(() => (this.vigentesRecurso.hasValue() ? this.vigentesRecurso.value() : []));
  /** Se puede crear: la combinación nueva está completa y, si se clona, ya se eligió la plantilla de origen. */
  protected readonly puedeCrear = computed(
    () => this.contexto.completo() && !this.enviando() && (this.modo() === 'Cero' || !!this.origen()),
  );
  protected readonly vigentes = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    const lista = (this.vigentesRecurso.hasValue() ? this.vigentesRecurso.value() : []).filter(
      (p) => !this.unidadId() || p.unidadId === this.unidadId(),
    );
    return texto
      ? lista.filter((p) =>
          [p.nombre, p.plantillaId, p.tipoDocumento, p.rubroId, p.subRubroId].some((v) => v.toLowerCase().includes(texto)),
        )
      : lista;
  });

  /** Tipo de documento elegido para CREAR, y si trae consigo otra plantilla (la Resolución trae su Certificación). */
  protected readonly acompanante = computed(() => {
    const tipo = this.contexto.tipoDocumento();
    const tipos = this.tiposRecurso.hasValue() ? this.tiposRecurso.value() : [];
    return tipo ? (tipos.find((t) => t.copiaDeTipoDocumentoId === tipo.id && t.activo)?.nombre ?? null) : null;
  });

  protected acompananteDe(tipoDocumentoId: string): string | null {
    const tipos = this.tiposRecurso.hasValue() ? this.tiposRecurso.value() : [];
    return tipos.find((t) => t.copiaDeTipoDocumentoId === tipoDocumentoId && t.activo)?.nombre ?? null;
  }

  constructor() {
    effect(() => {
      const operacion = this.operacion();
      const elemento = this.dialogo().nativeElement;
      if (operacion) {
        // Sin seguimiento: elegir un rubro no debe volver a ejecutar esta limpieza (la haría borrar lo elegido).
        untracked(() => this.reiniciar(operacion));
        if (!elemento.open) {
          // jsdom (pruebas) no implementa showModal.
          if (typeof elemento.showModal === 'function') {
            elemento.showModal();
          } else {
            elemento.setAttribute('open', '');
          }
        }
      } else if (elemento.open) {
        elemento.close?.();
        elemento.removeAttribute('open');
      }
    });
  }

  /** Cada vez que se abre: la pestaña pedida, sin errores y con las listas vacías. */
  private reiniciar(operacion: OperacionSeleccion): void {
    this.pestana.set(operacion);
    this.error.set(null);
    this.nombre.set('');
    this.busqueda.set('');
    this.modo.set('Cero');
    this.origen.set(null);
    this.contexto.descartarCambios();
    this.contexto.seleccionarRubro(null);
    this.contexto.seleccionarTipoDocumento(null);
    this.contexto.seleccionarClaseDocumento(null);
  }

  protected cancelar(evento?: Event): void {
    evento?.preventDefault();
    if (!this.enviando()) {
      this.cerrar.emit();
    }
  }

  protected crear(): void {
    const rubro = this.contexto.rubro();
    const subRubro = this.contexto.subRubro();
    const tipo = this.contexto.tipoDocumento();
    const clase = this.contexto.claseDocumento();
    if (!this.puedeCrear() || !rubro || !subRubro || !tipo || !clase) {
      return;
    }
    this.enviar(
      this.api.agregarCrear(this.consolidadoId(), {
        rubroId: rubro.id,
        subRubroId: subRubro.id,
        tipoDocumentoId: tipo.id,
        claseDocumentoId: clase.id,
        nombre: this.nombre().trim() || null,
        plantillaOrigenId: this.modo() === 'Clonar' ? (this.origen()?.plantillaId ?? null) : null,
      }),
    );
  }

  protected modificar(plantillaId: string): void {
    if (!this.enviando()) {
      this.enviar(this.api.agregarModificar(this.consolidadoId(), plantillaId));
    }
  }

  private enviar(peticion: ReturnType<ApiService['agregarCrear']>): void {
    this.enviando.set(true);
    this.error.set(null);
    peticion.subscribe({
      next: (propuestas) => {
        this.enviando.set(false);
        this.vigentesRecurso.reload();
        this.agregado.emit(propuestas);
      },
      error: (e: unknown) => {
        this.enviando.set(false);
        this.error.set(mensajeError(e));
      },
    });
  }
}
