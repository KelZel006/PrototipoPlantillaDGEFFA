import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, input, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ContextoService } from '../../core/contexto.service';
import { InteraccionService } from '../../core/interaccion.service';
import { Campo, NodoContenido, Parrafo } from '../../core/modelos';
import { CAMPOS, OPERADORES, PLANTILLA, parrafo } from '../../pruebas/datos-prueba';
import { EditorTexto } from '../editor-texto/editor-texto';
import { ListaParrafos } from '../lista-parrafos/lista-parrafos';
import { EditorSeccion } from './editor-seccion';

/** Sustituto del editor TipTap: permite simular lo que escribe el usuario. */
@Component({ selector: 'app-editor-texto', template: '' })
class EditorTextoFalso {
  readonly campos = input<Campo[]>([]);
  readonly etiqueta = input<(id: string) => string>();
  readonly etiquetaAccesible = input('');
  readonly cambio = output<NodoContenido>();
}

const DOCUMENTO: NodoContenido = {
  type: 'doc',
  content: [
    {
      type: 'paragraph',
      attrs: { textAlign: null },
      content: [{ type: 'text', text: 'Considerando de prueba ' }, { type: 'campo', attrs: { campoId: 'operador' } }],
    },
  ],
};

describe('EditorSeccion', () => {
  let fixture: ComponentFixture<EditorSeccion>;
  let elemento: HTMLElement;
  let http: HttpTestingController;
  let interaccion: InteraccionService;
  const lista: Parrafo[] = [parrafo('P1', 'Primero'), parrafo('P2', 'Segundo', { orden: 2 })];

  const refrescar = async () => {
    TestBed.tick();
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  };
  const escribir = async (doc: NodoContenido) => {
    fixture.debugElement.query(By.directive(EditorTextoFalso)).componentInstance.cambio.emit(doc);
    await refrescar();
  };
  const botonAccion = (texto: string) =>
    Array.from(elemento.querySelectorAll<HTMLButtonElement>('.acciones button')).find((b) => b.textContent?.includes(texto))!;
  const accionLista = (indice: number, etiqueta: string) =>
    elemento.querySelectorAll('.tarjeta-parrafo')[indice].querySelector<HTMLButtonElement>(`[aria-label="${etiqueta}"]`)!;
  const parrafosUrl = (r: { url: string; method: string }) => r.method === 'GET' && r.url === '/api/parrafos';

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [EditorSeccion],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).overrideComponent(EditorSeccion, { remove: { imports: [EditorTexto] }, add: { imports: [EditorTextoFalso] } });

    http = TestBed.inject(HttpTestingController);
    interaccion = TestBed.inject(InteraccionService);
    fixture = TestBed.createComponent(EditorSeccion);
    elemento = fixture.nativeElement;
    fixture.componentRef.setInput('plantilla', PLANTILLA);
    fixture.componentRef.setInput('seccion', 'Considerando');
    fixture.componentRef.setInput('nombre', {
      texto: 'Texto del Considerando',
      listado: 'Listado de Considerandos',
      elemento: 'Considerando',
    });
    fixture.detectChanges();
    TestBed.tick();

    http.expectOne('/api/campos').flush(CAMPOS);
    http.expectOne('/api/campos/operadores').flush(OPERADORES);
    const peticion = http.expectOne(parrafosUrl);
    expect(peticion.request.params.get('seccion')).toBe('Considerando');
    peticion.flush(lista);
    await refrescar();
  });

  afterEach(() => http.verify());

  it('muestra las tres áreas del diseño', () => {
    expect(elemento.querySelector('app-constructor-condiciones')).not.toBeNull();
    expect(elemento.textContent).toContain('Texto del Considerando');
    expect(elemento.textContent).toContain('Listado de Considerandos');
    expect(elemento.querySelector('app-panel-campos')).not.toBeNull();
    expect(elemento.querySelectorAll('.tarjeta-parrafo').length).toBe(2);
  });

  it('no permite agregar un párrafo vacío', () => {
    expect(botonAccion('Agregar a la lista').disabled).toBe(true);
  });

  it('agrega un párrafo con contenido limpio, al final y sin condición', async () => {
    await escribir(DOCUMENTO);
    expect(TestBed.inject(ContextoService).cambiosSinGuardar()).toBe(true);
    expect(elemento.textContent).toContain('Cambios sin guardar');

    botonAccion('Agregar a la lista').click();
    const creacion = http.expectOne((r) => r.method === 'POST' && r.url === '/api/parrafos');
    expect(creacion.request.body).toEqual({
      plantillaId: 'PLA-001',
      seccion: 'Considerando',
      orden: 0,
      activo: true,
      camposUsados: [],
      contenido: {
        type: 'doc',
        content: [
          {
            type: 'paragraph',
            content: [{ type: 'text', text: 'Considerando de prueba ' }, { type: 'campo', attrs: { campoId: 'operador' } }],
          },
        ],
      },
      condicion: null,
      mostrarSoloSiCumple: true,
    });
    creacion.flush(parrafo('P3', 'Considerando de prueba', { orden: 3 }));
    await refrescar();

    http.expectOne(parrafosUrl).flush([...lista, parrafo('P3', 'Considerando de prueba', { orden: 3 })]);
    await refrescar();

    expect(elemento.querySelectorAll('.tarjeta-parrafo').length).toBe(3);
    expect(interaccion.avisos().map((a) => a.texto)).toContain('Considerando agregado.');
    expect(TestBed.inject(ContextoService).cambiosSinGuardar()).toBe(false);
  });

  it('ante un conflicto de versión conserva el borrador y permite recargar', async () => {
    accionLista(0, 'Editar párrafo').click();
    await refrescar();
    expect(elemento.textContent).toContain('Editando párrafo 1');

    await escribir(DOCUMENTO);
    botonAccion('Actualizar').click();
    const primera = http.expectOne((r) => r.method === 'PUT');
    expect(primera.request.url).toBe('/api/parrafos/P1');
    expect(primera.request.body.version).toBe(1);
    primera.flush({ title: 'Conflicto con los datos guardados.' }, { status: 409, statusText: 'Conflict' });
    await refrescar();

    expect(elemento.querySelector('[role="alert"]')?.textContent).toContain('Otra persona modificó');
    Array.from(elemento.querySelectorAll<HTMLButtonElement>('button'))
      .find((b) => b.textContent?.includes('Recargar listado'))!
      .click();
    http.expectOne(parrafosUrl).flush([{ ...lista[0], version: 2 }, lista[1]]);
    await refrescar();

    // El texto sigue en el editor y ahora se guarda sobre la versión 2.
    botonAccion('Actualizar').click();
    const segunda = http.expectOne((r) => r.method === 'PUT');
    expect(segunda.request.body.version).toBe(2);
    expect(segunda.request.body.contenido.content[0].content[0].text).toBe('Considerando de prueba ');
    segunda.flush({ ...lista[0], version: 3 });
    await refrescar();
    http.expectOne(parrafosUrl).flush(lista);
  });

  it('pide confirmación antes de eliminar y envía la versión', async () => {
    const confirmar = vi.spyOn(interaccion, 'confirmar').mockResolvedValue(false);
    accionLista(1, 'Eliminar párrafo').click();
    await refrescar();
    expect(confirmar).toHaveBeenCalledOnce();
    expect(confirmar.mock.calls[0][0].mensaje).toContain('Segundo');
    http.expectNone((r) => r.method === 'DELETE');

    confirmar.mockResolvedValue(true);
    accionLista(1, 'Eliminar párrafo').click();
    await refrescar();
    const eliminacion = http.expectOne((r) => r.method === 'DELETE');
    expect(eliminacion.request.url).toBe('/api/parrafos/P2');
    expect(eliminacion.request.params.get('version')).toBe('1');
    eliminacion.flush(null);
    await refrescar();
    http.expectOne(parrafosUrl).flush([lista[0]]);
    await refrescar();
    expect(elemento.querySelectorAll('.tarjeta-parrafo').length).toBe(1);
  });

  it('reordena enviando la sección completa con sus versiones', async () => {
    accionLista(0, 'Bajar párrafo').click();
    const reorden = http.expectOne('/api/parrafos/reordenar');
    expect(reorden.request.body).toEqual({
      plantillaId: 'PLA-001',
      seccion: 'Considerando',
      elementos: [
        { id: 'P2', version: 1 },
        { id: 'P1', version: 1 },
      ],
    });
    reorden.flush([{ ...lista[1], orden: 1, version: 2 }, { ...lista[0], orden: 2, version: 2 }]);
    await refrescar();
    expect(Array.from(elemento.querySelectorAll('.resumen')).map((r) => r.textContent)).toEqual(['Segundo', 'Primero']);
  });

  it('guarda el orden al arrastrar una tarjeta', async () => {
    fixture.debugElement.query(By.directive(ListaParrafos)).componentInstance.reordenar.emit({ desde: 1, hasta: 0 });
    // El nuevo orden se muestra de inmediato.
    fixture.detectChanges();
    expect(Array.from(elemento.querySelectorAll('.resumen')).map((r) => r.textContent)).toEqual(['Segundo', 'Primero']);

    const reorden = http.expectOne('/api/parrafos/reordenar');
    expect(reorden.request.body.elementos.map((e: { id: string }) => e.id)).toEqual(['P2', 'P1']);
    reorden.flush({ title: 'Conflicto con los datos guardados.' }, { status: 409, statusText: 'Conflict' });
    await refrescar();

    // Si el servidor lo rechaza, se avisa y se recarga la lista.
    expect(interaccion.avisos().some((a) => a.tipo === 'error')).toBe(true);
    http.expectOne(parrafosUrl).flush(lista);
    await refrescar();
    expect(Array.from(elemento.querySelectorAll('.resumen')).map((r) => r.textContent)).toEqual(['Primero', 'Segundo']);
  });

  it('pide confirmación antes de descartar cambios al editar otro párrafo', async () => {
    const confirmar = vi.spyOn(interaccion, 'confirmar').mockResolvedValue(false);
    await escribir(DOCUMENTO);

    accionLista(1, 'Editar párrafo').click();
    await refrescar();

    expect(confirmar).toHaveBeenCalledOnce();
    expect(elemento.textContent).toContain('Nuevo párrafo');
  });
});
