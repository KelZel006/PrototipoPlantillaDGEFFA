import { describirGrupo, normalizarCondicion, problemasCondicion, reglaCompleta } from './condiciones';
import { Campo, GrupoCondiciones } from './modelos';

const campo = (id: string, etiqueta: string): Campo => ({
  id,
  version: 1,
  etiqueta,
  categoria: 'Solicitud',
  tipoDato: 'Texto',
  origen: { coleccion: 'solicitudes', propiedad: id },
  contextos: ['considerando'],
});

const campos = new Map([
  ['estado_solicitud', campo('estado_solicitud', 'Estado de Solicitud')],
  ['monto_exoneracion', campo('monto_exoneracion', 'Monto Exoneración')],
  ['fecha_resolucion', { ...campo('fecha_resolucion', 'Fecha de Resolución'), tipoDato: 'Fecha' as const }],
]);

describe('condiciones', () => {
  it('describe un grupo con subgrupos y conectores', () => {
    const grupo: GrupoCondiciones = {
      operadorLogico: 'Y',
      reglas: [{ campoId: 'estado_solicitud', operador: 'IgualA', valor: 'En Curso' }],
      grupos: [
        {
          operadorLogico: 'O',
          reglas: [
            { campoId: 'monto_exoneracion', operador: 'MayorQue', valor: '100000' },
            { campoId: 'fecha_resolucion', operador: 'EsNulo', valor: null },
          ],
          grupos: [],
        },
      ],
    };
    expect(describirGrupo(grupo, campos)).toBe(
      'Estado de Solicitud igual a En Curso y (Monto Exoneración mayor que 100000 o Fecha de Resolución está vacío)',
    );
  });

  it('muestra las fechas en formato DD/MM/AAAA', () => {
    expect(describirGrupo({ operadorLogico: 'Y', reglas: [{ campoId: 'fecha_resolucion', operador: 'MenorQue', valor: '2026-12-31' }], grupos: [] }, campos)).toBe(
      'Fecha de Resolución menor que 31/12/2026',
    );
  });

  it('exige campo y, si el operador lo pide, un valor', () => {
    expect(reglaCompleta({ campoId: '', operador: 'IgualA', valor: 'x' })).toBe(false);
    expect(reglaCompleta({ campoId: 'estado_solicitud', operador: 'IgualA', valor: ' ' })).toBe(false);
    expect(reglaCompleta({ campoId: 'fecha_resolucion', operador: 'EsNulo', valor: null })).toBe(true);
  });

  it('explica las reglas incompletas y los grupos vacíos', () => {
    const grupo: GrupoCondiciones = {
      operadorLogico: 'Y',
      reglas: [{ campoId: '', operador: 'IgualA', valor: '' }],
      grupos: [{ operadorLogico: 'O', reglas: [], grupos: [] }],
    };
    expect(problemasCondicion(grupo)).toEqual([
      'Complete o quite las condiciones incompletas.',
      'Hay un grupo sin condiciones.',
    ]);
  });

  it('una condición sin reglas equivale a no tener condición', () => {
    expect(normalizarCondicion({ operadorLogico: 'Y', reglas: [], grupos: [] })).toBeNull();
  });
});
