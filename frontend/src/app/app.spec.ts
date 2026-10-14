import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('muestra el título y la advertencia de datos de prueba', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;

    expect(elemento.querySelector('h1')?.textContent).toContain('Editor de Resoluciones');
    expect(elemento.textContent).toContain('Prototipo · datos de prueba');

    // Al iniciar, consulta los catálogos y el diagnóstico solo a través de la API.
    TestBed.tick();
    const http = TestBed.inject(HttpTestingController);
    for (const url of ['/api/rubros', '/api/tipos-documento', '/api/salud']) {
      http.expectOne(url).flush([]);
    }
    http.verify();
  });
});
