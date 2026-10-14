import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { ContextoService } from '../../core/contexto.service';
import { PanelPlantilla } from './panel-plantilla';

registerLocaleData(localeEs);

const plantilla = {
  id: 'PLA-001',
  version: 1,
  nombre: 'Plantilla de prueba',
  rubroId: 'RUB-001',
  subRubroId: 'SUB-001',
  tipoDocumentoId: 'TDO-001',
  estado: 'Borrador',
  fechaModificacion: '2026-10-14T00:00:00+00:00',
};

describe('PanelPlantilla', () => {
  let fixture: ComponentFixture<PanelPlantilla>;
  let http: HttpTestingController;
  let elemento: HTMLElement;

  // No se usa whenStable(): esperaría a que respondan las peticiones pendientes de los recursos.
  const estabilizar = async () => {
    TestBed.tick();
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PanelPlantilla],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: LOCALE_ID, useValue: 'es' }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PanelPlantilla);
    elemento = fixture.nativeElement;
  });

  afterEach(() => http.verify());

  const seleccionarCombinacion = () => {
    const contexto = TestBed.inject(ContextoService);
    contexto.seleccionarRubro({ id: 'RUB-001', version: 1, nombre: 'Rubro', activo: true });
    contexto.seleccionarSubRubro({ id: 'SUB-001', version: 1, rubroId: 'RUB-001', nombre: 'Sub', activo: true });
    contexto.seleccionarTipoDocumento({ id: 'TDO-001', version: 1, nombre: 'Tipo', activo: true });
  };

  it('pide completar las tres listas antes de consultar', async () => {
    fixture.detectChanges();
    await estabilizar();
    expect(elemento.textContent).toContain('Seleccione Rubro, Sub Rubro y Tipo Documento');
  });

  it('muestra el resumen de párrafos por sección', async () => {
    seleccionarCombinacion();
    fixture.detectChanges();
    await estabilizar();
    http.expectOne((r) => r.url === '/api/plantillas').flush([plantilla]);
    await estabilizar();
    http.expectOne((r) => r.url === '/api/parrafos').flush([
      { id: 'P1', seccion: 'Considerando', activo: true, condicionId: 'CON-001' },
      { id: 'P2', seccion: 'Considerando', activo: false },
      { id: 'P3', seccion: 'Resuelve', activo: true },
    ]);
    await estabilizar();

    const secciones = Array.from(elemento.querySelectorAll('.seccion')).map((s) =>
      Array.from(s.children)
        .map((parte) => parte.textContent?.replace(/\s+/g, ' ').trim())
        .join(' '),
    );
    expect(secciones[0]).toContain('Cabecera 0 párrafos');
    expect(secciones[1]).toContain('Considerando 2 párrafos · 1 activo · 1 con condición');
    expect(secciones[2]).toContain('Resuelve 1 párrafo');
  });

  it('crea la plantilla cuando la combinación no tiene una', async () => {
    seleccionarCombinacion();
    fixture.detectChanges();
    await estabilizar();
    http.expectOne((r) => r.url === '/api/plantillas').flush([]);
    await estabilizar();

    const boton = elemento.querySelector<HTMLButtonElement>('.boton-primario')!;
    expect(boton.textContent).toContain('Crear plantilla de prueba');
    boton.click();

    const creacion = http.expectOne((r) => r.method === 'POST' && r.url === '/api/plantillas');
    expect(creacion.request.body).toMatchObject({ rubroId: 'RUB-001', subRubroId: 'SUB-001', tipoDocumentoId: 'TDO-001' });
    creacion.flush(plantilla);
    await estabilizar();

    // Tras crearla se vuelve a consultar.
    http.expectOne((r) => r.method === 'GET' && r.url === '/api/plantillas').flush([plantilla]);
    await estabilizar();
    http.expectOne((r) => r.url === '/api/parrafos').flush([]);
    await estabilizar();
    expect(elemento.textContent).toContain('PLA-001');
  });

  it('muestra los errores de validación del servidor al crear', async () => {
    seleccionarCombinacion();
    fixture.detectChanges();
    await estabilizar();
    http.expectOne((r) => r.url === '/api/plantillas').flush([]);
    await estabilizar();

    elemento.querySelector<HTMLButtonElement>('.boton-primario')!.click();
    http
      .expectOne((r) => r.method === 'POST')
      .flush(
        { title: 'Los datos enviados no son válidos.', errors: { tipoDocumentoId: ['Ya existe una plantilla.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await estabilizar();

    expect(elemento.querySelector('[role="alert"]')?.textContent).toContain('Ya existe una plantilla.');
  });
});
