import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from '../../core/api.service';
import { CatalogoService } from '../../core/catalogo.service';
import { describirGrupo, tieneReglas } from '../../core/condiciones';
import { textosCondicionados } from '../../core/contenido';
import { mensajeError } from '../../core/errores';
import { BloqueConfigurado, GrupoCondiciones, Parrafo, PlantillaEdicion, esCampoLista } from '../../core/modelos';
import { numeradorDe } from '../../core/numeracion';
import { VistaContenido } from './vista-contenido';

interface ElementoVista {
  parrafo: Parrafo;
  numeracion: string | null;
  continua: boolean;
  condicion: string | null;
  condicionSinAplicar: boolean;
  textosCondicionados: number;
}

/**
 * Plantilla completa para consultar (por ejemplo, el Oficial Jurídico al revisar): todos sus bloques en orden y cada
 * párrafo con su texto, su numeración prevista y sus marcas (condición, título, unido al anterior, inactivo).
 */
@Component({
  selector: 'app-vista-plantilla',
  imports: [VistaContenido],
  templateUrl: './vista-plantilla.html',
  styleUrl: './vista-plantilla.css',
})
export class VistaPlantilla {
  private readonly api = inject(ApiService);
  protected readonly catalogo = inject(CatalogoService);

  readonly plantilla = input.required<PlantillaEdicion>();

  private readonly recurso = rxResource({
    params: () => this.plantilla().id,
    stream: ({ params }) => this.api.parrafos(params),
  });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));
  private readonly parrafos = computed(() => (this.recurso.hasValue() ? this.recurso.value() : []));

  protected readonly esLista = (campoId: string) => {
    const campo = this.catalogo.porId().get(campoId);
    return !!campo && esCampoLista(campo);
  };
  protected readonly describir = (condicion: GrupoCondiciones) => describirGrupo(condicion, this.catalogo.porId());

  /** Cada bloque del tipo documental con sus párrafos en orden, numerados como en el listado del editor. */
  protected readonly bloques = computed(() =>
    this.plantilla().bloques.map((bloque: BloqueConfigurado) => {
      const numerar = numeradorDe(bloque.numeracion);
      const lista = this.parrafos().filter((p) => p.bloque === bloque.codigo).sort((a, b) => a.orden - b.orden);
      let numero = 0;
      const elementos: ElementoVista[] = lista.map((parrafo, indice) => {
        const continua = !!parrafo.unirConAnterior && !parrafo.esTitulo && indice > 0 && !lista[indice - 1].esTitulo;
        const conCondicion = tieneReglas(parrafo.condicion);
        return {
          parrafo,
          numeracion: numerar && parrafo.activo && !continua && !parrafo.esTitulo ? numerar(++numero) : null,
          continua,
          condicion: conCondicion && parrafo.condicion ? this.describir(parrafo.condicion) : null,
          condicionSinAplicar: conCondicion && !parrafo.mostrarSoloSiCumple,
          textosCondicionados: textosCondicionados(parrafo.contenido, this.catalogo.etiqueta).length,
        };
      });
      return { bloque, elementos };
    }),
  );
}
