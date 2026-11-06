import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlantillaAplicable } from './plantilla-aplicable';

describe('PlantillaAplicable', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<PlantillaAplicable>;
  let elemento: HTMLElement;

  const estabilizar = async () => {
    TestBed.tick();
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  };
  const elegir = async (id: string, valor: string) => {
    const lista = elemento.querySelector<HTMLSelectElement>(`#${id}`)!;
    lista.value = valor;
    lista.dispatchEvent(new Event('change'));
    await estabilizar();
  };
  const buscar = () => elemento.querySelector<HTMLButtonElement>('.boton-primario')!;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [PlantillaAplicable],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PlantillaAplicable);
    fixture.componentRef.setInput('unidadId', 'exoneraciones');
    elemento = fixture.nativeElement;
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/rubros').flush([{ id: '1800', version: 1, nombre: 'Asociaciones', activo: true }]);
    http.expectOne('/api/tipos-documento').flush([]);
    http.expectOne('/api/clases-documento').flush([{ id: 'CDO-003', version: 1, nombre: 'Aclaratoria', activo: true }]);
    http.expectOne('/api/beneficios').flush([{ id: 'BEN-002', version: 1, nombre: 'ISR', activo: true }]);
    await estabilizar();

    await elegir('registro-rubro', '1800');
    http.expectOne((r) => r.url === '/api/subrubros').flush([{ id: '1801', version: 1, rubroId: '1800', nombre: 'ONGS', activo: true }]);
    await estabilizar();
  });

  afterEach(() => http.verify());

  it('busca con los datos del registro y omite las listas vacías', async () => {
    expect(buscar().disabled).toBe(true);
    await elegir('registro-subrubro', '1801');
    await elegir('registro-documento', 'CDO-003');
    await elegir('registro-beneficio', 'BEN-002');

    buscar().click();
    const peticion = http.expectOne((r) => r.url === '/api/plantillas-vigentes/aplicable');
    expect(peticion.request.params.keys().sort()).toEqual(['beneficioId', 'claseDocumentoId', 'rubroId', 'subRubroId', 'unidadId']);
    peticion.flush({ plantillaId: 'PLA-005', nombre: 'Plantilla (Aclaratoria) – ONGS', numeroVersion: 1 });
    await estabilizar();

    expect(elemento.querySelector('.resultado')?.textContent).toContain('PLA-005');
  });

  it('avisa si ninguna plantilla coincide', async () => {
    await elegir('registro-subrubro', '1801');
    buscar().click();
    http.expectOne((r) => r.url === '/api/plantillas-vigentes/aplicable').flush(null, { status: 204, statusText: 'No Content' });
    await estabilizar();

    expect(elemento.querySelector('.resultado')?.textContent).toContain('Ninguna plantilla vigente');
  });
});
