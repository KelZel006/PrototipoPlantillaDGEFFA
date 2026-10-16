import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ContextoService } from '../../core/contexto.service';
import { InteraccionService } from '../../core/interaccion.service';
import { SelectorContexto } from './selector-contexto';

describe('SelectorContexto', () => {
  let fixture: ComponentFixture<SelectorContexto>;
  let http: HttpTestingController;
  let elemento: HTMLElement;

  const lista = (id: string) => elemento.querySelector<HTMLSelectElement>(`#${id}`)!;
  const elegir = (id: string, valor: string) => {
    const select = lista(id);
    select.value = valor;
    select.dispatchEvent(new Event('change'));
  };
  // No se usa whenStable(): esperaría a que respondan las peticiones pendientes de los recursos.
  const estabilizar = async () => {
    TestBed.tick();
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [SelectorContexto],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SelectorContexto);
    elemento = fixture.nativeElement;
    fixture.detectChanges();
    TestBed.tick();

    http.expectOne('/api/rubros').flush([
      { id: 'RUB-001', version: 1, nombre: 'Rubro activo', activo: true },
      { id: 'RUB-009', version: 1, nombre: 'Rubro inactivo', activo: false },
    ]);
    http.expectOne('/api/tipos-documento').flush([{ id: 'TDO-001', version: 1, nombre: 'Aprobación', activo: true }]);
    http.expectOne('/api/clases-documento').flush([
      { id: 'CDO-001', version: 1, nombre: 'Original', activo: true },
      { id: 'CDO-002', version: 1, nombre: 'Modificación', activo: true },
      { id: 'CDO-003', version: 1, nombre: 'Aclaratoria', activo: true },
    ]);
    await estabilizar();
  });

  afterEach(() => http.verify());

  it('muestra las cuatro listas con sus etiquetas', () => {
    const etiquetas = Array.from(elemento.querySelectorAll('label')).map((l) => l.textContent?.trim());
    expect(etiquetas).toEqual(['Rubro', 'Sub Rubro', 'Tipo Documento', 'Documento']);
    expect(Array.from(lista('lista-documento').options).map((o) => o.textContent?.trim())).toEqual([
      'Seleccione el documento',
      'Original',
      'Modificación',
      'Aclaratoria',
    ]);
  });

  it('solo ofrece registros activos', () => {
    const opciones = Array.from(lista('lista-rubro').options).map((o) => o.textContent?.trim());
    expect(opciones).toEqual(['Seleccione un rubro', 'Rubro activo']);
  });

  it('habilita y carga Sub Rubro según el rubro elegido', async () => {
    expect(lista('lista-subrubro').disabled).toBe(true);

    elegir('lista-rubro', 'RUB-001');
    await estabilizar();
    const peticion = http.expectOne((r) => r.url === '/api/subrubros');
    expect(peticion.request.params.get('rubroId')).toBe('RUB-001');
    peticion.flush([{ id: 'SUB-001', version: 1, rubroId: 'RUB-001', nombre: 'Sub uno', activo: true }]);
    await estabilizar();

    expect(lista('lista-subrubro').disabled).toBe(false);
    elegir('lista-subrubro', 'SUB-001');
    elegir('lista-tipo', 'TDO-001');
    await estabilizar();
    expect(TestBed.inject(ContextoService).completo()).toBe(false);

    elegir('lista-documento', 'CDO-002');
    await estabilizar();
    expect(TestBed.inject(ContextoService).claseDocumento()?.nombre).toBe('Modificación');
    expect(TestBed.inject(ContextoService).completo()).toBe(true);
  });

  it('pide confirmación si hay cambios sin guardar y respeta la cancelación', async () => {
    const contexto = TestBed.inject(ContextoService);
    const interaccion = TestBed.inject(InteraccionService);
    contexto.marcarCambios('Considerando', true);
    const confirmar = vi.spyOn(interaccion, 'confirmar').mockResolvedValue(false);

    elegir('lista-tipo', 'TDO-001');
    await estabilizar();

    expect(confirmar).toHaveBeenCalledOnce();
    expect(contexto.tipoDocumento()).toBeNull();
    expect(lista('lista-tipo').value).toBe('');

    confirmar.mockResolvedValue(true);
    elegir('lista-tipo', 'TDO-001');
    await estabilizar();

    expect(contexto.tipoDocumento()?.id).toBe('TDO-001');
    expect(contexto.cambiosSinGuardar()).toBe(false);
  });

  it('informa los errores de conexión con opción de reintentar', async () => {
    elegir('lista-rubro', 'RUB-001');
    await estabilizar();
    http.expectOne((r) => r.url === '/api/subrubros').error(new ProgressEvent('error'), { status: 0 });
    await estabilizar();

    const aviso = elemento.querySelector('[role="alert"]');
    expect(aviso?.textContent).toContain('No se pudo conectar con el servidor');
    expect(aviso?.querySelector('button')?.textContent).toContain('Reintentar');
  });
});
