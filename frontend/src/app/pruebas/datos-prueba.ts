import { Campo, OperadoresPorTipo, Parrafo, Plantilla } from '../core/modelos';

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

export const PLANTILLA: Plantilla = {
  id: 'PLA-001',
  version: 1,
  nombre: 'Plantilla de prueba',
  rubroId: 'RUB-001',
  subRubroId: 'SUB-001',
  tipoDocumentoId: 'TDO-001',
  claseDocumentoId: 'CDO-001',
  estado: 'Borrador',
  fechaModificacion: '2026-10-14T00:00:00+00:00',
};

export function parrafo(id: string, texto: string, cambios: Partial<Parrafo> = {}): Parrafo {
  return {
    id,
    version: 1,
    plantillaId: 'PLA-001',
    seccion: 'Considerando',
    orden: 1,
    contenido: { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: texto }] }] },
    activo: true,
    mostrarSoloSiCumple: false,
    camposUsados: [],
    fechaModificacion: '2026-10-14T00:00:00+00:00',
    ...cambios,
  };
}
