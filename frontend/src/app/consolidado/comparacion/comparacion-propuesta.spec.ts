import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ComparacionPropuesta as Comparacion } from '../../core/modelos';
import { ComparacionPropuesta } from './comparacion-propuesta';

const COMPARACION: Comparacion = {
  propuestaId: 'PRO-1',
  operacion: 'Modificar',
  ajustes: [
    { tipo: 'ParrafoModificado', bloqueCodigo: 'CONSIDERANDO', parrafoId: 'A', etiqueta: 'CONSIDERANDO 1 modificado' },
    { tipo: 'ParrafoEliminado', bloqueCodigo: 'CONSIDERANDO', parrafoId: 'B', etiqueta: 'CONSIDERANDO 2 eliminado' },
    { tipo: 'DisenoModificado', bloqueCodigo: 'DISENO', etiqueta: 'Pie de página modificado' },
  ],
  diseno: [{ campo: 'Pie de página', original: 'Pie anterior', propuesto: 'Pie nuevo' }],
  bloques: [
    { codigo: 'CABECERA', nombre: 'Cabecera', estado: 'SinCambios', parrafos: [
      { parrafoId: 'C', estado: 'SinCambios', movido: false, posicionOriginal: 1, posicionPropuesta: 1,
        original: [{ tipo: 'Igual', texto: 'Encabezado.' }], propuesto: [{ tipo: 'Igual', texto: 'Encabezado.' }],
        soloFormato: false, configuracion: [], ajustes: [] },
    ] },
    { codigo: 'CONSIDERANDO', nombre: 'Considerando', estado: 'Modificado', parrafos: [
      { parrafoId: 'A', estado: 'Modificado', movido: false, posicionOriginal: 1, posicionPropuesta: 1,
        original: [{ tipo: 'Igual', texto: 'Que el monto ' }, { tipo: 'Sustituido', texto: 'asciende a' }],
        propuesto: [{ tipo: 'Igual', texto: 'Que el monto ' }, { tipo: 'Sustituido', texto: 'alcanza' }],
        soloFormato: false, configuracion: ['Se desactivó'], ajustes: ['CONSIDERANDO 1 modificado'] },
      { parrafoId: 'B', estado: 'Eliminado', movido: false, posicionOriginal: 2, original: [{ tipo: 'Eliminado', texto: 'Que se elimina.' }],
        propuesto: null, soloFormato: false, configuracion: [], ajustes: ['CONSIDERANDO 2 eliminado'] },
      { parrafoId: 'D', estado: 'SinCambios', movido: false, posicionOriginal: 3, posicionPropuesta: 2,
        original: [{ tipo: 'Igual', texto: 'Contexto sin cambios.' }], propuesto: [{ tipo: 'Igual', texto: 'Contexto sin cambios.' }],
        soloFormato: false, configuracion: [], ajustes: [] },
    ] },
  ],
};

describe('ComparacionPropuesta', () => {
  it('muestra original y propuesta lado a lado con las diferencias resaltadas', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ComparacionPropuesta);
    fixture.componentRef.setInput('propuestaId', 'PRO-1');
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/propuestas/PRO-1/comparacion').flush(COMPARACION);
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
    const elemento: HTMLElement = fixture.nativeElement;

    expect(elemento.textContent).toContain('3 ajustes en 1 bloque');
    const bloques = elemento.querySelectorAll('details');
    // Los bloques sin cambios aparecen plegados.
    expect(bloques[0].open).toBe(false);
    expect(bloques[1].open).toBe(true);
    expect(bloques[1].querySelector('del.sustituido-original')?.textContent).toBe('asciende a');
    expect(bloques[1].querySelector('ins.sustituido-propuesto')?.textContent).toBe('alcanza');
    expect(bloques[1].textContent).toContain('PÁRRAFO ELIMINADO');
    expect(bloques[1].textContent).toContain('Se desactivó');
    expect(elemento.querySelector('table')?.textContent).toContain('Pie nuevo');

    // Se puede ver una sola columna.
    Array.from(elemento.querySelectorAll<HTMLButtonElement>('.vistas button')).find((b) => b.textContent?.includes('Documento'))!.click();
    fixture.detectChanges();
    expect(bloques[1].querySelector('.columnas')?.classList).toContain('solo-propuesta');
    http.verify();
  });

  it('«Solo cambios» oculta los párrafos y bloques sin cambios', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ComparacionPropuesta);
    fixture.componentRef.setInput('propuestaId', 'PRO-1');
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/propuestas/PRO-1/comparacion').flush(COMPARACION);
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
    const elemento: HTMLElement = fixture.nativeElement;
    expect(elemento.textContent).toContain('Contexto sin cambios.');

    const boton = Array.from(elemento.querySelectorAll<HTMLButtonElement>('.vistas button')).find((b) => b.textContent?.includes('Solo cambios'))!;
    boton.click();
    fixture.detectChanges();

    expect(boton.getAttribute('aria-pressed')).toBe('true');
    expect(elemento.textContent).not.toContain('Contexto sin cambios.');
    expect(elemento.querySelectorAll('details')).toHaveLength(1);
    expect(elemento.querySelector('.ocultos')?.textContent).toBe('1 párrafo sin cambios oculto');
    expect(elemento.textContent).toContain('alcanza');

    boton.click();
    fixture.detectChanges();
    expect(elemento.textContent).toContain('Contexto sin cambios.');
  });
});
