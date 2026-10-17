import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { InteraccionService } from '../../core/interaccion.service';
import { PlantillaEdicion } from '../../core/modelos';
import { PLANTILLA } from '../../pruebas/datos-prueba';
import { DisenoDocumento } from './diseno-documento';

describe('DisenoDocumento', () => {
  let fixture: ComponentFixture<DisenoDocumento>;
  let elemento: HTMLElement;
  let http: HttpTestingController;

  const conLogo: PlantillaEdicion = { ...PLANTILLA, version: 2, logoId: 'ARC-1', piePagina: 'Pie anterior', alineacionLogo: 'Centro' };

  const elegirArchivo = (archivo: File) => {
    const entrada = elemento.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(entrada, 'files', { value: [archivo], configurable: true });
    entrada.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };
  const boton = (texto: string) =>
    Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes(texto));

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DisenoDocumento],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DisenoDocumento);
    elemento = fixture.nativeElement;
    fixture.componentRef.setInput('plantilla', conLogo);
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('muestra el logo actual y el pie de página guardado', () => {
    expect(elemento.querySelector('img')?.getAttribute('src')).toBe('/api/archivos/ARC-1');
    expect(elemento.querySelector('textarea')?.value).toBe('Pie anterior');
    expect(elemento.textContent).toContain('Reemplazar logo');
  });

  it('carga el logo como formulario y valida tipo y tamaño antes de enviarlo', () => {
    elegirArchivo(new File(['x'], 'logo.gif', { type: 'image/gif' }));
    expect(elemento.querySelector('[role="alert"]')?.textContent).toContain('PNG o JPEG');

    elegirArchivo(new File([new Uint8Array(1024 * 1024 + 1)], 'grande.png', { type: 'image/png' }));
    expect(elemento.querySelector('[role="alert"]')?.textContent).toContain('1 MB');
    http.expectNone((r) => r.url.endsWith('/logo'));

    elegirArchivo(new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'logo.png', { type: 'image/png' }));
    const carga = http.expectOne('/api/propuestas/PRO-001/logo');
    expect(carga.request.method).toBe('POST');
    expect(carga.request.body).toBeInstanceOf(FormData);
    expect((carga.request.body as FormData).get('archivo')).toBeInstanceOf(File);
    carga.flush({ ...conLogo, version: 3, logoId: 'ARC-2' });
  });

  it('guarda el pie de página y la alineación sobre la versión actual', () => {
    const texto = elemento.querySelector<HTMLTextAreaElement>('textarea')!;
    texto.value = 'Dirección de prueba\nTeléfono 0000-0000';
    texto.dispatchEvent(new Event('input'));
    elemento.querySelector<HTMLInputElement>('input[type="radio"][value="Derecha"]')!.click();
    const espacio = elemento.querySelector<HTMLInputElement>('input[type="number"]')!;
    expect(espacio.value).toBe('1.2');
    espacio.value = '2.5';
    espacio.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(elemento.textContent).toContain('Cambios sin guardar');

    boton('Guardar diseño')!.click();
    const guardado = http.expectOne('/api/propuestas/PRO-001/diseno');
    expect(guardado.request.method).toBe('PUT');
    expect(guardado.request.body).toEqual({
      piePagina: 'Dirección de prueba\nTeléfono 0000-0000',
      alineacionLogo: 'Derecha',
      espacioFirmasCm: 2.5,
    });
    guardado.flush({ ...conLogo, version: 3, piePagina: 'Dirección de prueba\nTeléfono 0000-0000', alineacionLogo: 'Derecha' });
    fixture.detectChanges();
    expect(elemento.textContent).not.toContain('Cambios sin guardar');
  });

  it('no cierra el panel al cargar el logo', () => {
    fixture.componentRef.setInput('plantilla', { ...PLANTILLA, id: 'PLA-NUEVA', logoId: null, piePagina: null });
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
    const panel = elemento.querySelector<HTMLDetailsElement>('details')!;
    expect(panel.open).toBe(true);

    // La misma plantilla en una versión nueva, ya con logo: el panel sigue abierto.
    fixture.componentRef.setInput('plantilla', { ...PLANTILLA, id: 'PLA-NUEVA', version: 2, logoId: 'ARC-9' });
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
    expect(panel.open).toBe(true);
  });

  it('no permite guardar un espacio entre firmas fuera de rango', () => {
    const espacio = elemento.querySelector<HTMLInputElement>('input[type="number"]')!;
    espacio.value = '12';
    espacio.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(elemento.querySelector('[role="alert"]')?.textContent).toContain('entre 0 y 10 cm');
    expect(boton('Guardar diseño')!.disabled).toBe(true);
  });

  it('pide confirmación antes de quitar el logo', async () => {
    const confirmar = vi.spyOn(TestBed.inject(InteraccionService), 'confirmar').mockResolvedValue(true);

    boton('Quitar logo')!.click();
    await Promise.resolve();
    await Promise.resolve();

    expect(confirmar).toHaveBeenCalledOnce();
    const quitar = http.expectOne('/api/propuestas/PRO-001/logo');
    expect(quitar.request.method).toBe('DELETE');
    quitar.flush({ ...conLogo, version: 3, logoId: null });
  });
});
