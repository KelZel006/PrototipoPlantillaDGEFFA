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

  it('busca la plantilla por la combinación de las tres listas', () => {
    api
      .plantillas({ rubroId: 'RUB-001', subRubroId: 'SUB-001', tipoDocumentoId: 'TDO-001', claseDocumentoId: 'CDO-002' })
      .subscribe();
    const peticion = http.expectOne((r) => r.url === '/api/plantillas');
    expect(peticion.request.params.toString()).toBe(
      'rubroId=RUB-001&subRubroId=SUB-001&tipoDocumentoId=TDO-001&claseDocumentoId=CDO-002',
    );
    peticion.flush([]);
  });

  it('crea plantillas con POST a la API (nunca escribe archivos)', () => {
    api
      .crearPlantilla({
        nombre: 'N',
        rubroId: 'R',
        subRubroId: 'S',
        tipoDocumentoId: 'T',
        claseDocumentoId: 'C',
        estado: 'Borrador',
      })
      .subscribe();
    const peticion = http.expectOne('/api/plantillas');
    expect(peticion.request.method).toBe('POST');
    peticion.flush({});
  });

  it('agrega la sección solo cuando se indica', () => {
    api.parrafos('PLA-001').subscribe();
    expect(http.expectOne((r) => r.url === '/api/parrafos').request.params.has('seccion')).toBe(false);

    api.parrafos('PLA-001', 'Resuelve').subscribe();
    expect(http.expectOne((r) => r.params.get('seccion') === 'Resuelve').request.params.get('plantillaId')).toBe(
      'PLA-001',
    );
  });
});
