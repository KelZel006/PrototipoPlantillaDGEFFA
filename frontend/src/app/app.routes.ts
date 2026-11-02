import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { UnidadService } from './core/unidad.service';

/** Las direcciones sin unidad llevan a la unidad abierta (la última elegida). */
const aLaUnidad = (seccion: 'consolidados' | 'vigentes') => () => `/unidades/${inject(UnidadService).actual()}/${seccion}`;

/**
 * Cada unidad tiene su menú: sus Documentos Plantillas y sus plantillas vigentes. Las pantallas del Documento
 * Consolidado se cargan aparte; el editor, solo al abrir una propuesta.
 */
export const rutas: Routes = [
  { path: '', pathMatch: 'full', redirectTo: aLaUnidad('consolidados') },
  { path: 'consolidados', pathMatch: 'full', redirectTo: aLaUnidad('consolidados') },
  { path: 'vigentes', pathMatch: 'full', redirectTo: aLaUnidad('vigentes') },
  {
    path: 'unidades/:unidad/consolidados',
    title: 'Documentos Plantillas',
    loadComponent: () => import('./consolidado/lista-consolidados/lista-consolidados').then((m) => m.ListaConsolidados),
  },
  {
    path: 'unidades/:unidad/vigentes',
    title: 'Consolidado de Plantillas',
    loadComponent: () => import('./consolidado/plantillas-vigentes/plantillas-vigentes').then((m) => m.PlantillasVigentes),
  },
  // El documento ya no tiene pantalla propia: sus plantillas se abren desde la lista.
  { path: 'consolidados/:id', pathMatch: 'full', redirectTo: aLaUnidad('consolidados') },
  {
    path: 'consolidados/:id/propuestas/:propuestaId',
    title: 'Editar propuesta',
    loadComponent: () => import('./consolidado/editor-propuesta/editor-propuesta').then((m) => m.EditorPropuesta),
  },
  {
    path: 'consolidados/:id/propuestas/:propuestaId/diferencias',
    title: 'Diferencias de la propuesta',
    loadComponent: () => import('./consolidado/comparacion/diferencias-propuesta').then((m) => m.DiferenciasPropuesta),
  },
  {
    path: 'consolidados/:id/revision',
    title: 'Revisión jurídica',
    loadComponent: () => import('./consolidado/revision/revision-consolidado').then((m) => m.RevisionConsolidado),
  },
  { path: '**', redirectTo: 'consolidados' },
];
