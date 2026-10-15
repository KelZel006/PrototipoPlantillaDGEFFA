import { moveItemInArray } from '@angular/cdk/drag-drop';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable } from 'rxjs';
import { identificadorUnico } from '../../compartido/identificador';
import { ApiService } from '../../core/api.service';
import { CatalogoService } from '../../core/catalogo.service';
import { copiarGrupo, grupoVacio, normalizarCondicion, problemasCondicion } from '../../core/condiciones';
import {
  MAXIMO_CARACTERES,
  camposUsados,
  contarCaracteres,
  documentoVacio,
  limpiarDocumento,
  textoPlano,
  tieneContenido,
} from '../../core/contenido';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { PreferenciasService } from '../../core/preferencias.service';
import { Campo, GrupoCondiciones, NodoContenido, Parrafo, Plantilla, Seccion, esCampoLista } from '../../core/modelos';
import { ConstructorCondiciones } from '../constructor-condiciones/constructor-condiciones';
import { EditorTexto } from '../editor-texto/editor-texto';
import { ListaParrafos } from '../lista-parrafos/lista-parrafos';
import { PanelCampos } from '../panel-campos/panel-campos';

/** Textos de la sección en la interfaz. */
export interface NombreSeccion {
  /** Título del editor, por ejemplo "Texto del Considerando". */
  texto: string;
  /** Título del listado, por ejemplo "Listado de Considerandos". */
  listado: string;
  /** Nombre de un elemento, por ejemplo "Considerando" o "Párrafo de cabecera". */
  elemento: string;
  /** Si se indica, cada elemento se numera en el documento, por ejemplo "CONSIDERANDO" → "CONSIDERANDO(1):". */
  etiquetaNumerada?: string;
}

interface Borrador {
  contenido: NodoContenido;
  condicion: GrupoCondiciones;
  mostrarSoloSiCumple: boolean;
}

/** Huella de un borrador para saber si cambió respecto de lo guardado. */
function huella(borrador: Borrador): string {
  return JSON.stringify({
    contenido: limpiarDocumento(borrador.contenido),
    condicion: normalizarCondicion(borrador.condicion),
    mostrarSoloSiCumple: borrador.mostrarSoloSiCumple,
  });
}

/**
 * Edición de una sección (Considerando y, más adelante, Cabecera y Resuelve) con la distribución del diseño:
 * condiciones y editor a la izquierda, listado al centro y campos disponibles a la derecha.
 */
@Component({
  selector: 'app-editor-seccion',
  imports: [ConstructorCondiciones, EditorTexto, ListaParrafos, PanelCampos],
  templateUrl: './editor-seccion.html',
  styleUrl: './editor-seccion.css',
  host: { '(window:beforeunload)': 'alSalir($event)' },
})
export class EditorSeccion {
  private readonly api = inject(ApiService);
  protected readonly catalogo = inject(CatalogoService);
  private readonly interaccion = inject(InteraccionService);
  private readonly contexto = inject(ContextoService);
  protected readonly preferencias = inject(PreferenciasService);

  readonly plantilla = input.required<Plantilla>();
  readonly seccion = input.required<Seccion>();
  readonly nombre = input.required<NombreSeccion>();
  /**
   * Solo el identificador: una versión nueva de la misma plantilla (por ejemplo, al cargar el logo) no debe
   * recargar la lista ni borrar el borrador.
   */
  private readonly idPlantilla = computed(() => this.plantilla().id);

  protected readonly uid = identificadorUnico('texto');
  private readonly editorTexto = viewChild(EditorTexto);

  private readonly parrafosRecurso = rxResource({
    params: () => ({ plantillaId: this.idPlantilla(), seccion: this.seccion() }),
    stream: ({ params }) => this.api.parrafos(params.plantillaId, params.seccion),
  });

  /** Lista mostrada; se actualiza también con las respuestas de reordenar, sin esperar una nueva consulta. */
  protected readonly parrafos = linkedSignal(() => (this.parrafosRecurso.hasValue() ? this.parrafosRecurso.value() : []));
  protected readonly cargandoLista = this.parrafosRecurso.isLoading;
  protected readonly errorLista = computed(() =>
    this.parrafosRecurso.error() ? mensajeError(this.parrafosRecurso.error()) : null,
  );

  // Borrador del párrafo en edición.
  protected readonly editando = signal<Parrafo | null>(null);
  protected readonly contenido = signal<NodoContenido>(documentoVacio());
  protected readonly condicion = signal<GrupoCondiciones>(grupoVacio());
  protected readonly mostrarSoloSiCumple = signal(true);
  private readonly huellaGuardada = signal(huella(this.borradorActual()));

  protected readonly sucio = computed(() => huella(this.borradorActual()) !== this.huellaGuardada());
  protected readonly guardando = signal(false);
  protected readonly ocupado = signal(false);
  protected readonly errorGuardado = signal<string | null>(null);
  protected readonly conflicto = signal(false);

  /** Contexto de uso de los campos, por ejemplo "considerando". */
  protected readonly contextoCampos = computed(() => this.seccion().toLowerCase());
  protected readonly camposSeccion = computed(() => this.catalogo.camposDeContexto(this.contextoCampos()));
  protected readonly camposCondicion = this.catalogo.camposCondicionParrafo;
  protected readonly usados = computed(() => camposUsados(this.contenido()));
  /** El cursor del editor está dentro de una tabla (habilita los campos de lista). */
  protected readonly enTabla = signal(false);
  protected readonly posicionEditando = computed(() => {
    const id = this.editando()?.id;
    return id ? this.parrafos().findIndex((p) => p.id === id) + 1 : 0;
  });

  /** Motivos por los que todavía no se puede guardar, en lenguaje del usuario. */
  protected readonly problemas = computed(() => {
    const problemas: string[] = [];
    if (!tieneContenido(this.contenido())) {
      problemas.push(`Escriba el texto del ${this.nombre().elemento.toLowerCase()}.`);
    }
    if (contarCaracteres(this.contenido(), this.catalogo.etiqueta) > MAXIMO_CARACTERES) {
      problemas.push(`El texto supera los ${MAXIMO_CARACTERES} caracteres.`);
    }
    return [...problemas, ...problemasCondicion(this.condicion())];
  });

  protected readonly puedeGuardar = computed(
    () => this.problemas().length === 0 && !this.guardando() && (!this.editando() || this.sucio()),
  );

  constructor() {
    // Al cambiar de plantilla o de sección se empieza con un borrador vacío.
    effect(() => {
      this.idPlantilla();
      this.seccion();
      untracked(() => this.cargarBorrador(null));
    });
    // El selector de contexto consulta este indicador antes de cambiar de plantilla.
    effect(() => this.contexto.marcarCambios(this.seccion(), this.sucio()));
    inject(DestroyRef).onDestroy(() => this.contexto.marcarCambios(this.seccion(), false));
  }

  protected alSalir(evento: BeforeUnloadEvent): void {
    if (this.sucio()) {
      evento.preventDefault();
    }
  }

  protected insertarCampo(campo: Campo): void {
    if (this.editorTexto()?.insertarCampo(campo.id) === false && esCampoLista(campo)) {
      this.interaccion.avisar('error', `«${campo.etiqueta}» es un campo de lista: coloque el cursor dentro de una tabla.`);
    }
  }

  protected limpiarTexto(): void {
    this.editorTexto()?.limpiar();
  }

  protected async editar(parrafo: Parrafo): Promise<void> {
    if (this.editando()?.id === parrafo.id) {
      return;
    }
    if (this.sucio() && !(await this.confirmarDescartar())) {
      return;
    }
    this.cargarBorrador(parrafo);
  }

  protected async cancelar(): Promise<void> {
    if (this.sucio() && !(await this.confirmarDescartar())) {
      return;
    }
    this.cargarBorrador(null);
  }

  protected guardar(): void {
    if (!this.puedeGuardar()) {
      return;
    }
    const datos = {
      contenido: limpiarDocumento(this.contenido()),
      condicion: normalizarCondicion(this.condicion()),
      mostrarSoloSiCumple: this.mostrarSoloSiCumple(),
    };
    const editando = this.editando();
    const peticion: Observable<Parrafo> = editando
      ? this.api.actualizarParrafo({ ...editando, ...datos })
      : this.api.crearParrafo({
          plantillaId: this.plantilla().id,
          seccion: this.seccion(),
          orden: 0,
          activo: true,
          camposUsados: [],
          ...datos,
        });

    this.guardando.set(true);
    this.errorGuardado.set(null);
    this.conflicto.set(false);
    peticion.subscribe({
      next: () => {
        this.guardando.set(false);
        this.interaccion.avisar('exito', `${this.nombre().elemento} ${editando ? 'actualizado' : 'agregado'}.`);
        this.cargarBorrador(null);
        this.refrescar();
      },
      error: (error: unknown) => {
        this.guardando.set(false);
        this.conflicto.set(error instanceof HttpErrorResponse && error.status === 409);
        this.errorGuardado.set(
          this.conflicto()
            ? 'Otra persona modificó o eliminó este párrafo mientras lo editaba. Sus cambios siguen en el editor: ' +
                'recargue el listado y vuelva a guardar si desea reemplazar la versión nueva.'
            : mensajeError(error),
        );
      },
    });
  }

  /** Tras un conflicto: trae la versión actual sin perder lo que el usuario escribió. */
  protected recargarTrasConflicto(): void {
    this.conflicto.set(false);
    this.errorGuardado.set(null);
    this.refrescar();
  }

  protected async eliminar(parrafo: Parrafo): Promise<void> {
    const resumen = textoPlano(parrafo.contenido, this.catalogo.etiqueta);
    const aceptado = await this.interaccion.confirmar({
      titulo: `Eliminar ${this.nombre().elemento.toLowerCase()}`,
      mensaje: `Se eliminará el párrafo «${resumen.length > 120 ? resumen.slice(0, 120) + '…' : resumen}». Esta acción no se puede deshacer.`,
      aceptar: 'Eliminar',
      peligro: true,
    });
    if (!aceptado) {
      return;
    }
    this.ejecutar(this.api.eliminarParrafo(parrafo), 'Párrafo eliminado.', () => {
      if (this.editando()?.id === parrafo.id) {
        this.cargarBorrador(null);
      }
    });
  }

  protected duplicar(parrafo: Parrafo): void {
    this.ejecutar(this.api.duplicarParrafo(parrafo.id), 'Párrafo duplicado debajo del original.');
  }

  protected alternarActivo(parrafo: Parrafo): void {
    const activo = !parrafo.activo;
    this.ejecutar(
      this.api.actualizarParrafo({ ...parrafo, activo }),
      activo ? 'Párrafo activado.' : 'Párrafo desactivado: no aparecerá en el documento.',
    );
  }

  /** Subir o bajar una posición con las flechas. */
  protected mover(evento: { parrafo: Parrafo; desplazamiento: -1 | 1 }): void {
    const origen = this.parrafos().findIndex((p) => p.id === evento.parrafo.id);
    this.reordenar({ desde: origen, hasta: origen + evento.desplazamiento });
  }

  /** Lleva un párrafo de una posición a otra (arrastrar y soltar) y guarda el orden de toda la sección. */
  protected reordenar({ desde, hasta }: { desde: number; hasta: number }): void {
    const lista = [...this.parrafos()];
    if (desde === hasta || desde < 0 || hasta < 0 || desde >= lista.length || hasta >= lista.length) {
      return;
    }
    moveItemInArray(lista, desde, hasta);
    // Se muestra el nuevo orden de inmediato; si el servidor lo rechaza, se vuelve a cargar la lista.
    this.parrafos.set(lista);
    this.ocupado.set(true);
    this.api.reordenarParrafos(this.plantilla().id, this.seccion(), lista).subscribe({
      next: (ordenados) => {
        this.ocupado.set(false);
        this.parrafos.set(ordenados);
        this.sincronizarEditando(ordenados);
      },
      error: (error: unknown) => {
        this.ocupado.set(false);
        this.interaccion.avisar('error', mensajeError(error), 8000);
        this.refrescar();
      },
    });
  }

  /** Ejecuta una acción del listado, avisa el resultado y vuelve a cargar la lista. */
  private ejecutar(peticion: Observable<unknown>, mensajeExito: string, despues?: () => void): void {
    this.ocupado.set(true);
    peticion.subscribe({
      next: () => {
        this.ocupado.set(false);
        this.interaccion.avisar('exito', mensajeExito);
        despues?.();
        this.refrescar();
      },
      error: (error: unknown) => {
        this.ocupado.set(false);
        this.interaccion.avisar('error', mensajeError(error), 8000);
        this.refrescar();
      },
    });
  }

  /** Vuelve a consultar la lista. Las versiones nuevas del párrafo en edición se adoptan sin tocar el borrador. */
  private refrescar(): void {
    this.api.parrafos(this.plantilla().id, this.seccion()).subscribe({
      next: (lista) => {
        this.parrafos.set(lista);
        this.sincronizarEditando(lista);
      },
      error: (error: unknown) => this.interaccion.avisar('error', mensajeError(error), 8000),
    });
  }

  private sincronizarEditando(lista: Parrafo[]): void {
    const id = this.editando()?.id;
    if (!id) {
      return;
    }
    const actual = lista.find((p) => p.id === id);
    if (actual) {
      this.editando.set(actual);
    } else {
      // Lo eliminó otra persona: el texto queda en el editor como un párrafo nuevo.
      this.editando.set(null);
      this.interaccion.avisar('error', 'El párrafo que editaba ya no existe. Su texto quedó como un párrafo nuevo.', 8000);
    }
  }

  private cargarBorrador(parrafo: Parrafo | null): void {
    this.editando.set(parrafo);
    this.contenido.set(parrafo ? structuredClone(parrafo.contenido) : documentoVacio());
    this.condicion.set(parrafo?.condicion ? copiarGrupo(parrafo.condicion) : grupoVacio());
    this.mostrarSoloSiCumple.set(parrafo ? parrafo.mostrarSoloSiCumple : true);
    this.huellaGuardada.set(huella(this.borradorActual()));
    this.errorGuardado.set(null);
    this.conflicto.set(false);
    this.editorTexto()?.establecer(this.contenido());
    if (parrafo) {
      this.editorTexto()?.enfocar();
    }
  }

  private borradorActual(): Borrador {
    return {
      contenido: this.contenido(),
      condicion: this.condicion(),
      mostrarSoloSiCumple: this.mostrarSoloSiCumple(),
    };
  }

  private confirmarDescartar(): Promise<boolean> {
    return this.interaccion.confirmar({
      titulo: 'Cambios sin guardar',
      mensaje: 'El párrafo en edición tiene cambios sin guardar. ¿Desea descartarlos?',
      aceptar: 'Descartar cambios',
      peligro: true,
    });
  }
}
