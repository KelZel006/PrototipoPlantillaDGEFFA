import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { Router, provideRouter } from '@angular/router';
import { DetalleConsolidado as Detalle, Propuesta, ResumenConsolidado, Usuario } from '../core/modelos';
import { UsuarioPruebaService } from '../core/usuario-prueba.service';
import { DetalleConsolidado } from './detalle-consolidado/detalle-consolidado';
import { ListaConsolidados } from './lista-consolidados/lista-consolidados';
import { SeleccionPlantilla } from './seleccion-plantilla/seleccion-plantilla';

registerLocaleData(localeEs);

@Component({ template: '' })
class Vacia {}

const USUARIOS: Usuario[] = [
  { id: 'a.perez', version: 1, nombreCompleto: 'Ana Pérez', roles: ['Analista'], activo: true },
  { id: 'j.ramos', version: 1, nombreCompleto: 'Julia Ramos', roles: ['OficialJuridico'], activo: true },
];

const LISTA: ResumenConsolidado[] = [
  {
    id: 'CON-2', numero: 'PLAN-20260002', anio: 2026, estado: 'EnRegistro', analistaId: 'a.perez',
    fechaCreacion: '2026-10-12T15:00:00Z', fechaActualizacion: '2026-10-15T15:00:00Z',
    tiposDocumento: ['Certificación', 'Resolución'], totalPlantillas: 2,
  },
  {
    id: 'CON-1', numero: 'PLAN-20260001', anio: 2026, estado: 'Aprobado', analistaId: 'a.perez',
    fechaCreacion: '2026-10-05T15:00:00Z', fechaActualizacion: '2026-10-07T15:00:00Z',
    tiposDocumento: ['Dictamen'], totalPlantillas: 1,
  },
];

function propuesta(id: string, cambios: Partial<Propuesta> = {}): Propuesta {
  return {
    id, version: 1, consolidadoId: 'CON-2', operacion: 'Modificar', rubroId: '1100', subRubroId: '1107',
    tipoDocumentoId: 'TDO-001', claseDocumentoId: 'CDO-001', nombre: `Plantilla ${id}`, plantillaId: 'PLA-001',
    retirada: false, fechaCreacion: '2026-10-12T15:00:00Z', fechaModificacion: '2026-10-12T15:00:00Z', ...cambios,
  };
}

const DETALLE: Detalle = {
  consolidado: {
    id: 'CON-2', version: 3, numero: 'PLAN-20260002', anio: 2026, secuencia: 2, estado: 'EnRegistro', analistaId: 'a.perez',
    fechaCreacion: '2026-10-12T15:00:00Z', fechaActualizacion: '2026-10-15T15:00:00Z', numeroPresentacion: 1,
  },
  tiposIncluidos: [
    { tipoDocumentoId: 'TDO-004', tipoDocumento: 'Certificación', operacion: 'Modificar', plantillas: 1, ajustes: 1 },
    { tipoDocumentoId: 'TDO-001', tipoDocumento: 'Resolución', operacion: 'Modificar', plantillas: 1, ajustes: 2 },
  ],
  propuestas: [
    propuesta('PRO-1', { nombre: 'Resolución de prueba' }),
    propuesta('PRO-2', { nombre: 'Certificación de prueba', tipoDocumentoId: 'TDO-004', plantillaId: 'PLA-004', derivadaDePropuestaId: 'PRO-1' }),
  ],
  ajustes: {
    'PRO-1': [
      { tipo: 'ParrafoAgregado', bloqueCodigo: 'CONSIDERANDO', parrafoId: 'P9', etiqueta: 'NUEVO CONSIDERANDO (posición 2)' },
      { tipo: 'ParrafoModificado', bloqueCodigo: 'POR_TANTO', parrafoId: 'P8', etiqueta: 'POR TANTO 1 modificado' },
    ],
    'PRO-2': [{ tipo: 'ParrafoAgregado', bloqueCodigo: 'CONSIDERANDO', parrafoId: 'P10', etiqueta: 'NUEVO CONSIDERANDO (posición 2)' }],
  },
  presentaciones: [],
  devoluciones: [
    {
      id: 'DEV-1', version: 1, consolidadoId: 'CON-2', numeroPresentacion: 1, usuario: 'j.ramos', fecha: '2026-10-14T16:00:00Z',
      justificacion: 'Falta citar la constancia.',
      observaciones: [{ propuestaId: 'PRO-1', bloqueCodigo: 'CONSIDERANDO', problema: 'No cita la constancia.' }],
    },
  ],
  movimientos: [
    { id: 'MOV-1', version: 1, consolidadoId: 'CON-2', tipo: 'Creacion', usuario: 'a.perez', fecha: '2026-10-12T15:00:00Z', detalle: 'Se creó.' },
  ],
};

const estabilizar = async (fixture: ComponentFixture<unknown>) => {
  TestBed.tick();
  await new Promise((resolver) => setTimeout(resolver));
  TestBed.tick();
  fixture.detectChanges();
};

const botones = (elemento: HTMLElement, texto: string) =>
  Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).filter((b) => b.textContent?.includes(texto));

describe('Documento Consolidado', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: Vacia }]),
        { provide: LOCALE_ID, useValue: 'es' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const comoUsuario = (id: string) => TestBed.inject(UsuarioPruebaService).seleccionar(id);

  describe('Pantalla A: listado', () => {
    const abrir = async (usuario: string) => {
      comoUsuario(usuario);
      const fixture = TestBed.createComponent(ListaConsolidados);
      fixture.detectChanges();
      await estabilizar(fixture);
      http.expectOne('/api/usuarios').flush(USUARIOS);
      http.expectOne('/api/consolidados').flush(LISTA);
      await estabilizar(fixture);
      return fixture;
    };

    it('lista los documentos con sus tipos y filtra por estado', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;

      const filas = () => Array.from(elemento.querySelectorAll('tbody tr')).map((f) => f.textContent?.replace(/\s+/g, ' '));
      expect(filas()).toHaveLength(2);
      expect(filas()[0]).toContain('PLAN-20260002');
      expect(filas()[0]).toContain('Certificación, Resolución');
      expect(filas()[0]).toContain('EN REGISTRO');

      const estado = elemento.querySelectorAll<HTMLSelectElement>('.filtros select')[1];
      estado.value = 'Aprobado';
      estado.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      expect(filas()).toHaveLength(1);
      expect(filas()[0]).toContain('PLAN-20260001');
    });

    it('el analista crea un consolidado sin escribir el número', async () => {
      const fixture = await abrir('a.perez');
      const navegar = vi.spyOn(TestBed.inject(Router), 'navigate');

      botones(fixture.nativeElement, 'Nuevo consolidado')[0].click();
      const creacion = http.expectOne('/api/consolidados');
      expect(creacion.request.method).toBe('POST');
      expect(creacion.request.body).toBeNull();
      creacion.flush({ ...DETALLE.consolidado, id: 'CON-9', numero: 'PLAN-20260009' });

      expect(navegar).toHaveBeenCalledWith(['/consolidados', 'CON-9']);
    });

    it('el Oficial Jurídico no crea consolidados', async () => {
      const fixture = await abrir('j.ramos');

      expect(botones(fixture.nativeElement, 'Nuevo consolidado')).toHaveLength(0);
      expect(fixture.nativeElement.textContent).toContain('pendientes de revisión jurídica');
    });
  });

  describe('Pantalla B: registro del consolidado', () => {
    const abrir = async (usuario: string, detalle: Detalle = DETALLE) => {
      comoUsuario(usuario);
      const fixture = TestBed.createComponent(DetalleConsolidado);
      fixture.componentRef.setInput('id', 'CON-2');
      fixture.detectChanges();
      await estabilizar(fixture);
      http.expectOne('/api/usuarios').flush(USUARIOS);
      http.match('/api/tipos-documento').forEach((p) =>
        p.flush([
          { id: 'TDO-001', version: 1, nombre: 'Resolución', activo: true },
          { id: 'TDO-004', version: 1, nombre: 'Certificación', copiaDeTipoDocumentoId: 'TDO-001', activo: true },
        ]),
      );
      http.expectOne('/api/consolidados/CON-2').flush(detalle);
      await estabilizar(fixture);
      http.match('/api/tipos-documento').forEach((p) => p.flush([]));
      await estabilizar(fixture);
      return fixture;
    };

    it('muestra cada tipo una vez, sus ajustes, la devolución y las acciones del analista', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;

      const tipos = Array.from(elemento.querySelectorAll('tbody th')).map((t) => t.textContent?.trim());
      expect(tipos).toEqual(['Certificación', 'Resolución']);
      expect(elemento.querySelector('.aviso-devolucion')?.textContent).toContain('Falta citar la constancia.');
      expect(elemento.querySelector('.aviso-devolucion')?.textContent).toContain('Resolución de prueba · CONSIDERANDO');

      botones(elemento, 'Ver ajustes')[1].click();
      fixture.detectChanges();
      expect(Array.from(elemento.querySelectorAll('.ajuste')).map((a) => a.textContent?.trim())).toEqual([
        'NUEVO CONSIDERANDO (posición 2)',
        'POR TANTO 1 modificado',
      ]);

      expect(botones(elemento, 'CREAR PLANTILLA')).toHaveLength(1);
      expect(botones(elemento, 'MODIFICAR PLANTILLA')).toHaveLength(1);
      // La Certificación que acompaña a la Resolución no se retira sola.
      expect(botones(elemento, 'Retirar')).toHaveLength(1);
      expect(elemento.textContent).toContain('Acompaña a «Resolución de prueba»');
    });

    it('retira una propuesta con su motivo', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;

      botones(elemento, 'Retirar')[0].click();
      fixture.detectChanges();
      expect(elemento.querySelector('dialog[aria-labelledby="titulo-retiro"]')?.textContent).toContain('También se retira «Certificación de prueba»');
      const motivo = elemento.querySelector<HTMLTextAreaElement>('dialog[aria-labelledby="titulo-retiro"] textarea')!;
      motivo.value = 'Se hará en otro documento';
      motivo.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      botones(elemento, 'Retirar').at(-1)!.click();

      const retiro = http.expectOne('/api/propuestas/PRO-1/retirar');
      expect(retiro.request.body).toEqual({ motivo: 'Se hará en otro documento' });
      retiro.flush([propuesta('PRO-1', { retirada: true }), propuesta('PRO-2', { retirada: true })]);
      await estabilizar(fixture);
      http.expectOne('/api/consolidados/CON-2').flush(DETALLE);
    });

    it('otro usuario solo consulta', async () => {
      const fixture = await abrir('j.ramos', { ...DETALLE, consolidado: { ...DETALLE.consolidado, estado: 'Verificado' } });
      const elemento: HTMLElement = fixture.nativeElement;

      expect(botones(elemento, 'CREAR PLANTILLA')).toHaveLength(0);
      expect(botones(elemento, 'Retirar')).toHaveLength(0);
      expect(elemento.textContent).toContain('pendiente de revisión jurídica');
      expect(elemento.querySelector('.aviso-devolucion')).toBeNull();
    });
  });

  describe('Pantalla C: selección de plantilla', () => {
    it('MODIFICAR lista las vigentes y deshabilita las que están en otro documento', async () => {
      const fixture = TestBed.createComponent(SeleccionPlantilla);
      const agregadas: Propuesta[][] = [];
      fixture.componentInstance.agregado.subscribe((p) => agregadas.push(p));
      fixture.componentRef.setInput('consolidadoId', 'CON-2');
      fixture.componentRef.setInput('operacion', 'Modificar');
      fixture.detectChanges();
      await estabilizar(fixture);
      http.expectOne('/api/tipos-documento').flush([
        { id: 'TDO-001', version: 1, nombre: 'Resolución', activo: true },
        { id: 'TDO-004', version: 1, nombre: 'Certificación', copiaDeTipoDocumentoId: 'TDO-001', activo: true },
      ]);
      http.expectOne('/api/plantillas-vigentes').flush([
        {
          plantillaId: 'PLA-001', nombre: 'Resolución vigente', rubroId: '1100', subRubroId: '1107', tipoDocumentoId: 'TDO-001',
          tipoDocumento: 'Resolución', claseDocumentoId: 'CDO-001', versionId: 'VER-1', numeroVersion: 3, fechaPublicacion: '2026-10-01T00:00:00Z',
        },
        {
          plantillaId: 'PLA-002', nombre: 'Dictamen ocupado', rubroId: '1800', subRubroId: '1801', tipoDocumentoId: 'TDO-002',
          tipoDocumento: 'Dictamen', claseDocumentoId: 'CDO-001', versionId: 'VER-2', numeroVersion: 1, fechaPublicacion: '2026-10-01T00:00:00Z',
          enConsolidado: 'PLAN-20260007',
        },
      ]);
      await estabilizar(fixture);
      const elemento: HTMLElement = fixture.nativeElement;

      const items = elemento.querySelectorAll('.vigentes li');
      expect(items[0].textContent).toContain('versión vigente 3');
      expect(items[0].textContent).toContain('Se agregará también su Certificación');
      expect(items[1].textContent).toContain('Ya está en el documento PLAN-20260007');
      expect(items[1].querySelector('button')?.disabled).toBe(true);

      items[0].querySelector('button')!.click();
      const modificar = http.expectOne('/api/consolidados/CON-2/propuestas/modificar');
      expect(modificar.request.body).toEqual({ plantillaId: 'PLA-001' });
      modificar.flush([propuesta('PRO-1'), propuesta('PRO-2')]);
      await estabilizar(fixture);
      http.expectOne('/api/plantillas-vigentes').flush([]);

      expect(agregadas[0].map((p) => p.id)).toEqual(['PRO-1', 'PRO-2']);
    });
  });
});
