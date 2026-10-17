import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { mensajeError } from '../../core/errores';
import { InteraccionService } from '../../core/interaccion.service';
import { EstadoConsolidado, NOMBRE_ESTADO } from '../../core/modelos';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { InsigniaEstado } from '../insignia-estado';

/**
 * Pantalla A: Documentos Consolidados que el usuario puede ver. El servidor decide qué entra en cada bandeja: el
 * analista ve solo los suyos (sin los que ya envió a verificación) y el Oficial Jurídico, los VERIFICADOS y APROBADOS.
 */
@Component({
  selector: 'app-lista-consolidados',
  imports: [DatePipe, RouterLink, InsigniaEstado],
  templateUrl: './lista-consolidados.html',
  styleUrl: './lista-consolidados.css',
})
export class ListaConsolidados {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly interaccion = inject(InteraccionService);
  protected readonly usuario = inject(UsuarioPruebaService);

  // Se vuelve a consultar al cambiar de usuario de prueba: cambia la bandeja.
  private readonly recurso = rxResource({
    params: () => this.usuario.id(),
    stream: () => this.api.consolidados(),
  });

  protected readonly cargando = this.recurso.isLoading;
  protected readonly error = computed(() => (this.recurso.error() ? mensajeError(this.recurso.error()) : null));
  private readonly todos = computed(() => (this.recurso.hasValue() ? this.recurso.value() : []));

  protected readonly estados: { valor: EstadoConsolidado; nombre: string }[] = (
    ['EnRegistro', 'Verificado', 'Aprobado'] as const
  ).map((valor) => ({ valor, nombre: NOMBRE_ESTADO[valor] }));

  // Filtros.
  protected readonly numero = signal('');
  protected readonly anio = signal('');
  protected readonly estado = signal('');
  protected readonly analista = signal('');
  protected readonly tipo = signal('');
  protected readonly fecha = signal('');

  protected readonly anios = computed(() => [...new Set(this.todos().map((c) => c.anio))].sort((a, b) => b - a));
  protected readonly analistas = computed(() => [...new Set(this.todos().map((c) => c.analistaId))].sort());
  protected readonly tipos = computed(() => [...new Set(this.todos().flatMap((c) => c.tiposDocumento))].sort());

  protected readonly filtrados = computed(() => {
    const numero = this.numero().trim().toUpperCase();
    return this.todos().filter(
      (c) =>
        (!numero || c.numero.includes(numero)) &&
        (!this.anio() || String(c.anio) === this.anio()) &&
        (!this.estado() || c.estado === this.estado()) &&
        (!this.analista() || c.analistaId === this.analista()) &&
        (!this.tipo() || c.tiposDocumento.includes(this.tipo())) &&
        (!this.fecha() || c.fechaCreacion.startsWith(this.fecha())),
    );
  });

  protected readonly hayFiltros = computed(
    () => !!(this.numero() || this.anio() || this.estado() || this.analista() || this.tipo() || this.fecha()),
  );

  protected readonly creando = signal(false);

  protected crear(): void {
    if (this.creando()) {
      return;
    }
    this.creando.set(true);
    this.api.crearConsolidado().subscribe({
      next: (consolidado) => {
        this.creando.set(false);
        this.interaccion.avisar('exito', `Se creó el documento ${consolidado.numero}.`);
        void this.router.navigate(['/consolidados', consolidado.id]);
      },
      error: (error: unknown) => {
        this.creando.set(false);
        this.interaccion.avisar('error', mensajeError(error), 8000);
      },
    });
  }

  protected limpiar(): void {
    for (const filtro of [this.numero, this.anio, this.estado, this.analista, this.tipo, this.fecha]) {
      filtro.set('');
    }
  }

  protected recargar(): void {
    this.recurso.reload();
  }
}
