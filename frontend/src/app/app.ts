import { Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Avisos } from './compartido/avisos';
import { DialogoConfirmacion } from './compartido/dialogo-confirmacion';
import { Rol } from './core/modelos';
import { UnidadService } from './core/unidad.service';
import { UsuarioPruebaService } from './core/usuario-prueba.service';
import { EstadoDatos } from './editor/estado-datos/estado-datos';

const NOMBRE_ROL: Record<Rol, string> = {
  Analista: 'Analista',
  OficialJuridico: 'Oficial Jurídico',
  Administrador: 'Administrador',
};

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, EstadoDatos, DialogoConfirmacion, Avisos],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly usuario = inject(UsuarioPruebaService);
  protected readonly unidades = inject(UnidadService);

  /** El botón ☰ oculta el menú lateral (en pantallas pequeñas, lo muestra). */
  protected readonly menuAlternado = signal(false);
  /** Unidad desplegada en el menú: de entrada, la abierta. */
  protected readonly abierta = linkedSignal<string | null>(() => this.unidades.actual());

  /** Iniciales del perfil conectado, para el avatar del menú. */
  protected readonly iniciales = computed(() =>
    (this.usuario.actual()?.nombreCompleto ?? this.usuario.id())
      .split(/[\s.]+/)
      .filter((parte) => parte && !parte.startsWith('('))
      .slice(0, 2)
      .map((parte) => parte[0].toUpperCase())
      .join(''),
  );

  protected nombresRoles(roles: Rol[]): string {
    return roles.map((r) => NOMBRE_ROL[r]).join(', ');
  }
}
