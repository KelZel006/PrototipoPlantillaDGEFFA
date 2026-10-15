import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { ContextoService } from '../../core/contexto.service';
import { InteraccionService } from '../../core/interaccion.service';
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

  it('muestra la plantilla de la combinación elegida', async () => {
    seleccionarCombinacion();
    fixture.detectChanges();
    await estabilizar();
    const peticion = http.expectOne((r) => r.url === '/api/plantillas');
    expect(peticion.request.params.get('tipoDocumentoId')).toBe('TDO-001');
    peticion.flush([plantilla]);
    await estabilizar();
    http.expectOne('/api/solicitudes').flush([]);
    http.expectOne('/api/detalles-bienes').flush([]);
    await estabilizar();

    expect(elemento.querySelector('h2')?.textContent).toContain('Plantilla de prueba');
    expect(elemento.textContent).toContain('PLA-001');
    expect(elemento.textContent).toContain('Borrador');
  });

  describe('generar PDF', () => {
    const solicitudes = [
      { id: 'SOL-004', version: 1, numeroSolicitud: 'P-4', beneficiario: 'Otro', rubroId: 'RUB-002', subRubroId: 'SUB-003', esDatoPrueba: true },
      { id: 'SOL-001', version: 1, numeroSolicitud: 'P-1', beneficiario: 'Uno', rubroId: 'RUB-001', subRubroId: 'SUB-001', esDatoPrueba: true },
    ];
    let ventana: { location: { href: string }; close: ReturnType<typeof vi.fn> };

    const cargarPlantilla = async () => {
      seleccionarCombinacion();
      fixture.detectChanges();
      await estabilizar();
      http.expectOne((r) => r.url === '/api/plantillas').flush([plantilla]);
      await estabilizar();
      http.expectOne('/api/solicitudes').flush(solicitudes);
      http.expectOne('/api/detalles-bienes').flush([
        { id: 'BIE-1', version: 1, solicitudId: 'SOL-001', codigoArancelario: '1' },
        { id: 'BIE-2', version: 1, solicitudId: 'SOL-001', codigoArancelario: '2' },
      ]);
      await estabilizar();
    };
    const botonPdf = () =>
      Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes('Generar PDF'))!;

    beforeEach(() => {
      ventana = { location: { href: '' }, close: vi.fn() };
      vi.spyOn(window, 'open').mockReturnValue(ventana as unknown as Window);
      URL.createObjectURL = vi.fn(() => 'blob:pdf-de-prueba');
      URL.revokeObjectURL = vi.fn();
    });

    afterEach(() => vi.restoreAllMocks());

    it('propone primero la solicitud de la misma combinación y abre el PDF', async () => {
      await cargarPlantilla();
      const opciones = Array.from(elemento.querySelectorAll('.solicitud option')).map((o) => o.textContent?.trim());
      expect(opciones.map((o) => o?.replace(/\s+/g, ' '))).toEqual([
        'Sin datos (mostrar los campos)',
        'P-1 – Uno · 2 bienes',
        'P-4 – Otro · sin bienes (otro rubro)',
      ]);

      botonPdf().click();
      await estabilizar();
      const peticion = http.expectOne((r) => r.url === '/api/plantillas/PLA-001/pdf');
      expect(peticion.request.params.get('solicitudId')).toBe('SOL-001');
      expect(peticion.request.params.get('interlineado')).toBe('1.5');
      expect(peticion.request.responseType).toBe('blob');
      peticion.flush(new Blob(['%PDF-1.4'], { type: 'application/pdf' }));
      await estabilizar();

      expect(ventana.location.href).toBe('blob:pdf-de-prueba');
    });

    it('sin solicitud no envía el parámetro y muestra los errores del servidor', async () => {
      await cargarPlantilla();
      const lista = elemento.querySelector<HTMLSelectElement>('.solicitud select')!;
      lista.value = '';
      lista.dispatchEvent(new Event('change'));
      const interlineado = elemento.querySelector<HTMLSelectElement>('.interlineado select')!;
      expect(Array.from(interlineado.options).map((o) => o.textContent?.trim())).toEqual(['Sencillo', '1.15', '1.5', 'Doble']);
      interlineado.value = '2';
      interlineado.dispatchEvent(new Event('change'));

      botonPdf().click();
      await estabilizar();
      const peticion = http.expectOne((r) => r.url === '/api/plantillas/PLA-001/pdf');
      expect(peticion.request.params.has('solicitudId')).toBe(false);
      expect(peticion.request.params.get('interlineado')).toBe('2');
      peticion.flush(new Blob([JSON.stringify({ title: 'Registro no encontrado.' })], { type: 'application/json' }), {
        status: 404,
        statusText: 'Not Found',
      });
      await estabilizar();
      await estabilizar();

      expect(ventana.close).toHaveBeenCalled();
      const interaccion = TestBed.inject(InteraccionService);
      expect(interaccion.avisos().map((a) => a.texto)).toContain('No se pudo generar el PDF. Registro no encontrado.');
    });
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
    http.expectOne('/api/solicitudes').flush([]);
    http.expectOne('/api/detalles-bienes').flush([]);
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
