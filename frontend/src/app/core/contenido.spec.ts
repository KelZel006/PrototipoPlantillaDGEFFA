import { camposUsados, contarCaracteres, limpiarDocumento, textoPlano, tieneContenido } from './contenido';
import { NodoContenido } from './modelos';

const etiquetas: Record<string, string> = { operador: 'Operador', numero_solicitud: 'Número de Solicitud' };
const etiqueta = (id: string) => etiquetas[id] ?? id;

const documento: NodoContenido = {
  type: 'doc',
  content: [
    {
      type: 'paragraph',
      attrs: { textAlign: null },
      content: [
        { type: 'text', text: 'Para ' },
        { type: 'campo', attrs: { campoId: 'operador' }, marks: [{ type: 'bold' }] },
        { type: 'text', text: ' y ' },
        { type: 'campo', attrs: { campoId: 'operador' } },
      ],
    },
    {
      type: 'bulletList',
      content: [
        {
          type: 'listItem',
          content: [{ type: 'paragraph', content: [{ type: 'campo', attrs: { campoId: 'numero_solicitud' } }] }],
        },
      ],
    },
  ],
};

describe('contenido del editor', () => {
  it('obtiene los campos usados sin repetir y en orden de aparición', () => {
    expect(camposUsados(documento)).toEqual(['operador', 'numero_solicitud']);
  });

  it('cuenta los caracteres igual que el servidor: texto más "{Etiqueta}" por campo', () => {
    // "Para " (5) + "{Operador}" (10) + " y " (3) + "{Operador}" (10) + "{Número de Solicitud}" (21) = 49
    expect(contarCaracteres(documento, etiqueta)).toBe(49);
  });

  it('distingue un párrafo vacío o con solo espacios', () => {
    expect(tieneContenido({ type: 'doc', content: [{ type: 'paragraph' }] })).toBe(false);
    expect(tieneContenido({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: '   ' }] }] })).toBe(
      false,
    );
    expect(tieneContenido({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'campo', attrs: { campoId: 'x' } }] }] })).toBe(true);
  });

  it('genera el texto plano del resumen con las etiquetas de los campos', () => {
    expect(textoPlano(documento, etiqueta)).toBe('Para {Operador} y {Operador} {Número de Solicitud}');
  });

  it('resume las tablas sin mostrar sus celdas', () => {
    const conTabla: NodoContenido = {
      type: 'doc',
      content: [
        { type: 'paragraph', content: [{ type: 'text', text: 'Bienes:' }] },
        { type: 'table', content: [{ type: 'tableRow', content: [{ type: 'tableCell', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'celda' }] }] }] }] },
      ],
    };
    expect(textoPlano(conTabla, etiqueta)).toBe('Bienes: [Tabla]');
  });

  it('quita los atributos nulos que agrega el editor', () => {
    const limpio = limpiarDocumento(documento);
    expect(limpio.content![0].attrs).toBeUndefined();
    expect(limpio.content![0].content![1]).toEqual({ type: 'campo', attrs: { campoId: 'operador' }, marks: [{ type: 'bold' }] });
    expect(limpiarDocumento({ type: 'doc', content: [{ type: 'paragraph', content: [] }] })).toEqual({
      type: 'doc',
      content: [{ type: 'paragraph' }],
    });
  });
});
