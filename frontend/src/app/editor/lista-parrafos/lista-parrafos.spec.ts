import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CAMPOS, OPERADORES, parrafo } from '../../pruebas/datos-prueba';
import { ListaParrafos } from './lista-parrafos';

describe('ListaParrafos', () => {
  let fixture: ComponentFixture<ListaParrafos>;
  let elemento: HTMLElement;

  const tarjetas = () => Array.from(elemento.querySelectorAll<HTMLElement>('.tarjeta-parrafo'));
  const boton = (tarjeta: HTMLElement, etiqueta: string) =>
    tarjeta.querySelector<HTMLButtonElement>(`[aria-label="${etiqueta}"]`)!;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [ListaParrafos],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    // El catálogo resuelve las etiquetas de los campos.
    const http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ListaParrafos);
    elemento = fixture.nativeElement;
    fixture.componentRef.setInput('titulo', 'Listado de Considerandos');
    fixture.componentRef.setInput('parrafos', [
      parrafo('P1', 'Siempre visible'),
      parrafo('P2', 'Con condición', {
        mostrarSoloSiCumple: true,
        condicion: {
          operadorLogico: 'Y',
          reglas: [{ campoId: 'monto_exoneracion', operador: 'MayorQue', valor: '100000' }],
          grupos: [],
        },
      }),
      parrafo('P3', 'Condición guardada sin aplicar', {
        activo: false,
        contenido: {
          type: 'doc',
          content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Operador ' }, { type: 'campo', attrs: { campoId: 'operador' } }] }],
        },
        condicion: { operadorLogico: 'Y', reglas: [{ campoId: 'operador', operador: 'EsNulo' }], grupos: [] },
      }),
    ]);
    fixture.componentRef.setInput('editandoId', 'P2');
    fixture.detectChanges();
    TestBed.tick();
    http.expectOne('/api/campos').flush(CAMPOS);
    http.expectOne('/api/campos/operadores').flush(OPERADORES);
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  });

  it('muestra el título con el número de párrafos', () => {
    expect(elemento.querySelector('h3')?.textContent?.replace(/\s+/g, ' ').trim()).toBe('Listado de Considerandos 3');
  });

  it('resume las condiciones sin abrir el editor', () => {
    const condiciones = tarjetas().map((t) =>
      Array.from(t.querySelector('.condiciones')!.children)
        .map((parte) => parte.textContent?.replace(/\s+/g, ' ').trim())
        .filter(Boolean)
        .join(' '),
    );
    expect(condiciones[0]).toBe('Condiciones: Siempre visible');
    expect(condiciones[1]).toBe('Condiciones: Monto Exoneración mayor que 100000');
    expect(condiciones[2]).toContain('Sin aplicar · Operador está vacío');
    expect(condiciones[2]).toContain('Inactivo');
  });

  it('muestra los campos con su etiqueta y marca el párrafo en edición', () => {
    expect(tarjetas()[2].querySelector('.resumen')?.textContent).toBe('Operador {Operador}');
    expect(tarjetas()[1].classList).toContain('editando');
    expect(tarjetas()[2].classList).toContain('inactivo');
  });

  it('no permite subir el primero ni bajar el último', () => {
    expect(boton(tarjetas()[0], 'Subir párrafo').disabled).toBe(true);
    expect(boton(tarjetas()[0], 'Bajar párrafo').disabled).toBe(false);
    expect(boton(tarjetas()[2], 'Bajar párrafo').disabled).toBe(true);
  });

  it('emite las acciones del párrafo elegido', () => {
    const eventos: string[] = [];
    fixture.componentInstance.editar.subscribe((p) => eventos.push(`editar ${p.id}`));
    fixture.componentInstance.eliminar.subscribe((p) => eventos.push(`eliminar ${p.id}`));
    fixture.componentInstance.duplicar.subscribe((p) => eventos.push(`duplicar ${p.id}`));
    fixture.componentInstance.alternarActivo.subscribe((p) => eventos.push(`activo ${p.id}`));
    fixture.componentInstance.mover.subscribe((m) => eventos.push(`mover ${m.parrafo.id} ${m.desplazamiento}`));

    boton(tarjetas()[0], 'Editar párrafo').click();
    boton(tarjetas()[1], 'Eliminar párrafo').click();
    boton(tarjetas()[1], 'Duplicar párrafo').click();
    boton(tarjetas()[2], 'Activar párrafo').click();
    boton(tarjetas()[1], 'Subir párrafo').click();

    expect(eventos).toEqual(['editar P1', 'eliminar P2', 'duplicar P2', 'activo P3', 'mover P2 -1']);
  });

  it('emite el nuevo orden al soltar una tarjeta en otra posición', () => {
    const ordenes: { desde: number; hasta: number }[] = [];
    fixture.componentInstance.reordenar.subscribe((o) => ordenes.push(o));
    const soltar = (fixture.componentInstance as unknown as {
      soltar: (e: { previousIndex: number; currentIndex: number }) => void;
    }).soltar.bind(fixture.componentInstance);

    soltar({ previousIndex: 0, currentIndex: 2 });
    soltar({ previousIndex: 1, currentIndex: 1 });

    expect(ordenes).toEqual([{ desde: 0, hasta: 2 }]);
    expect(elemento.querySelector('[cdkDropList], .cdk-drop-list')).not.toBeNull();
    expect(elemento.querySelectorAll('.cdk-drag').length).toBe(3);
  });

  it('deshabilita las acciones mientras se guarda', () => {
    fixture.componentRef.setInput('ocupado', true);
    fixture.detectChanges();
    expect(tarjetas().every((t) => Array.from(t.querySelectorAll('button')).every((b) => b.disabled))).toBe(true);
  });
});
