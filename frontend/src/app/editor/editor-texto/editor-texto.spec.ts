import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NodoContenido } from '../../core/modelos';
import { CAMPOS } from '../../pruebas/datos-prueba';
import { EditorTexto, enlacePermitido } from './editor-texto';

describe('EditorTexto', () => {
  let fixture: ComponentFixture<EditorTexto>;
  let elemento: HTMLElement;
  let cambios: NodoContenido[];

  const etiqueta = (id: string) => CAMPOS.find((c) => c.id === id)?.etiqueta ?? id;

  beforeEach(async () => {
    fixture = TestBed.createComponent(EditorTexto);
    elemento = fixture.nativeElement;
    cambios = [];
    fixture.componentInstance.cambio.subscribe((doc) => cambios.push(doc));
    fixture.componentRef.setInput('campos', CAMPOS.filter((c) => c.contextos.includes('considerando')));
    fixture.componentRef.setInput('etiqueta', etiqueta);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => fixture.destroy());

  it('muestra los campos como etiquetas diferenciadas y cuenta los caracteres', async () => {
    fixture.componentInstance.establecer({
      type: 'doc',
      content: [
        {
          type: 'paragraph',
          content: [
            { type: 'text', text: 'Para el operador ' },
            { type: 'campo', attrs: { campoId: 'operador' } },
          ],
        },
      ],
    });
    fixture.detectChanges();

    const campo = elemento.querySelector('.campo-dinamico');
    expect(campo?.textContent).toBe('{Operador}');
    expect(campo?.getAttribute('data-campo-id')).toBe('operador');
    expect(campo?.getAttribute('contenteditable')).toBe('false');
    // "Para el operador " (17) + "{Operador}" (10)
    expect(elemento.querySelector('.contador')?.textContent?.trim()).toBe('27/2000');
    // Cargar contenido no cuenta como cambio del usuario.
    expect(cambios).toEqual([]);
  });

  it('descarta del HTML recibido los elementos fuera del esquema', () => {
    // Equivale a pegar HTML externo: el editor lo interpreta con su esquema y descarta lo no permitido.
    const editor = (fixture.componentInstance as unknown as { editor: import('@tiptap/core').Editor }).editor;
    editor.commands.setContent(
      '<h1>Título</h1><p style="color:red">Hola <img src="x.png"><script>alert(1)</script>' +
        '<a href="javascript:alert(1)">enlace</a> <span data-campo-id="operador"></span></p>',
    );

    const tipos = new Set<string>();
    const visitar = (nodo: NodoContenido) => {
      tipos.add(nodo.type);
      nodo.marks?.forEach((m) => tipos.add(`marca:${m.type}`));
      nodo.content?.forEach(visitar);
    };
    visitar(editor.getJSON() as NodoContenido);

    expect([...tipos].sort()).toEqual(['campo', 'doc', 'paragraph', 'text']);
    expect(elemento.querySelector('.superficie-editor img, .superficie-editor script, .superficie-editor h1')).toBeNull();
    expect(JSON.stringify(editor.getJSON())).not.toContain('javascript');
  });

  it('ofrece los campos agrupados en el menú "Insertar campo"', () => {
    const grupos = Array.from(elemento.querySelectorAll('optgroup')).map((g) => g.getAttribute('label'));
    expect(grupos).toEqual(['Solicitud', 'Resolución']);
    expect(Array.from(elemento.querySelectorAll('optgroup option')).map((o) => o.textContent?.trim())).toEqual([
      'Operador',
      'Número de Solicitud',
      'Monto Exoneración',
    ]);
  });

  it('justifica el texto por defecto', () => {
    fixture.componentInstance.insertarTabla(1, 1, false);
    expect(elemento.querySelector('[aria-label="Justificar"]')?.getAttribute('aria-pressed')).toBe('true');
  });

  it('tiene botones accesibles para el formato del diseño', () => {
    const etiquetas = Array.from(elemento.querySelectorAll('.barra button')).map((b) => b.getAttribute('aria-label'));
    expect(etiquetas).toEqual([
      'Negrita',
      'Cursiva',
      'Subrayado',
      'Lista con viñetas',
      'Lista numerada',
      'Alinear a la izquierda',
      'Centrar',
      'Alinear a la derecha',
      'Justificar',
      'Enlace',
      'Quitar formato',
      'Insertar tabla',
    ]);
  });
});

describe('EditorTexto: tablas', () => {
  let fixture: ComponentFixture<EditorTexto>;
  let elemento: HTMLElement;
  let cambios: NodoContenido[];

  beforeEach(async () => {
    fixture = TestBed.createComponent(EditorTexto);
    elemento = fixture.nativeElement;
    cambios = [];
    fixture.componentInstance.cambio.subscribe((doc) => cambios.push(doc));
    fixture.componentRef.setInput('campos', CAMPOS);
    fixture.componentRef.setInput('etiqueta', (id: string) => id);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => fixture.destroy());

  const tabla = () => cambios.at(-1)!.content!.find((n) => n.type === 'table')!;

  it('inserta una tabla con fila de encabezado y muestra sus acciones', () => {
    fixture.componentInstance.insertarTabla(2, 3, true);
    fixture.detectChanges();

    const filas = tabla().content!;
    expect(filas).toHaveLength(2);
    expect(filas[0].content!.map((c) => c.type)).toEqual(['tableHeader', 'tableHeader', 'tableHeader']);
    expect(filas[1].content!.map((c) => c.type)).toEqual(['tableCell', 'tableCell', 'tableCell']);

    expect(elemento.querySelector('[aria-label="Acciones de la tabla"]')).not.toBeNull();
    expect(elemento.querySelector<HTMLButtonElement>('[aria-label="Insertar tabla"]')!.disabled).toBe(true);
  });

  it('ajusta filas y columnas a los límites del servidor', () => {
    fixture.componentInstance.insertarTabla(0, 40, false);
    const filas = tabla().content!;
    expect(filas).toHaveLength(1);
    expect(filas[0].content).toHaveLength(12);
  });

  it('inserta un campo de lista solo dentro de una tabla y lo distingue', () => {
    expect(fixture.componentInstance.insertarCampo('codigo_arancelario')).toBe(false);
    expect(elemento.querySelector('.campo-lista')).toBeNull();
    const opcionLista = () =>
      Array.from(elemento.querySelectorAll<HTMLOptionElement>('optgroup option')).find((o) => o.value === 'codigo_arancelario')!;
    expect(opcionLista().disabled).toBe(true);

    fixture.componentInstance.insertarTabla(2, 2, true);
    fixture.detectChanges();
    expect(fixture.componentInstance.insertarCampo('codigo_arancelario')).toBe(true);
    fixture.detectChanges();
    expect(elemento.querySelector('.campo-dinamico.campo-lista')?.textContent).toBe('{codigo_arancelario}');
    expect(opcionLista().disabled).toBe(false);
  });

  it('avisa cuando el cursor entra en una tabla', () => {
    const estados: boolean[] = [];
    fixture.componentInstance.enTablaCambio.subscribe((e) => estados.push(e));
    fixture.componentInstance.insertarTabla(2, 2, false);
    expect(estados).toEqual([true]);
  });

  it('no inserta una tabla dentro de otra', () => {
    fixture.componentInstance.insertarTabla(2, 2, false);
    const antes = cambios.length;
    fixture.componentInstance.insertarTabla(2, 2, false);
    expect(cambios.length).toBe(antes);
  });
});

describe('enlacePermitido', () => {
  it('acepta solo http, https y mailto', () => {
    expect(enlacePermitido('https://ejemplo.org')).toBe(true);
    expect(enlacePermitido('http://ejemplo.org/ruta')).toBe(true);
    expect(enlacePermitido('mailto:alguien@ejemplo.org')).toBe(true);
    expect(enlacePermitido('javascript:alert(1)')).toBe(false);
    expect(enlacePermitido('data:text/html,hola')).toBe(false);
    expect(enlacePermitido('ejemplo.org')).toBe(false);
  });
});
