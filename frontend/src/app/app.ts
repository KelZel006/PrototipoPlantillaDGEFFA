import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { Avisos } from './compartido/avisos';
import { DialogoConfirmacion } from './compartido/dialogo-confirmacion';
import { Rol } from './core/modelos';
import { UsuarioPruebaService } from './core/usuario-prueba.service';
import { EstadoDatos } from './editor/estado-datos/estado-datos';

const NOMBRE_ROL: Record<Rol, string> = {
  Analista: 'Analista',
  OficialJuridico: 'Oficial Jurídico',
  Administrador: 'Administrador',
};

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, EstadoDatos, DialogoConfirmacion, Avisos],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly usuario = inject(UsuarioPruebaService);

  protected nombresRoles(roles: Rol[]): string {
    return roles.map((r) => NOMBRE_ROL[r]).join(', ');
  }
}
