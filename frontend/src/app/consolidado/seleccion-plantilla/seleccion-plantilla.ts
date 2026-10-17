import { DatePipe } from '@angular/common';
import { Component, ElementRef, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { Propuesta } from '../../core/modelos';
import { SelectorContexto } from '../../editor/selector-contexto/selector-contexto';

export type OperacionSeleccion = 'Crear' | 'Modificar';

/**
 * Pantalla C: agregar una plantilla al consolidado.
 * <ul>
 * <li>CREAR: Rubro, Sub Rubro, Tipo Documento y Documento de una plantilla que todavía no existe, y su nombre.</li>
 * <li>MODIFICAR: el catálogo de plantillas vigentes, con su tipo, código y versión. Las que ya están en otro documento
 * abierto aparecen deshabilitadas (D4).</li>
 * </ul>
 * Una Resolución trae consigo su Certificación (D3).
 */
@Component({
  selector: 'app-seleccion-plantilla',
  imports: [DatePipe, SelectorContexto],
  templateUrl: './seleccion-plantilla.html',
  styleUrl: './seleccion-plantilla.css',
})
export class SeleccionPlantilla {
  private readonly api = inject(ApiService);
  protected readonly contexto = inject(ContextoService);

  readonly consolidadoId = input.required<string>();
  /** Operación con que se abre el diálogo; null lo cierra. */
  readonly operacion = input<OperacionSeleccion | null>(null);
  readonly cerrar = output<void>();
  readonly agregado = output<Propuesta[]>();

  private readonly dialogo = viewChild.required<ElementRef<HTMLDialogElement>>('dialogo');

  protected readonly pestana = signal<OperacionSeleccion>('Crear');
  protected readonly nombre = signal('');
  protected readonly busqueda = signal('');
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly tiposRecurso = rxResource({ stream: () => this.api.tiposDocumento() });
  private readonly vigentesRecurso = rxResource({
    params: () => (this.operacion() ? this.consolidadoId() : undefined),
    stream: () => this.api.plantillasVigentes(),
  });

  protected readonly cargandoVigentes = this.vigentesRecurso.isLoading;
  protected readonly vigentes = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    const lista = this.vigentesRecurso.hasValue() ? this.vigentesRecurso.value() : [];
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
        this.pestana.set(operacion);
        this.error.set(null);
        this.nombre.set('');
        this.busqueda.set('');
        // Cada creación empieza con las listas vacías.
        this.contexto.descartarCambios();
        this.contexto.seleccionarRubro(null);
        this.contexto.seleccionarTipoDocumento(null);
        this.contexto.seleccionarClaseDocumento(null);
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
    if (!this.contexto.completo() || !rubro || !subRubro || !tipo || !clase || this.enviando()) {
      return;
    }
    this.enviar(
      this.api.agregarCrear(this.consolidadoId(), {
        rubroId: rubro.id,
        subRubroId: subRubro.id,
        tipoDocumentoId: tipo.id,
        claseDocumentoId: clase.id,
        nombre: this.nombre().trim() || null,
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
