import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ContextoService } from '../../core/contexto.service';
import { Plantilla, Seccion } from '../../core/modelos';
import { PLANTILLA } from '../../pruebas/datos-prueba';
import { EditorSeccion, NombreSeccion } from '../editor-seccion/editor-seccion';
import { PestanasSecciones } from './pestanas-secciones';

/** Sustituto del editor de sección: basta con saber qué secciones se montan. */
@Component({ selector: 'app-editor-seccion', template: '{{ seccion() }}' })
class EditorSeccionFalso {
  readonly plantilla = input<Plantilla>();
  readonly seccion = input<Seccion>();
  readonly nombre = input<NombreSeccion>();
}

describe('PestanasSecciones', () => {
  let fixture: ComponentFixture<PestanasSecciones>;
  let elemento: HTMLElement;

  const pestana = (seccion: string) => elemento.querySelector<HTMLButtonElement>(`#pestana-${seccion}`)!;
  const panel = (seccion: string) => elemento.querySelector<HTMLElement>(`#panel-${seccion}`);

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PestanasSecciones] }).overrideComponent(PestanasSecciones, {
      remove: { imports: [EditorSeccion] },
      add: { imports: [EditorSeccionFalso] },
    });
    fixture = TestBed.createComponent(PestanasSecciones);
    elemento = fixture.nativeElement;
    fixture.componentRef.setInput('plantilla', PLANTILLA);
    fixture.detectChanges();
  });

  it('habilita las cinco secciones en el orden del documento', () => {
    expect(Array.from(elemento.querySelectorAll('[role="tab"]')).map((t) => t.textContent?.trim())).toEqual([
      'Cabecera',
      'Considerando',
      'Por Tanto',
      'Resuelve',
      'Firmas',
    ]);
    for (const seccion of ['Cabecera', 'Considerando', 'PorTanto', 'Resuelve', 'Firmas']) {
      expect(pestana(seccion).getAttribute('aria-disabled')).toBe('false');
    }
    // Siempre se abre en la primera pestaña.
    expect(pestana('Cabecera').getAttribute('aria-selected')).toBe('true');
    expect(elemento.textContent).not.toContain('Próximamente');
  });

  it('mantiene montadas las secciones para conservar sus borradores', () => {
    expect(panel('Cabecera')?.hidden).toBe(false);
    expect(panel('Considerando')?.hidden).toBe(true);
    expect(panel('Resuelve')?.hidden).toBe(true);

    pestana('Resuelve').click();
    fixture.detectChanges();

    expect(panel('Resuelve')?.hidden).toBe(false);
    expect(panel('Considerando')?.hidden).toBe(true);
    expect(elemento.querySelectorAll('app-editor-seccion').length).toBe(5);
    expect(panel('Firmas')?.hidden).toBe(true);
  });

  it('vuelve a la Cabecera al abrir otra plantilla', () => {
    pestana('Resuelve').click();
    fixture.detectChanges();
    expect(pestana('Resuelve').getAttribute('aria-selected')).toBe('true');

    // Una versión nueva de la misma plantilla no cambia la pestaña.
    fixture.componentRef.setInput('plantilla', { ...PLANTILLA, version: 2 });
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
    expect(pestana('Resuelve').getAttribute('aria-selected')).toBe('true');

    fixture.componentRef.setInput('plantilla', { ...PLANTILLA, id: 'PLA-OTRA' });
    fixture.detectChanges();
    TestBed.tick();
    fixture.detectChanges();
    expect(pestana('Cabecera').getAttribute('aria-selected')).toBe('true');
  });

  it('marca las pestañas con cambios sin guardar', () => {
    TestBed.inject(ContextoService).marcarCambios('Cabecera', true);
    fixture.detectChanges();

    expect(pestana('Cabecera').querySelector('.cambios')).not.toBeNull();
    expect(pestana('Considerando').querySelector('.cambios')).toBeNull();
  });
});
