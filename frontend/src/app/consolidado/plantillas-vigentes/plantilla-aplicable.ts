import { Component, inject, input, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { ContextoService } from '../../core/contexto.service';
import { mensajeError } from '../../core/errores';
import { PlantillaVigente } from '../../core/modelos';
import { SelectorContexto } from '../../editor/selector-contexto/selector-contexto';

/**
 * Consulta qué plantilla vigente le corresponde a un registro: la más específica que coincida con sus datos. Por ejemplo,
 * una Aclaratoria de ONGS con ISV usa la plantilla de ISV, si existe, y con ISR usa la general de Aclaratoria de ONGS.
 */
@Component({
  selector: 'app-plantilla-aplicable',
  imports: [SelectorContexto],
  providers: [ContextoService],
  template: `
    <p class="texto-secundario sin-margen">Indique los datos del registro; deje vacías las listas que no tenga.</p>
    <app-selector-contexto etiqueta="Datos del registro" prefijo="registro" />
    <div>
      <button type="button" class="boton boton-primario boton-compacto" [disabled]="!contexto.completo() || buscando()" (click)="buscar()">
        {{ buscando() ? 'Buscando…' : 'Buscar plantilla' }}
      </button>
    </div>
    @if (error(); as mensaje) {
      <div class="aviso aviso-error" role="alert">{{ mensaje }}</div>
    } @else if (consultado()) {
      <p class="resultado" role="status">
        @if (resultado(); as p) {
          Corresponde <strong>{{ p.nombre }}</strong> (<code>{{ p.plantillaId }}</code>, versión vigente {{ p.numeroVersion }}).
        } @else {
          Ninguna plantilla vigente coincide con esos datos.
        }
      </p>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--espacio-2);
    }
    .resultado {
      margin: 0;
      padding: var(--espacio-2) var(--espacio-3);
      border-radius: var(--radio);
      background: var(--primario-suave);
      font-size: var(--texto-pequeno);
    }
  `,
})
export class PlantillaAplicable {
  private readonly api = inject(ApiService);
  protected readonly contexto = inject(ContextoService);

  readonly unidadId = input.required<string>();

  protected readonly buscando = signal(false);
  protected readonly consultado = signal(false);
  protected readonly resultado = signal<PlantillaVigente | null>(null);
  protected readonly error = signal<string | null>(null);

  protected buscar(): void {
    const rubro = this.contexto.rubro();
    const subRubro = this.contexto.subRubro();
    if (!rubro || !subRubro) {
      return;
    }
    this.buscando.set(true);
    this.error.set(null);
    this.api
      .plantillaAplicable({
        unidadId: this.unidadId(),
        rubroId: rubro.id,
        subRubroId: subRubro.id,
        tipoDocumentoId: this.contexto.tipoDocumento()?.id,
        claseDocumentoId: this.contexto.claseDocumento()?.id,
        beneficioId: this.contexto.beneficio()?.id,
      })
      .subscribe({
        next: (plantilla) => {
          this.buscando.set(false);
          this.consultado.set(true);
          this.resultado.set(plantilla);
        },
        error: (e: unknown) => {
          this.buscando.set(false);
          this.error.set(mensajeError(e));
        },
      });
  }
}
