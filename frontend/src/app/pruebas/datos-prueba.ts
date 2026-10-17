import { BloqueConfigurado, Campo, OperadoresPorTipo, Parrafo, PlantillaEdicion } from '../core/modelos';

/** Datos ficticios compartidos por las pruebas del frontend. */

export const CAMPOS: Campo[] = [
  {
    id: 'operador',
    version: 1,
    etiqueta: 'Operador',
    categoria: 'Solicitud',
    tipoDato: 'Texto',
    origen: { coleccion: 'solicitudes', propiedad: 'operador' },
    contextos: ['considerando'],
  },
  {
    id: 'numero_solicitud',
    version: 1,
    etiqueta: 'Número de Solicitud',
    categoria: 'Solicitud',
    tipoDato: 'Texto',
    origen: { coleccion: 'solicitudes', propiedad: 'numeroSolicitud' },
    contextos: ['considerando'],
  },
  {
    id: 'monto_exoneracion',
    version: 1,
    etiqueta: 'Monto Exoneración',
    categoria: 'Resolución',
    tipoDato: 'Moneda',
    origen: { coleccion: 'solicitudes', propiedad: 'montoExoneracion' },
    contextos: ['considerando'],
  },
  {
    id: 'codigo_arancelario',
    version: 1,
    etiqueta: 'Código Arancelario',
    categoria: 'Detalle de bienes',
    tipoDato: 'Texto',
    origen: { coleccion: 'detallesBienes', propiedad: 'codigoArancelario' },
    contextos: ['tabla'],
  },
];

export const OPERADORES: OperadoresPorTipo = {
  Texto: ['IgualA', 'DistintoDe', 'Contiene', 'NoContiene', 'EsNulo', 'NoEsNulo'],
  Numero: ['IgualA', 'DistintoDe', 'MayorQue', 'MenorQue', 'MayorOIgualQue', 'MenorOIgualQue', 'EsNulo', 'NoEsNulo'],
  Moneda: ['IgualA', 'DistintoDe', 'MayorQue', 'MenorQue', 'MayorOIgualQue', 'MenorOIgualQue', 'EsNulo', 'NoEsNulo'],
  Fecha: ['IgualA', 'DistintoDe', 'MayorQue', 'MenorQue', 'MayorOIgualQue', 'MenorOIgualQue', 'EsNulo', 'NoEsNulo'],
  Booleano: ['IgualA', 'DistintoDe', 'EsNulo', 'NoEsNulo'],
};

/** Los cinco bloques originales, como los configura el servidor por defecto. */
export const BLOQUES: BloqueConfigurado[] = [
  { codigo: 'CABECERA', nombre: 'Cabecera', numeracion: 'Ninguna', esFirmas: false, contextoCampos: 'cabecera', obligatorio: true, orden: 1 },
  { codigo: 'CONSIDERANDO', nombre: 'Considerando', numeracion: 'Considerando', esFirmas: false, contextoCampos: 'considerando', obligatorio: false, orden: 2 },
  { codigo: 'POR_TANTO', nombre: 'Por Tanto', titulo: 'POR TANTO:', numeracion: 'Ninguna', esFirmas: false, contextoCampos: 'portanto', obligatorio: false, orden: 3 },
  { codigo: 'RESUELVE', nombre: 'Resuelve', titulo: 'RESUELVE:', numeracion: 'Ordinal', esFirmas: false, contextoCampos: 'resuelve', obligatorio: false, orden: 4 },
  { codigo: 'FIRMAS', nombre: 'Firmas', numeracion: 'Ninguna', esFirmas: true, contextoCampos: 'firmas', obligatorio: true, orden: 5 },
];

/** Propuesta en edición (MODIFICAR de PLA-001) dentro de un consolidado EN REGISTRO. */
export const PLANTILLA: PlantillaEdicion = {
  id: 'PRO-001',
  version: 1,
  nombre: 'Plantilla de prueba',
  rubroId: 'RUB-001',
  subRubroId: 'SUB-001',
  tipoDocumentoId: 'TDO-001',
  claseDocumentoId: 'CDO-001',
  fechaModificacion: '2026-10-14T00:00:00+00:00',
  operacion: 'Modificar',
  consolidadoId: 'CON-001',
  consolidadoNumero: 'PLAN-20260001',
  estadoConsolidado: 'EnRegistro',
  editable: true,
  retirada: false,
  plantillaId: 'PLA-001',
  bloques: BLOQUES,
  obligatoriosVacios: [],
};

export function parrafo(id: string, texto: string, cambios: Partial<Parrafo> = {}): Parrafo {
  return {
    id,
    version: 1,
    propuestaId: 'PRO-001',
    bloque: 'CONSIDERANDO',
    orden: 1,
    contenido: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: texto }] }] },
    activo: true,
    mostrarSoloSiCumple: false,
    camposUsados: [],
    ...cambios,
  };
}
