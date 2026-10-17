/**
 * Numeración de los bloques del documento. Son las mismas reglas que aplica el servidor (Documento/Numeracion.cs):
 * "CONSIDERANDO(1):" en Considerando y "PRIMERO:", "SEGUNDO:"… en Resuelve.
 */

const UNIDADES = ['', 'PRIMERO', 'SEGUNDO', 'TERCERO', 'CUARTO', 'QUINTO', 'SEXTO', 'SÉPTIMO', 'OCTAVO', 'NOVENO'];
const DECENAS = [
  '',
  'DÉCIMO',
  'VIGÉSIMO',
  'TRIGÉSIMO',
  'CUADRAGÉSIMO',
  'QUINCUAGÉSIMO',
  'SEXAGÉSIMO',
  'SEPTUAGÉSIMO',
  'OCTOGÉSIMO',
  'NONAGÉSIMO',
];

/** Ordinal en palabras y mayúsculas: 1 → PRIMERO, 11 → DÉCIMO PRIMERO, 20 → VIGÉSIMO. Desde 100, "100°". */
export function ordinal(numero: number): string {
  if (numero < 1 || numero > 99) {
    return `${numero}°`;
  }
  const decena = DECENAS[Math.floor(numero / 10)];
  const unidad = UNIDADES[numero % 10];
  return [decena, unidad].filter(Boolean).join(' ');
}

/** Da la etiqueta del bloque número n de una sección. */
export type Numerador = (numero: number) => string;

export const numerarConsiderando: Numerador = (numero) => `CONSIDERANDO(${numero}):`;

export const numerarResuelve: Numerador = (numero) => `${ordinal(numero)}:`;

/** Numerador según la configuración del bloque; null si el bloque no se numera. */
export function numeradorDe(numeracion: 'Ninguna' | 'Considerando' | 'Ordinal'): Numerador | null {
  return numeracion === 'Considerando' ? numerarConsiderando : numeracion === 'Ordinal' ? numerarResuelve : null;
}
