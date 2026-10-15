import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CAMPOS } from '../../pruebas/datos-prueba';
import { PanelCampos } from './panel-campos';

describe('PanelCampos', () => {
  let fixture: ComponentFixture<PanelCampos>;
  let elemento: HTMLElement;

  const etiquetas = () => Array.from(elemento.querySelectorAll('.campo .etiqueta')).map((e) => e.textContent?.trim());
  const buscar = (texto: string) => {
    const entrada = elemento.querySelector<HTMLInputElement>('input[type="search"]')!;
    entrada.value = texto;
    entrada.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(() => {
    fixture = TestBed.createComponent(PanelCampos);
    elemento = fixture.nativeElement;
    fixture.componentRef.setInput('campos', CAMPOS.filter((c) => c.contextos.includes('considerando')));
    fixture.componentRef.setInput('usados', ['numero_solicitud']);
    fixture.detectChanges();
  });

  it('agrupa por categoría y muestra etiqueta, identificador y tipo', () => {
    expect(Array.from(elemento.querySelectorAll('.categoria')).map((c) => c.textContent)).toEqual(['Solicitud', 'Resolución']);
    expect(elemento.querySelector('.campo .tecnico')?.textContent).toBe('operador · Texto');
  });

  it('busca sin distinguir tildes ni mayúsculas, también por identificador', () => {
    buscar('NUMERO');
    expect(etiquetas()).toEqual(['Número de Solicitud']);
    buscar('monto_exo');
    expect(etiquetas()).toEqual(['Monto Exoneración']);
    buscar('inexistente');
    expect(elemento.textContent).toContain('No hay campos que coincidan');
  });

  it('solo habilita los campos de lista con el cursor dentro de una tabla', () => {
    fixture.componentRef.setInput('campos', CAMPOS);
    fixture.detectChanges();
    const botonLista = () =>
      Array.from(elemento.querySelectorAll<HTMLButtonElement>('.campo')).find((b) => b.textContent?.includes('Código Arancelario'))!;

    expect(botonLista().disabled).toBe(true);
    expect(botonLista().querySelector('.tecnico')?.textContent).toBe('codigo_arancelario · Lista · Texto');
    expect(elemento.textContent).toContain('Se insertan dentro de una tabla');

    fixture.componentRef.setInput('enTabla', true);
    fixture.detectChanges();
    expect(botonLista().disabled).toBe(false);
  });

  it('tiene un botón para ocultar el panel', () => {
    let ocultado = false;
    fixture.componentInstance.ocultar.subscribe(() => (ocultado = true));
    elemento.querySelector<HTMLButtonElement>('[aria-label="Ocultar el panel de campos"]')!.click();
    expect(ocultado).toBe(true);
  });

  it('marca los campos usados en el párrafo y emite el campo a insertar', () => {
    const insertados: string[] = [];
    fixture.componentInstance.insertar.subscribe((campo) => insertados.push(campo.id));

    const usados = Array.from(elemento.querySelectorAll('.campo-usado .etiqueta')).map((e) => e.textContent);
    expect(usados).toEqual(['Número de Solicitud']);

    elemento.querySelector<HTMLButtonElement>('.campo')!.click();
    expect(insertados).toEqual(['operador']);
  });
});
