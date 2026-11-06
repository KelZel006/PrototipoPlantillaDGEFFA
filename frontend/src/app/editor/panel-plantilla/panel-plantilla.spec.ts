import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { Router, provideRouter } from '@angular/router';
import { InteraccionService } from '../../core/interaccion.service';
import { DetalleConsolidado, PlantillaEdicion, Propuesta } from '../../core/modelos';
import { PlantillaActualService } from '../../core/plantilla-actual.service';
import { PLANTILLA } from '../../pruebas/datos-prueba';
import { PanelPlantilla } from './panel-plantilla';

registerLocaleData(localeEs);

const TIPOS = [
  { id: 'TDO-001', version: 1, nombre: 'Resolución', activo: true },
  { id: 'TDO-004', version: 1, nombre: 'Certificación', textoCorrido: true, copiaDeTipoDocumentoId: 'TDO-001', activo: true },
];

function propuesta(id: string, cambios: Partial<Propuesta> = {}): Propuesta {
  return {
    id, version: 1, consolidadoId: 'CON-001', unidadId: 'exoneraciones', operacion: 'Modificar', rubroId: 'R', subRubroId: 'S',
    tipoDocumentoId: 'TDO-001', claseDocumentoId: 'C', beneficioId: 'BEN-001', nombre: 'Plantilla de prueba', plantillaId: 'PLA-001', retirada: false,
    fechaCreacion: '2026-10-12T15:00:00Z', fechaModificacion: '2026-10-12T15:00:00Z', contenidoPropuesto: { bloques: [] }, ...cambios,
  } as Propuesta;
}

/** Documento de la propuesta abierta: dos plantillas y una devolución del Oficial Jurídico (que el editor no muestra). */
const DOCUMENTO: DetalleConsolidado = {
  consolidado: {
    id: 'CON-001', version: 2, numero: 'PLAN-20260001', anio: 2026, secuencia: 1, estado: 'EnRegistro', unidadId: 'exoneraciones',
    analistaId: 'usuario.prueba', fechaCreacion: '2026-10-12T15:00:00Z', fechaActualizacion: '2026-10-15T15:00:00Z', numeroPresentacion: 1,
  },
  tiposIncluidos: [],
  propuestas: [
    propuesta('PRO-001', { nombreReferencia: 'Exoneración a docentes' }),
    propuesta('PRO-002', { nombre: 'Certificación de prueba', tipoDocumentoId: 'TDO-004', derivadaDePropuestaId: 'PRO-001' }),
  ],
  ajustes: {},
  presentaciones: [],
  devoluciones: [
    {
      id: 'DEV-1', version: 1, consolidadoId: 'CON-001', numeroPresentacion: 1, usuario: 'j.ramos', fecha: '2026-10-14T16:00:00Z',
      justificacion: 'Falta citar la constancia.',
      observaciones: [{ propuestaId: 'PRO-001', bloqueCodigo: 'CONSIDERANDO', problema: 'No cita la constancia.', recomendacion: 'Agregarla.' }],
    },
  ],
  movimientos: [],
};

describe('PanelPlantilla', () => {
  let fixture: ComponentFixture<PanelPlantilla>;
  let http: HttpTestingController;
  let elemento: HTMLElement;

  // No se usa whenStable(): esperaría a que respondan las peticiones pendientes de los recursos.
  const estabilizar = async () => {
    TestBed.tick();
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PanelPlantilla],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: LOCALE_ID, useValue: 'es' }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PanelPlantilla);
    elemento = fixture.nativeElement;
  });

  afterEach(() => {
    // El detalle del documento (solo si la plantilla es editable) y los usuarios de prueba.
    http.match((r) => r.url.startsWith('/api/consolidados/')).forEach((p) => p.cancelled || p.flush(DOCUMENTO));
    http.match('/api/usuarios').forEach((p) => p.flush([]));
    // Catálogos del diálogo para agregar otra plantilla.
    for (const url of ['/api/plantillas-vigentes', '/api/rubros', '/api/clases-documento', '/api/beneficios', '/api/tipos-documento']) {
      http.match(url).forEach((p) => p.cancelled || p.flush([]));
    }
    http.verify();
  });

  const boton = (texto: string) => Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes(texto));

  /** Abre la propuesta y responde las consultas iniciales. */
  const abrir = async (propuesta: PlantillaEdicion) => {
    TestBed.inject(PlantillaActualService).abrir(propuesta.id);
    fixture.detectChanges();
    await estabilizar();
    http.expectOne('/api/tipos-documento').flush(TIPOS);
    http.expectOne(`/api/propuestas/${propuesta.id}/edicion`).flush(propuesta);
    await estabilizar();
  };

  it('muestra la operación, el documento consolidado y el diseño si la propuesta es editable', async () => {
    await abrir(PLANTILLA);

    expect(elemento.textContent).toContain('MODIFICAR');
    expect(elemento.textContent).toContain('Plantilla de prueba');
    expect(elemento.textContent).toContain('PLAN-20260001');
    // El documento ya no tiene pantalla propia: el número no es un enlace.
    expect(elemento.querySelector('a')).toBeNull();
    expect(elemento.textContent).toContain('EN REGISTRO');
    expect(elemento.querySelector('app-diseno-documento')).not.toBeNull();
    expect(elemento.textContent).not.toContain('Solo lectura');
  });

  it('sin permiso de edición queda en solo lectura, sin el diseño', async () => {
    await abrir({ ...PLANTILLA, editable: false, estadoConsolidado: 'Verificado' });

    expect(elemento.textContent).toContain('Solo lectura');
    expect(elemento.textContent).toContain('VERIFICADO');
    expect(elemento.querySelector('app-diseno-documento')).toBeNull();
  });

  it('la Certificación que acompaña a la Resolución lo avisa y advierte el texto corrido', async () => {
    await abrir({ ...PLANTILLA, id: 'PRO-002', tipoDocumentoId: 'TDO-004', derivadaDePropuestaId: 'PRO-001' });

    expect(elemento.querySelector('.aviso-copia')?.textContent).toContain('acompaña a la Resolución');
    expect(elemento.querySelector('.nota-corrido')?.textContent).toContain('Certificación: el texto se genera corrido');
  });

  describe('Vista previa en PDF', () => {
    const comoUsuario = async (roles: string[]) => {
      http.expectOne('/api/usuarios').flush([{ id: 'usuario.prueba', version: 1, nombreCompleto: 'Usuario de prueba', roles, activo: true }]);
      await estabilizar();
    };

    it('el analista la ve entre el título y el número del documento, con los datos de una solicitud de la misma combinación', async () => {
      await abrir({ ...PLANTILLA, editable: false, estadoConsolidado: 'Verificado' });
      await comoUsuario(['Analista']);
      const resumen = Array.from(elemento.querySelector('.resumen')!.children).map((e) => (e.tagName === 'BUTTON' ? 'BUTTON' : e.className));
      const boton = elemento.querySelector<HTMLButtonElement>('.resumen > button')!;
      expect(boton.textContent?.trim()).toBe('Vista previa en PDF');
      // Orden: título, botón, número del documento.
      expect(resumen.indexOf('nombre')).toBeLessThan(resumen.indexOf('BUTTON'));
      expect(resumen.indexOf('BUTTON')).toBeLessThan(resumen.findIndex((c) => c.includes('texto-secundario')));

      const ventana = { location: { href: '' }, close: vi.fn() } as unknown as Window;
      vi.spyOn(window, 'open').mockReturnValue(ventana);
      vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:pdf');
      boton.click();
      http.expectOne('/api/solicitudes').flush([
        { id: 'SOL-9', version: 1, numeroSolicitud: 'PRUEBA-9', beneficiario: 'Otro', rubroId: 'X', subRubroId: 'Y', esDatoPrueba: true },
        { id: 'SOL-1', version: 1, numeroSolicitud: 'PRUEBA-1', beneficiario: 'Uno', rubroId: 'RUB-001', subRubroId: 'SUB-001', esDatoPrueba: true },
      ]);
      const pdf = http.expectOne((r) => r.url === '/api/propuestas/PRO-001/pdf');
      expect(pdf.request.params.get('solicitudId')).toBe('SOL-1');
      pdf.flush(new Blob(['%PDF'], { type: 'application/pdf' }));
      await estabilizar();
      expect(ventana.location.href).toBe('blob:pdf');
    });

    it('otro perfil no la ve', async () => {
      await abrir({ ...PLANTILLA, editable: false, estadoConsolidado: 'Verificado' });
      await comoUsuario(['OficialJuridico']);
      expect(elemento.textContent).not.toContain('Vista previa en PDF');
    });
  });

  describe('Nombre Plantilla', () => {
    it('se muestra arriba en negrita, con el nombre técnico debajo, y ya no se edita aquí', async () => {
      await abrir({ ...PLANTILLA, nombreReferencia: 'Referencia guardada' });

      expect(elemento.querySelector('h2')?.textContent).toBe('Referencia guardada');
      expect(elemento.querySelector('.nombre-tecnico')?.textContent).toBe('Plantilla de prueba');
      expect(elemento.querySelector('#nombre-referencia')).toBeNull();
      expect(Array.from(elemento.querySelectorAll('button')).some((b) => b.textContent?.trim() === 'Guardar')).toBe(false);
    });

    it('sin nombre de referencia, el título es el nombre técnico', async () => {
      await abrir(PLANTILLA);

      expect(elemento.querySelector('h2')?.textContent).toBe('Plantilla de prueba');
      expect(elemento.querySelector('.nombre-tecnico')).toBeNull();
    });
  });

  describe('Documento consolidado', () => {
    /** Abre la plantilla editable y responde el detalle de su documento. */
    const abrirEditable = async (documento = DOCUMENTO) => {
      await abrir(PLANTILLA);
      http.expectOne('/api/consolidados/CON-001').flush(documento);
      await estabilizar();
    };

    it('«Enviar a verificación» envía el documento tras confirmar', async () => {
      await abrirEditable();
      const navegar = vi.spyOn(TestBed.inject(Router), 'navigate');

      const enviar = elemento.querySelector<HTMLButtonElement>('.acciones-documento .enviar')!;
      expect(enviar.textContent?.trim()).toBe('Enviar a verificación');
      enviar.click();
      await estabilizar();
      const interaccion = TestBed.inject(InteraccionService);
      expect(interaccion.confirmacion()?.titulo).toBe('Enviar PLAN-20260001 a verificación');
      expect(interaccion.confirmacion()?.mensaje).toContain('Se enviarán 2 plantillas al Oficial Jurídico');
      interaccion.responder(true);
      await estabilizar();

      const envio = http.expectOne('/api/consolidados/CON-001/enviar');
      expect(envio.request.method).toBe('POST');
      envio.flush({ ...DOCUMENTO.consolidado, estado: 'Verificado', numeroPresentacion: 2 });
      expect(navegar).toHaveBeenCalledWith(['/consolidados']);
      expect(interaccion.avisos().map((a) => a.texto)).toContain('PLAN-20260001 se envió a verificación.');
    });

    it('sin confirmar no se envía', async () => {
      await abrirEditable();

      boton('Enviar a verificación')!.click();
      await estabilizar();
      TestBed.inject(InteraccionService).responder(false);
      await estabilizar();
      http.expectNone('/api/consolidados/CON-001/enviar');
    });

    it('no tiene «+ Agregar plantilla», «Retirar», ni los avisos de bloques obligatorios o de devolución', async () => {
      await abrirEditable();

      expect(boton('Agregar plantilla')).toBeUndefined();
      expect(boton('Retirar')).toBeUndefined();
      expect(elemento.querySelector('.aviso-obligatorio')).toBeNull();
      expect(elemento.textContent).not.toContain('Bloques obligatorios sin párrafos activos');
      expect(elemento.querySelector('.aviso-devolucion')).toBeNull();
      expect(elemento.textContent).not.toContain('Devuelto a registro');
    });

    it('en solo lectura no se envía ni se pide el documento', async () => {
      await abrir({ ...PLANTILLA, editable: false, estadoConsolidado: 'Verificado' });

      expect(boton('Enviar a verificación')).toBeUndefined();
      http.expectNone('/api/consolidados/CON-001');
    });
  });
});
