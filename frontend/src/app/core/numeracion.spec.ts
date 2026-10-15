import { numerarConsiderando, numerarResuelve, ordinal } from './numeracion';

describe('numeración del documento', () => {
  it('escribe los ordinales en palabras, igual que el servidor', () => {
    const casos: [number, string][] = [
      [1, 'PRIMERO'],
      [3, 'TERCERO'],
      [7, 'SÉPTIMO'],
      [10, 'DÉCIMO'],
      [11, 'DÉCIMO PRIMERO'],
      [19, 'DÉCIMO NOVENO'],
      [20, 'VIGÉSIMO'],
      [23, 'VIGÉSIMO TERCERO'],
      [99, 'NONAGÉSIMO NOVENO'],
      [100, '100°'],
    ];
    for (const [numero, esperado] of casos) {
      expect(ordinal(numero)).toBe(esperado);
    }
  });

  it('da la etiqueta de cada sección', () => {
    expect(numerarConsiderando(2)).toBe('CONSIDERANDO(2):');
    expect(numerarResuelve(2)).toBe('SEGUNDO:');
  });
});
