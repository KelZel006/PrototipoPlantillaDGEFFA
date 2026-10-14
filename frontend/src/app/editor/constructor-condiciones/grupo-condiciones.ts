import { AsyncPipe } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { Observable, of } from 'rxjs';
import { CatalogoService } from '../../core/catalogo.service';
import {
  ETIQUETAS_OPERADOR,
  PROFUNDIDAD_MAXIMA,
  grupoVacio,
  reglaNueva,
  requiereValor,
} from '../../core/condiciones';
import { Campo, GrupoCondiciones, OperadorComparacion, ReglaCondicion, TipoDato } from '../../core/modelos';

/** Instancia única: el pipe async no debe recibir un observable nuevo en cada detección de cambios. */
const SIN_SUGERENCIAS: Observable<string[]> = of([]);

/**
 * Un grupo de condiciones: filas Campo / Operador / Valor, el conector Y/O y subgrupos.
 * Es recursivo: cada subgrupo usa este mismo componente. No modifica el grupo recibido;
 * emite una copia con el cambio.
 */
@Component({
  selector: 'app-grupo-condiciones',
  imports: [AsyncPipe],
  templateUrl: './grupo-condiciones.html',
  styleUrl: './grupo-condiciones.css',
})
export class GrupoCondicionesComponente {
  private readonly catalogo = inject(CatalogoService);

  readonly grupo = input.required<GrupoCondiciones>();
  readonly campos = input.required<Campo[]>();
  readonly nivel = input(1);
  readonly identificador = input('c');

  readonly cambio = output<GrupoCondiciones>();
  /** Solo para subgrupos: pide al grupo padre que lo quite. */
  readonly quitar = output<void>();

  protected readonly etiquetasOperador = ETIQUETAS_OPERADOR;
  protected readonly requiereValor = requiereValor;
  protected readonly puedeAnidar = computed(() => this.nivel() < PROFUNDIDAD_MAXIMA);
  protected readonly cantidadElementos = computed(() => this.grupo().reglas.length + this.grupo().grupos.length);
  private readonly camposPorId = computed(() => new Map(this.campos().map((c) => [c.id, c] as const)));

  protected campo(regla: ReglaCondicion): Campo | undefined {
    return this.camposPorId().get(regla.campoId);
  }

  protected operadores(regla: ReglaCondicion): OperadorComparacion[] {
    const campo = this.campo(regla);
    const porTipo = this.catalogo.operadores();
    return campo && porTipo ? porTipo[campo.tipoDato] : [];
  }

  protected sugerencias(regla: ReglaCondicion): Observable<string[]> {
    const campo = this.campo(regla);
    return campo && campo.tipoDato === 'Texto' ? this.catalogo.valoresDe(campo.id) : SIN_SUGERENCIAS;
  }

  protected tipoEntrada(tipo: TipoDato | undefined): string {
    switch (tipo) {
      case 'Numero':
      case 'Moneda':
        return 'number';
      case 'Fecha':
        return 'date';
      default:
        return 'text';
    }
  }

  protected cambiarConector(operadorLogico: GrupoCondiciones['operadorLogico']): void {
    this.emitir({ ...this.grupo(), operadorLogico });
  }

  protected agregarRegla(): void {
    this.emitir({ ...this.grupo(), reglas: [...this.grupo().reglas, reglaNueva()] });
  }

  protected agregarGrupo(): void {
    const subgrupo = { ...grupoVacio(), reglas: [reglaNueva()] };
    this.emitir({ ...this.grupo(), grupos: [...this.grupo().grupos, subgrupo] });
  }

  protected quitarRegla(indice: number): void {
    this.emitir({ ...this.grupo(), reglas: this.grupo().reglas.filter((_, i) => i !== indice) });
  }

  /** Al cambiar de campo se elige el primer operador permitido para su tipo y se vacía el valor. */
  protected cambiarCampo(indice: number, campoId: string): void {
    const campo = this.camposPorId().get(campoId);
    const porTipo = this.catalogo.operadores();
    const operador = campo && porTipo ? porTipo[campo.tipoDato][0] : 'IgualA';
    this.reemplazarRegla(indice, { campoId, operador, valor: campo?.tipoDato === 'Booleano' ? 'true' : '' });
  }

  protected cambiarOperador(indice: number, operador: OperadorComparacion): void {
    const regla = this.grupo().reglas[indice];
    this.reemplazarRegla(indice, { ...regla, operador, valor: requiereValor(operador) ? regla.valor ?? '' : null });
  }

  protected cambiarValor(indice: number, valor: string): void {
    this.reemplazarRegla(indice, { ...this.grupo().reglas[indice], valor });
  }

  protected cambiarSubgrupo(indice: number, subgrupo: GrupoCondiciones): void {
    this.emitir({ ...this.grupo(), grupos: this.grupo().grupos.map((g, i) => (i === indice ? subgrupo : g)) });
  }

  protected quitarSubgrupo(indice: number): void {
    this.emitir({ ...this.grupo(), grupos: this.grupo().grupos.filter((_, i) => i !== indice) });
  }

  private reemplazarRegla(indice: number, regla: ReglaCondicion): void {
    this.emitir({ ...this.grupo(), reglas: this.grupo().reglas.map((r, i) => (i === indice ? regla : r)) });
  }

  private emitir(grupo: GrupoCondiciones): void {
    this.cambio.emit(grupo);
  }
}
