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

  it('marca los campos usados en el párrafo y emite el campo a insertar', () => {
    const insertados: string[] = [];
    fixture.componentInstance.insertar.subscribe((campo) => insertados.push(campo.id));

    const usados = Array.from(elemento.querySelectorAll('.campo-usado .etiqueta')).map((e) => e.textContent);
    expect(usados).toEqual(['Número de Solicitud']);

    elemento.querySelector<HTMLButtonElement>('.campo')!.click();
    expect(insertados).toEqual(['operador']);
  });
});
