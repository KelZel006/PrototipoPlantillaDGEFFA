import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from './api.service';

describe('ApiService', () => {
  let api: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('filtra los sub rubros por rubro', () => {
    api.subRubros('RUB-001').subscribe();
    const peticion = http.expectOne((r) => r.url === '/api/subrubros');
    expect(peticion.request.params.get('rubroId')).toBe('RUB-001');
    peticion.flush([]);
  });

  it('crea consolidados con POST indicando solo la unidad: el número lo asigna el servidor', () => {
    api.crearConsolidado('dispensas').subscribe();
    const peticion = http.expectOne('/api/consolidados');
    expect(peticion.request.method).toBe('POST');
    expect(peticion.request.body).toEqual({ unidadId: 'dispensas' });
    peticion.flush({});
  });

  it('agrega plantillas a un consolidado con CREAR y MODIFICAR', () => {
    api.agregarModificar('CON-1', 'PLA-001').subscribe();
    const modificar = http.expectOne('/api/consolidados/CON-1/propuestas/modificar');
    expect(modificar.request.body).toEqual({ plantillaId: 'PLA-001' });
    modificar.flush([]);

    api.agregarCrear('CON-1', { rubroId: 'R', subRubroId: 'S', tipoDocumentoId: 'T', claseDocumentoId: 'C' }).subscribe();
    http.expectOne('/api/consolidados/CON-1/propuestas/crear').flush([]);
  });

  it('edita los párrafos de la propuesta, nunca los de la plantilla vigente', () => {
    api.parrafos('PRO-001').subscribe();
    http.expectOne((r) => r.url === '/api/propuestas/PRO-001/parrafos' && !r.params.has('seccion')).flush([]);

    api.parrafos('PRO-001', 'RESUELVE').subscribe();
    expect(http.expectOne((r) => r.params.get('bloque') === 'RESUELVE').request.url).toBe('/api/propuestas/PRO-001/parrafos');
  });
});
