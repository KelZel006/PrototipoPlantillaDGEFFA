import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { responderUnidades } from './pruebas/datos-prueba';
import { ApiService } from './core/api.service';
import { UsuarioPruebaService, usuarioPruebaInterceptor } from './core/usuario-prueba.service';

describe('App', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(withInterceptors([usuarioPruebaInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
  });

  it('muestra el título, el usuario de prueba y la advertencia de datos de prueba', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;

    expect(elemento.querySelector('h1')?.textContent).toContain('Editor de Resoluciones');
    expect(elemento.textContent).toContain('Prototipo · datos de prueba');
    expect(elemento.textContent).toContain('Usuario de prueba');

    // Al iniciar consulta los usuarios de prueba y el diagnóstico, solo a través de la API.
    TestBed.tick();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/usuarios').flush([
      { id: 'usuario.prueba', version: 1, nombreCompleto: 'Usuario de prueba', roles: ['Analista', 'Administrador'], activo: true },
      { id: 'j.ramos', version: 1, nombreCompleto: 'Julia Ramos', roles: ['OficialJuridico'], activo: true },
    ]);
    http.expectOne('/api/salud').flush({ estado: 'correcto', almacenamiento: 'JSON', colecciones: [] });
    responderUnidades(http);
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
    expect(elemento.querySelector('select')?.textContent).toContain('Julia Ramos · Oficial Jurídico');
    // El menú muestra las cuatro unidades.
    expect(Array.from(elemento.querySelectorAll('.menu-unidades a')).map((a) => a.textContent?.trim())).toEqual([
      'Exoneraciones',
      'Dispensas',
      'Registro Exonerados',
      'Notas de Crédito y Devoluciones',
    ]);
    http.verify();
  });

  it('envía el usuario de prueba elegido en cada petición a la API', () => {
    const http = TestBed.inject(HttpTestingController);
    const api = TestBed.inject(ApiService);

    api.consolidados().subscribe();
    expect(http.expectOne('/api/consolidados').request.headers.get('X-Usuario-Prueba')).toBe('usuario.prueba');

    TestBed.inject(UsuarioPruebaService).seleccionar('j.ramos');
    api.consolidados().subscribe();
    expect(http.expectOne('/api/consolidados').request.headers.get('X-Usuario-Prueba')).toBe('j.ramos');
    expect(localStorage.getItem('usuario-prueba')).toBe('j.ramos');
  });
});
