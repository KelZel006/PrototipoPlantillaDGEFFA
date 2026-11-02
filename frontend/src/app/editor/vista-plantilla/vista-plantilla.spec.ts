import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CAMPOS, OPERADORES, PLANTILLA, parrafo } from '../../pruebas/datos-prueba';
import { VistaPlantilla } from './vista-plantilla';

describe('VistaPlantilla', () => {
  it('muestra todos los bloques con sus párrafos, su numeración y sus marcas, sin poder editar', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(VistaPlantilla);
    fixture.componentRef.setInput('plantilla', { ...PLANTILLA, editable: false });
    fixture.detectChanges();
    TestBed.tick();

    http.expectOne('/api/campos').flush(CAMPOS);
    http.expectOne('/api/campos/operadores').flush(OPERADORES);
    http.expectOne((r) => r.url === '/api/propuestas/PRO-001/parrafos').flush([
      parrafo('P1', 'Encabezado de prueba', { bloque: 'CABECERA', orden: 1 }),
      parrafo('P2', 'CONSIDERACIONES', { bloque: 'CONSIDERANDO', orden: 1, esTitulo: true }),
      parrafo('P3', 'Primer considerando', {
        bloque: 'CONSIDERANDO',
        orden: 2,
        mostrarSoloSiCumple: true,
        condicion: { operadorLogico: 'Y', reglas: [{ campoId: 'monto_exoneracion', operador: 'MayorQue', valor: '100000' }], grupos: [] },
      }),
      parrafo('P4', 'Considerando inactivo', { bloque: 'CONSIDERANDO', orden: 3, activo: false }),
    ]);
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
    await new Promise((resolver) => setTimeout(resolver));
    const elemento: HTMLElement = fixture.nativeElement;

    expect(Array.from(elemento.querySelectorAll('.bloque h4')).map((h) => h.childNodes[0].textContent?.trim())).toEqual(
      ['Cabecera', 'Considerando', 'Por Tanto', 'Resuelve', 'Firmas'],
    );
    const considerandos = Array.from(elemento.querySelectorAll('.bloque')[1].querySelectorAll('.parrafo'));
    expect(considerandos[0].textContent).toContain('Título · centrado');
    expect(considerandos[1].querySelector('.numeracion')?.textContent).toBe('CONSIDERANDO(1):');
    expect(considerandos[1].textContent).toContain('Solo si: Monto Exoneración mayor que 100000');
    expect(considerandos[1].textContent).toContain('Primer considerando');
    expect(considerandos[2].textContent).toContain('Inactivo: no aparece en el documento');
    // Solo lectura: el texto no se puede editar.
    expect(elemento.querySelector('.superficie-lectura')?.getAttribute('contenteditable')).toBe('false');
    http.verify();
  });
});
