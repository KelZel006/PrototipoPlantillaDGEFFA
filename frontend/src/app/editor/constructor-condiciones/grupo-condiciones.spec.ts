import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GrupoCondiciones } from '../../core/modelos';
import { CAMPOS, OPERADORES } from '../../pruebas/datos-prueba';
import { GrupoCondicionesComponente } from './grupo-condiciones';

describe('GrupoCondiciones', () => {
  let fixture: ComponentFixture<GrupoCondicionesComponente>;
  let elemento: HTMLElement;
  let http: HttpTestingController;
  let emitido: GrupoCondiciones | null;

  const camposCondicion = CAMPOS.filter((c) => c.origen.coleccion === 'solicitudes');

  const refrescar = async () => {
    TestBed.tick();
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  };

  /** Simula el componente padre: aplica el grupo emitido como nuevo valor. */
  const aplicarEmitido = async () => {
    fixture.componentRef.setInput('grupo', emitido);
    await refrescar();
  };

  const listas = () => Array.from(elemento.querySelectorAll<HTMLSelectElement>('.regla select'));
  const opciones = (lista: HTMLSelectElement) => Array.from(lista.options).map((o) => o.text);

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [GrupoCondicionesComponente],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(GrupoCondicionesComponente);
    elemento = fixture.nativeElement;
    emitido = null;
    fixture.componentInstance.cambio.subscribe((grupo) => (emitido = grupo));
    fixture.componentRef.setInput('campos', camposCondicion);
    fixture.componentRef.setInput('grupo', {
      operadorLogico: 'Y',
      reglas: [{ campoId: 'operador', operador: 'IgualA', valor: 'Operador uno' }],
      grupos: [],
    });
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/campos').flush(CAMPOS);
    http.expectOne('/api/campos/operadores').flush(OPERADORES);
    await refrescar();
    http.match((r) => r.url.endsWith('/valores')).forEach((p) => p.flush(['Operador uno', 'Operador dos']));
    await refrescar();
  });

  it('ofrece los operadores del tipo de dato del campo', async () => {
    expect(opciones(listas()[1])).toEqual(['Igual a', 'Distinto de', 'Contiene', 'No contiene', 'Está vacío', 'No está vacío']);

    listas()[0].value = 'monto_exoneracion';
    listas()[0].dispatchEvent(new Event('change'));
    expect(emitido!.reglas[0]).toEqual({ campoId: 'monto_exoneracion', operador: 'IgualA', valor: '' });
    await aplicarEmitido();

    expect(opciones(listas()[1])).toContain('Mayor que');
    expect(opciones(listas()[1])).not.toContain('Contiene');
    expect(elemento.querySelector<HTMLInputElement>('.regla input')!.type).toBe('number');
  });

  it('sugiere los valores existentes en los datos de prueba', () => {
    const sugerencias = Array.from(elemento.querySelectorAll('datalist option')).map((o) => (o as HTMLOptionElement).value);
    expect(sugerencias).toEqual(['Operador uno', 'Operador dos']);
  });

  it('no pide valor para "Está vacío"', async () => {
    listas()[1].value = 'EsNulo';
    listas()[1].dispatchEvent(new Event('change'));
    expect(emitido!.reglas[0]).toEqual({ campoId: 'operador', operador: 'EsNulo', valor: null });
    await aplicarEmitido();
    expect(elemento.querySelector<HTMLInputElement>('.regla input')!.disabled).toBe(true);
  });

  it('muestra el conector Y/O con dos o más condiciones y permite agrupar', async () => {
    expect(elemento.querySelector('.conector')).toBeNull();

    elemento.querySelector<HTMLButtonElement>('.acciones-grupo .boton-secundario')!.click();
    await aplicarEmitido();
    expect(elemento.querySelectorAll('.regla').length).toBe(2);

    elemento.querySelector<HTMLButtonElement>('.conector [aria-checked="false"]')!.click();
    expect(emitido!.operadorLogico).toBe('O');
    await aplicarEmitido();

    const agregarGrupo = Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) =>
      b.textContent?.includes('Agregar grupo'),
    )!;
    agregarGrupo.click();
    expect(emitido!.grupos).toHaveLength(1);
    expect(emitido!.grupos[0].reglas).toHaveLength(1);
    await aplicarEmitido();
    expect(elemento.querySelector('.subgrupo')).not.toBeNull();
  });

  it('quita una condición', () => {
    elemento.querySelector<HTMLButtonElement>('.quitar-regla')!.click();
    expect(emitido!.reglas).toEqual([]);
  });
});
