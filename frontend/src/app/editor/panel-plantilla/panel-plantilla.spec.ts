import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { provideRouter } from '@angular/router';
import { InteraccionService } from '../../core/interaccion.service';
import { PlantillaEdicion } from '../../core/modelos';
import { PlantillaActualService } from '../../core/plantilla-actual.service';
import { PLANTILLA } from '../../pruebas/datos-prueba';
import { PanelPlantilla } from './panel-plantilla';

registerLocaleData(localeEs);

const TIPOS = [
  { id: 'TDO-001', version: 1, nombre: 'Resolución', activo: true },
  { id: 'TDO-004', version: 1, nombre: 'Certificación', textoCorrido: true, copiaDeTipoDocumentoId: 'TDO-001', activo: true },
];

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
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: LOCALE_ID, useValue: 'es' }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PanelPlantilla);
    elemento = fixture.nativeElement;
  });

  afterEach(() => http.verify());

  /** Abre la propuesta y responde las consultas iniciales. */
  const abrir = async (propuesta: PlantillaEdicion, solicitudes: unknown[] = [], bienes: unknown[] = []) => {
    TestBed.inject(PlantillaActualService).abrir(propuesta.id);
    fixture.detectChanges();
    await estabilizar();
    http.expectOne('/api/tipos-documento').flush(TIPOS);
    http.expectOne(`/api/propuestas/${propuesta.id}/edicion`).flush(propuesta);
    await estabilizar();
    http.expectOne('/api/solicitudes').flush(solicitudes);
    http.expectOne('/api/detalles-bienes').flush(bienes);
    await estabilizar();
  };

  it('muestra la operación, el documento consolidado y el diseño si la propuesta es editable', async () => {
    await abrir(PLANTILLA);

    expect(elemento.textContent).toContain('MODIFICAR');
    expect(elemento.textContent).toContain('Plantilla de prueba');
    expect(elemento.querySelector('a')?.textContent).toContain('PLAN-20260001');
    expect(elemento.textContent).toContain('EN REGISTRO');
    expect(elemento.querySelector('app-diseno-documento')).not.toBeNull();
    expect(elemento.textContent).not.toContain('Solo lectura');
  });

  it('sin permiso de edición queda en solo lectura, sin el diseño', async () => {
    await abrir({ ...PLANTILLA, editable: false, estadoConsolidado: 'Verificado' });

    expect(elemento.textContent).toContain('Solo lectura');
    expect(elemento.textContent).toContain('VERIFICADO');
    expect(elemento.querySelector('app-diseno-documento')).toBeNull();
  });

  it('la Certificación que acompaña a la Resolución lo avisa y advierte el texto corrido', async () => {
    await abrir({ ...PLANTILLA, id: 'PRO-002', tipoDocumentoId: 'TDO-004', derivadaDePropuestaId: 'PRO-001' });

    expect(elemento.querySelector('.aviso-copia')?.textContent).toContain('acompaña a la Resolución');
    expect(elemento.querySelector('.nota-corrido')?.textContent).toContain('Certificación: el texto se genera corrido');
  });

  describe('vista previa en PDF', () => {
    const solicitudes = [
      { id: 'SOL-004', version: 1, numeroSolicitud: 'P-4', beneficiario: 'Otro', rubroId: 'RUB-002', subRubroId: 'SUB-003', esDatoPrueba: true },
      { id: 'SOL-001', version: 1, numeroSolicitud: 'P-1', beneficiario: 'Uno', rubroId: 'RUB-001', subRubroId: 'SUB-001', esDatoPrueba: true },
    ];
    const bienes = [
      { id: 'BIE-1', version: 1, solicitudId: 'SOL-001', codigoArancelario: '1' },
      { id: 'BIE-2', version: 1, solicitudId: 'SOL-001', codigoArancelario: '2' },
    ];
    let ventana: { location: { href: string }; close: ReturnType<typeof vi.fn> };

    const botonPdf = () =>
      Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes('Vista previa en PDF'))!;

    beforeEach(() => {
      ventana = { location: { href: '' }, close: vi.fn() };
      vi.spyOn(window, 'open').mockReturnValue(ventana as unknown as Window);
      URL.createObjectURL = vi.fn(() => 'blob:pdf-de-prueba');
      URL.revokeObjectURL = vi.fn();
    });

    afterEach(() => vi.restoreAllMocks());

    it('propone primero la solicitud de la misma combinación y abre el PDF de la propuesta', async () => {
      await abrir(PLANTILLA, solicitudes, bienes);
      const opciones = Array.from(elemento.querySelectorAll('.solicitud option')).map((o) => o.textContent?.trim());
      expect(opciones.map((o) => o?.replace(/\s+/g, ' '))).toEqual([
        'Sin datos (mostrar los campos)',
        'P-1 – Uno · 2 bienes',
        'P-4 – Otro · sin bienes (otro rubro)',
      ]);

      botonPdf().click();
      await estabilizar();
      const peticion = http.expectOne((r) => r.url === '/api/propuestas/PRO-001/pdf');
      expect(peticion.request.params.get('solicitudId')).toBe('SOL-001');
      expect(peticion.request.params.get('interlineado')).toBe('1.5');
      expect(peticion.request.responseType).toBe('blob');
      peticion.flush(new Blob(['%PDF-1.4'], { type: 'application/pdf' }));
      await estabilizar();

      expect(ventana.location.href).toBe('blob:pdf-de-prueba');
    });

    it('sin solicitud no envía el parámetro y muestra los errores del servidor', async () => {
      await abrir(PLANTILLA, solicitudes, bienes);
      const lista = elemento.querySelector<HTMLSelectElement>('.solicitud select')!;
      lista.value = '';
      lista.dispatchEvent(new Event('change'));
      const interlineado = elemento.querySelector<HTMLSelectElement>('.interlineado select')!;
      expect(Array.from(interlineado.options).map((o) => o.value)).toEqual([
        '0.8', '0.9', '1.0', '1.1', '1.2', '1.3', '1.4', '1.5', '1.6', '1.7', '1.8', '1.9', '2.0',
      ]);
      interlineado.value = '0.9';
      interlineado.dispatchEvent(new Event('change'));

      botonPdf().click();
      await estabilizar();
      const peticion = http.expectOne((r) => r.url === '/api/propuestas/PRO-001/pdf');
      expect(peticion.request.params.has('solicitudId')).toBe(false);
      expect(peticion.request.params.get('interlineado')).toBe('0.9');
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
});
