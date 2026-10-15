import { Component, computed, input, output, signal } from '@angular/core';
import { identificadorUnico } from '../../compartido/identificador';
import { Campo, TipoDato, esCampoLista } from '../../core/modelos';

const NOMBRES_TIPO: Record<TipoDato, string> = {
  Texto: 'Texto',
  Numero: 'Número',
  Moneda: 'Moneda',
  Fecha: 'Fecha',
  Booleano: 'Sí/No',
};

/** Normaliza para buscar sin distinguir mayúsculas ni tildes. */
function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/** Panel "Campos disponibles": búsqueda, agrupación por categoría e inserción en el cursor. */
@Component({
  selector: 'app-panel-campos',
  templateUrl: './panel-campos.html',
  styleUrl: './panel-campos.css',
})
export class PanelCampos {
  protected readonly uid = identificadorUnico('campos');

  readonly campos = input.required<Campo[]>();
  /** Campos presentes en el párrafo en edición. */
  readonly usados = input<string[]>([]);
  /** El cursor del editor está dentro de una tabla: solo entonces se pueden insertar campos de lista. */
  readonly enTabla = input(false);

  readonly insertar = output<Campo>();
  /** El usuario pidió ocultar el panel para ganar espacio. */
  readonly ocultar = output<void>();

  protected readonly busqueda = signal('');
  protected readonly nombresTipo = NOMBRES_TIPO;

  protected readonly grupos = computed(() => {
    const termino = normalizar(this.busqueda().trim());
    const visibles = this.campos().filter(
      (c) =>
        !termino ||
        normalizar(c.etiqueta).includes(termino) ||
        normalizar(c.id).includes(termino) ||
        normalizar(c.categoria).includes(termino),
    );
    const porCategoria = new Map<string, Campo[]>();
    for (const campo of visibles) {
      porCategoria.set(campo.categoria, [...(porCategoria.get(campo.categoria) ?? []), campo]);
    }
    return [...porCategoria.entries()].map(([categoria, campos]) => ({ categoria, campos }));
  });

  protected readonly esLista = esCampoLista;

  protected deshabilitado(campo: Campo): boolean {
    return esCampoLista(campo) && !this.enTabla();
  }

  protected estaUsado(campo: Campo): boolean {
    return this.usados().includes(campo.id);
  }
}
