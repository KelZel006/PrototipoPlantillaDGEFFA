import { Campo, GrupoCondiciones, OperadorComparacion, ReglaCondicion } from './modelos';

export const ETIQUETAS_OPERADOR: Record<OperadorComparacion, string> = {
  IgualA: 'Igual a',
  DistintoDe: 'Distinto de',
  Contiene: 'Contiene',
  NoContiene: 'No contiene',
  EsNulo: 'Está vacío',
  NoEsNulo: 'No está vacío',
  MayorQue: 'Mayor que',
  MenorQue: 'Menor que',
  MayorOIgualQue: 'Mayor o igual que',
  MenorOIgualQue: 'Menor o igual que',
};

/** Profundidad máxima de grupos anidados; coincide con el servidor. */
export const PROFUNDIDAD_MAXIMA = 5;

export function requiereValor(operador: OperadorComparacion): boolean {
  return operador !== 'EsNulo' && operador !== 'NoEsNulo';
}

export function grupoVacio(): GrupoCondiciones {
  return { operadorLogico: 'Y', reglas: [], grupos: [] };
}

export function reglaNueva(): ReglaCondicion {
  return { campoId: '', operador: 'IgualA', valor: '' };
}

export function reglas(grupo: GrupoCondiciones): ReglaCondicion[] {
  return [...grupo.reglas, ...grupo.grupos.flatMap(reglas)];
}

export function tieneReglas(grupo: GrupoCondiciones | null | undefined): boolean {
  return !!grupo && reglas(grupo).length > 0;
}

/** Una regla está completa si tiene campo y, cuando el operador lo pide, un valor. */
export function reglaCompleta(regla: ReglaCondicion): boolean {
  return regla.campoId !== '' && (!requiereValor(regla.operador) || (regla.valor ?? '').trim() !== '');
}

/** Problemas que impiden guardar la condición, explicados para el usuario. */
export function problemasCondicion(grupo: GrupoCondiciones): string[] {
  const problemas: string[] = [];
  if (reglas(grupo).some((r) => !reglaCompleta(r))) {
    problemas.push('Complete o quite las condiciones incompletas.');
  }
  const conGrupoVacio = (g: GrupoCondiciones): boolean =>
    g.grupos.some((sub) => (sub.reglas.length === 0 && sub.grupos.length === 0) || conGrupoVacio(sub));
  if (conGrupoVacio(grupo)) {
    problemas.push('Hay un grupo sin condiciones.');
  }
  return problemas;
}

/** Devuelve null si la condición no tiene reglas (equivale a no tener condición). */
export function normalizarCondicion(grupo: GrupoCondiciones): GrupoCondiciones | null {
  return tieneReglas(grupo) ? grupo : null;
}

/** Descripción de una regla, por ejemplo "Estado de Solicitud igual a En Curso". */
export function describirRegla(regla: ReglaCondicion, campos: ReadonlyMap<string, Campo>): string {
  const campo = campos.get(regla.campoId);
  const nombre = campo?.etiqueta ?? regla.campoId;
  const operador = ETIQUETAS_OPERADOR[regla.operador].toLowerCase();
  if (!requiereValor(regla.operador)) {
    return `${nombre} ${operador}`;
  }
  return `${nombre} ${operador} ${valorLegible(regla.valor ?? '', campo)}`.trim();
}

/** Muestra los valores como los lee el usuario: fechas DD/MM/AAAA y booleanos Sí/No. */
function valorLegible(valor: string, campo: Campo | undefined): string {
  const fecha = /^(\d{4})-(\d{2})-(\d{2})$/.exec(valor);
  if (campo?.tipoDato === 'Fecha' && fecha) {
    return `${fecha[3]}/${fecha[2]}/${fecha[1]}`;
  }
  if (campo?.tipoDato === 'Booleano') {
    return valor === 'true' ? 'Sí' : valor === 'false' ? 'No' : valor;
  }
  return valor;
}

/** Descripción completa de un grupo con sus conectores, por ejemplo "A y (B o C)". */
export function describirGrupo(grupo: GrupoCondiciones, campos: ReadonlyMap<string, Campo>): string {
  const conector = grupo.operadorLogico === 'Y' ? ' y ' : ' o ';
  const partes = [
    ...grupo.reglas.map((r) => describirRegla(r, campos)),
    ...grupo.grupos.filter(tieneReglas).map((g) => `(${describirGrupo(g, campos)})`),
  ];
  return partes.join(conector);
}

export function copiarGrupo(grupo: GrupoCondiciones): GrupoCondiciones {
  return structuredClone(grupo);
}
