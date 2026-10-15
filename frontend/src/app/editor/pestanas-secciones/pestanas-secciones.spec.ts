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

  it('habilita Cabecera y Considerando, y deja Resuelve para después', () => {
    expect(pestana('Cabecera').getAttribute('aria-disabled')).toBe('false');
    expect(pestana('Considerando').getAttribute('aria-selected')).toBe('true');
    expect(pestana('Resuelve').getAttribute('aria-disabled')).toBe('true');
    expect(pestana('Resuelve').textContent).toContain('Próximamente');
  });

  it('mantiene montadas las secciones para conservar sus borradores', () => {
    expect(panel('Cabecera')?.hidden).toBe(true);
    expect(panel('Considerando')?.hidden).toBe(false);
    expect(panel('Resuelve')).toBeNull();

    pestana('Cabecera').click();
    fixture.detectChanges();

    expect(panel('Cabecera')?.hidden).toBe(false);
    expect(panel('Considerando')?.hidden).toBe(true);
    expect(elemento.querySelectorAll('app-editor-seccion').length).toBe(2);
  });

  it('marca las pestañas con cambios sin guardar', () => {
    TestBed.inject(ContextoService).marcarCambios('Cabecera', true);
    fixture.detectChanges();

    expect(pestana('Cabecera').querySelector('.cambios')).not.toBeNull();
    expect(pestana('Considerando').querySelector('.cambios')).toBeNull();
  });
});
