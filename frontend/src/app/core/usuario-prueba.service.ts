import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiService } from './api.service';
import { Rol, Usuario } from './modelos';

const CLAVE = 'usuario-prueba';
export const ENCABEZADO_USUARIO = 'X-Usuario-Prueba';
export const USUARIO_PREDETERMINADO = 'usuario.prueba';

/**
 * Usuario de prueba elegido. Va aparte de {@link UsuarioPruebaService} porque lo usa el interceptor HTTP, que no
 * puede depender de la API.
 */
@Injectable({ providedIn: 'root' })
export class UsuarioSeleccionado {
  readonly id = signal(leer());

  seleccionar(id: string): void {
    this.id.set(id);
    try {
      localStorage.setItem(CLAVE, id);
    } catch {
      // Sin almacenamiento (por ejemplo, navegación privada): la elección dura hasta recargar.
    }
  }
}

/**
 * Usuario de prueba con el que se simula el flujo (analista, Oficial Jurídico, administrador). Se envía al servidor
 * en un encabezado y el servidor decide los permisos. No es autenticación: solo existe en el prototipo.
 */
@Injectable({ providedIn: 'root' })
export class UsuarioPruebaService {
  private readonly api = inject(ApiService);
  private readonly seleccionado = inject(UsuarioSeleccionado);

  readonly id = this.seleccionado.id.asReadonly();

  private readonly recurso = rxResource({ stream: () => this.api.usuarios() });
  readonly usuarios = computed(() => (this.recurso.hasValue() ? this.recurso.value() : []));
  readonly actual = computed<Usuario | null>(() => this.usuarios().find((u) => u.id === this.id()) ?? null);

  tiene(rol: Rol): boolean {
    return this.actual()?.roles.includes(rol) ?? false;
  }

  seleccionar(id: string): void {
    this.seleccionado.seleccionar(id);
  }
}

function leer(): string {
  try {
    return localStorage.getItem(CLAVE) || USUARIO_PREDETERMINADO;
  } catch {
    return USUARIO_PREDETERMINADO;
  }
}

/** Agrega el usuario de prueba a cada petición a la API. */
export const usuarioPruebaInterceptor: HttpInterceptorFn = (peticion, siguiente) => {
  if (!peticion.url.startsWith('/api')) {
    return siguiente(peticion);
  }
  const usuario = inject(UsuarioSeleccionado).id();
  return siguiente(peticion.clone({ setHeaders: { [ENCABEZADO_USUARIO]: usuario } }));
};
