import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { Router, provideRouter } from '@angular/router';
import { InteraccionService } from '../../core/interaccion.service';
import { DetalleConsolidado as Detalle, Usuario } from '../../core/modelos';
import { UsuarioPruebaService } from '../../core/usuario-prueba.service';
import { DetalleConsolidado } from '../detalle-consolidado/detalle-consolidado';
import { RevisionConsolidado } from './revision-consolidado';

registerLocaleData(localeEs);

@Component({ template: '' })
class Vacia {}

const USUARIOS: Usuario[] = [
  { id: 'a.perez', version: 1, nombreCompleto: 'Ana Pérez', roles: ['Analista'], activo: true },
  { id: 'j.ramos', version: 1, nombreCompleto: 'Julia Ramos', roles: ['OficialJuridico'], activo: true },
];

function detalle(estado: 'EnRegistro' | 'Verificado'): Detalle {
  return {
    consolidado: {
      id: 'CON-2', version: 3, numero: 'PLAN-20260002', anio: 2026, secuencia: 2, estado, analistaId: 'a.perez',
      fechaCreacion: '2026-10-12T15:00:00Z', fechaActualizacion: '2026-10-15T15:00:00Z', numeroPresentacion: 1,
    },
    tiposIncluidos: [{ tipoDocumentoId: 'TDO-001', tipoDocumento: 'Resolución', operacion: 'Modificar', plantillas: 1, ajustes: 1 }],
    propuestas: [
      {
        id: 'PRO-1', version: 1, consolidadoId: 'CON-2', operacion: 'Modificar', rubroId: 'R', subRubroId: 'S', tipoDocumentoId: 'TDO-001',
        claseDocumentoId: 'C', nombre: 'Resolución de prueba', plantillaId: 'PLA-001', retirada: false,
        fechaCreacion: '2026-10-12T15:00:00Z', fechaModificacion: '2026-10-12T15:00:00Z',
        contenidoPropuesto: { bloques: [{ codigo: 'CABECERA' }, { codigo: 'CONSIDERANDO' }] },
      },
    ],
    ajustes: { 'PRO-1': [{ tipo: 'ParrafoAgregado', bloqueCodigo: 'CONSIDERANDO', parrafoId: 'P9', etiqueta: 'NUEVO CONSIDERANDO (posición 2)' }] },
    presentaciones: [{ id: 'PRE-1', version: 1, consolidadoId: 'CON-2', numero: 1, usuario: 'a.perez', fecha: '2026-10-13T15:00:00Z', cantidadPlantillas: 1 }],
    devoluciones: [],
    movimientos: [],
  };
}

const estabilizar = async (fixture: ComponentFixture<unknown>) => {
  TestBed.tick();
  await new Promise((resolver) => setTimeout(resolver));
  TestBed.tick();
  fixture.detectChanges();
};

const boton = (elemento: HTMLElement, texto: string) =>
  Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes(texto))!;

describe('Revisión del Documento Consolidado', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: '**', component: Vacia }]), { provide: LOCALE_ID, useValue: 'es' }],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('el analista envía a verificación tras confirmar, y el documento sale de su bandeja', async () => {
    TestBed.inject(UsuarioPruebaService).seleccionar('a.perez');
    const fixture = TestBed.createComponent(DetalleConsolidado);
    fixture.componentRef.setInput('id', 'CON-2');
    fixture.detectChanges();
    await estabilizar(fixture);
    http.expectOne('/api/usuarios').flush(USUARIOS);
    http.match('/api/tipos-documento').forEach((p) => p.flush([]));
    http.expectOne('/api/consolidados/CON-2').flush(detalle('EnRegistro'));
    await estabilizar(fixture);
    http.match('/api/tipos-documento').forEach((p) => p.flush([]));
    const elemento: HTMLElement = fixture.nativeElement;
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigate');

    // El analista no ve las diferencias: las revisa el Oficial Jurídico.
    expect(Array.from(elemento.querySelectorAll('a')).some((a) => a.textContent?.includes('Diferencias'))).toBe(false);
    expect(elemento.textContent).toContain('Primera presentación');

    boton(elemento, 'ENVIAR A VERIFICACIÓN').click();
    await estabilizar(fixture);
    const interaccion = TestBed.inject(InteraccionService);
    expect(interaccion.confirmacion()?.titulo).toBe('Enviar PLAN-20260002 a verificación');
    interaccion.responder(true);
    await estabilizar(fixture);

    const envio = http.expectOne('/api/consolidados/CON-2/enviar');
    expect(envio.request.method).toBe('POST');
    envio.flush({ ...detalle('Verificado').consolidado, numeroPresentacion: 2 });
    expect(navegar).toHaveBeenCalledWith(['/consolidados']);
  });

  it('el Oficial Jurídico compara y devuelve con justificación y observaciones', async () => {
    TestBed.inject(UsuarioPruebaService).seleccionar('j.ramos');
    const fixture = TestBed.createComponent(RevisionConsolidado);
    fixture.componentRef.setInput('id', 'CON-2');
    fixture.detectChanges();
    await estabilizar(fixture);
    http.expectOne('/api/usuarios').flush(USUARIOS);
    http.expectOne('/api/consolidados/CON-2').flush(detalle('Verificado'));
    await estabilizar(fixture);
    http.expectOne('/api/propuestas/PRO-1/comparacion').flush({ propuestaId: 'PRO-1', operacion: 'Modificar', bloques: [], diseno: [], ajustes: [] });
    await estabilizar(fixture);
    const elemento: HTMLElement = fixture.nativeElement;

    expect(elemento.querySelector('.navegacion')?.textContent).toContain('NUEVO CONSIDERANDO (posición 2)');
    expect(boton(elemento, 'APROBAR').disabled).toBe(false);

    boton(elemento, 'DEVOLVER A REGISTRO').click();
    fixture.detectChanges();
    const devolver = () => Array.from(elemento.querySelectorAll<HTMLButtonElement>('dialog button')).find((b) => b.textContent?.includes('Devolver a registro'))!;
    expect(devolver().disabled).toBe(true);

    const justificacion = elemento.querySelector<HTMLTextAreaElement>('dialog textarea')!;
    justificacion.value = 'El considerando nuevo no cita la norma.';
    justificacion.dispatchEvent(new Event('input'));
    boton(elemento, 'Agregar observación').click();
    fixture.detectChanges();
    const selects = elemento.querySelectorAll<HTMLSelectElement>('dialog fieldset select');
    selects[1].value = 'CONSIDERANDO';
    selects[1].dispatchEvent(new Event('change'));
    const problema = elemento.querySelectorAll<HTMLTextAreaElement>('dialog fieldset textarea')[0];
    problema.value = 'Falta la referencia legal.';
    problema.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(devolver().disabled).toBe(false);

    devolver().click();
    await estabilizar(fixture);
    const interaccion = TestBed.inject(InteraccionService);
    expect(interaccion.confirmacion()?.mensaje).toContain('vuelve a la bandeja de a.perez');
    interaccion.responder(true);
    await estabilizar(fixture);

    const peticion = http.expectOne('/api/consolidados/CON-2/devolver');
    expect(peticion.request.body).toEqual({
      justificacion: 'El considerando nuevo no cita la norma.',
      observaciones: [{ propuestaId: 'PRO-1', bloqueCodigo: 'CONSIDERANDO', problema: 'Falta la referencia legal.', recomendacion: null }],
    });
    peticion.flush({});
  });

  it('el Oficial Jurídico aprueba tras confirmar y vuelve al documento aprobado', async () => {
    TestBed.inject(UsuarioPruebaService).seleccionar('j.ramos');
    const fixture = TestBed.createComponent(RevisionConsolidado);
    fixture.componentRef.setInput('id', 'CON-2');
    fixture.detectChanges();
    await estabilizar(fixture);
    http.expectOne('/api/usuarios').flush(USUARIOS);
    http.expectOne('/api/consolidados/CON-2').flush(detalle('Verificado'));
    await estabilizar(fixture);
    http.expectOne('/api/propuestas/PRO-1/comparacion').flush({ propuestaId: 'PRO-1', operacion: 'Modificar', bloques: [], diseno: [], ajustes: [] });
    await estabilizar(fixture);
    const navegar = vi.spyOn(TestBed.inject(Router), 'navigate');

    boton(fixture.nativeElement, 'APROBAR').click();
    await estabilizar(fixture);
    const interaccion = TestBed.inject(InteraccionService);
    expect(interaccion.confirmacion()?.mensaje).toContain('Se publicarán 1 plantilla como VIGENTES');
    interaccion.responder(true);
    await estabilizar(fixture);

    const aprobacion = http.expectOne('/api/consolidados/CON-2/aprobar');
    expect(aprobacion.request.method).toBe('POST');
    aprobacion.flush({ consolidado: { ...detalle('Verificado').consolidado, estado: 'Aprobado' }, publicadas: [{ id: 'VER-9', plantillaId: 'PLA-001', numeroVersion: 2 }] });
    expect(navegar).toHaveBeenCalledWith(['/consolidados', 'CON-2']);
    expect(interaccion.avisos().map((a) => a.texto)).toContain('PLAN-20260002 aprobado: 1 plantilla publicada.');
  });

  it('fuera de VERIFICADO no se revisa', async () => {
    TestBed.inject(UsuarioPruebaService).seleccionar('j.ramos');
    const fixture = TestBed.createComponent(RevisionConsolidado);
    fixture.componentRef.setInput('id', 'CON-2');
    fixture.detectChanges();
    await estabilizar(fixture);
    http.expectOne('/api/usuarios').flush(USUARIOS);
    http.expectOne('/api/consolidados/CON-2').flush({ ...detalle('Verificado'), consolidado: { ...detalle('Verificado').consolidado, estado: 'Aprobado' } });
    await estabilizar(fixture);

    expect(fixture.nativeElement.textContent).toContain('cuando el documento está VERIFICADO');
    expect(fixture.nativeElement.querySelector('app-comparacion-propuesta')).toBeNull();
  });
});
