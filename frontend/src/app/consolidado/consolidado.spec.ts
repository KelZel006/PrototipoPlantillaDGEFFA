import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, LOCALE_ID } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { Router, provideRouter } from '@angular/router';
import { DetalleConsolidado as Detalle, PlantillaDelResumen, Propuesta, ResumenConsolidado, Usuario } from '../core/modelos';
import { InteraccionService } from '../core/interaccion.service';
import { UsuarioPruebaService } from '../core/usuario-prueba.service';
import { ListaConsolidados } from './lista-consolidados/lista-consolidados';
import { SeleccionPlantilla } from './seleccion-plantilla/seleccion-plantilla';
import { responderUnidades } from '../pruebas/datos-prueba';
import { UnidadService } from '../core/unidad.service';

registerLocaleData(localeEs);

@Component({ template: '' })
class Vacia {}

const USUARIOS: Usuario[] = [
  { id: 'a.perez', version: 1, nombreCompleto: 'Ana Pérez', roles: ['Analista'], activo: true },
  { id: 'j.ramos', version: 1, nombreCompleto: 'Julia Ramos', roles: ['OficialJuridico'], activo: true },
];

function plantillaResumen(propuestaId: string, cambios: Partial<PlantillaDelResumen> = {}): PlantillaDelResumen {
  return {
    propuestaId, nombre: `Plantilla ${propuestaId}`, operacion: 'Modificar',
    rubroId: '1100', rubro: 'POR TRATADOS Y CONVENIOS', subRubroId: '1107', subRubro: 'FONDOS BILATERALES',
    tipoDocumentoId: 'TDO-001', tipoDocumento: 'Resolución', claseDocumentoId: 'CDO-001', claseDocumento: 'Original', ...cambios,
  };
}

const LISTA: ResumenConsolidado[] = [
  {
    id: 'CON-2', numero: 'PLAN-20260002', anio: 2026, estado: 'EnRegistro', unidadId: 'exoneraciones', analistaId: 'a.perez',
    fechaCreacion: '2026-10-12T15:00:00Z', fechaActualizacion: '2026-10-15T15:00:00Z',
    tiposDocumento: ['Certificación', 'Resolución'], totalPlantillas: 2,
    plantillas: [
      plantillaResumen('PRO-1'),
      plantillaResumen('PRO-2', { tipoDocumentoId: 'TDO-004', tipoDocumento: 'Certificación', operacion: 'Crear' }),
    ],
  },
  {
    id: 'CON-1', numero: 'PLAN-20260001', anio: 2026, estado: 'Aprobado', unidadId: 'exoneraciones', analistaId: 'a.perez',
    fechaCreacion: '2026-10-05T15:00:00Z', fechaActualizacion: '2026-10-07T15:00:00Z',
    tiposDocumento: ['Dictamen'], totalPlantillas: 1,
    plantillas: [
      plantillaResumen('PRO-3', {
        rubroId: '1800', rubro: 'ASOCIACIONES', subRubroId: '1801', subRubro: 'ONGS',
        tipoDocumentoId: 'TDO-002', tipoDocumento: 'Dictamen', claseDocumentoId: 'CDO-002', claseDocumento: 'Modificación',
      }),
    ],
  },
];

function propuesta(id: string, cambios: Partial<Propuesta> = {}): Propuesta {
  return {
    id, version: 1, consolidadoId: 'CON-2', unidadId: 'exoneraciones', operacion: 'Modificar', rubroId: '1100', subRubroId: '1107',
    tipoDocumentoId: 'TDO-001', claseDocumentoId: 'CDO-001', nombre: `Plantilla ${id}`, plantillaId: 'PLA-001',
    retirada: false, fechaCreacion: '2026-10-12T15:00:00Z', fechaModificacion: '2026-10-12T15:00:00Z', ...cambios,
  };
}

const DETALLE: Detalle = {
  consolidado: {
    id: 'CON-2', version: 3, numero: 'PLAN-20260002', anio: 2026, secuencia: 2, estado: 'EnRegistro', unidadId: 'exoneraciones', analistaId: 'a.perez',
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

  afterEach(() => {
    responderUnidades(http);
    http.verify();
  });

  const comoUsuario = (id: string) => TestBed.inject(UsuarioPruebaService).seleccionar(id);

  describe('Pantalla A: listado', () => {
    const abrir = async (usuario: string, unidad?: string, lista = LISTA) => {
      comoUsuario(usuario);
      const fixture = TestBed.createComponent(ListaConsolidados);
      fixture.componentRef.setInput('unidad', unidad);
      fixture.detectChanges();
      await estabilizar(fixture);
      http.expectOne('/api/usuarios').flush(USUARIOS);
      http.expectOne('/api/consolidados').flush(lista);
      responderUnidades(http);
      await estabilizar(fixture);
      return fixture;
    };

    it('cada unidad muestra solo sus documentos y queda abierta en el menú', async () => {
      const otra = { ...LISTA[0], id: 'CON-7', numero: 'PLAN-20260007', unidadId: 'dispensas', plantillas: [plantillaResumen('PRO-7')] };
      const fixture = await abrir('a.perez', 'dispensas', [...LISTA, otra]);
      const elemento: HTMLElement = fixture.nativeElement;

      const filas = Array.from(elemento.querySelectorAll('tbody tr')).map((f) => f.textContent ?? '');
      expect(filas).toHaveLength(1);
      expect(filas[0]).toContain('PLAN-20260007');
      expect(elemento.querySelector('.unidad-actual')?.textContent).toBe('Dispensas');
      expect(TestBed.inject(UnidadService).actual()).toBe('dispensas');
    });

    it('muestra una fila por plantilla con su identidad y el botón para modificarla a la izquierda', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;

      expect(Array.from(elemento.querySelectorAll('thead th')).map((t) => t.textContent?.trim())).toEqual([
        'Acción', 'Nro Documento', 'Rubro', 'Sub Rubro', 'Tipo Plantilla', 'Tipo Documento', 'Estado',
      ]);
      const filas = Array.from(elemento.querySelectorAll('tbody tr'));
      expect(filas).toHaveLength(3);
      const primera = filas[0].textContent?.replace(/\s+/g, ' ') ?? '';
      expect(primera).toContain('PLAN-20260002');
      expect(primera).toContain('POR TRATADOS Y CONVENIOS');
      expect(primera).toContain('FONDOS BILATERALES');
      expect(primera).toContain('Resolución');
      expect(primera).toContain('Original');
      // El analista modifica sus plantillas EN REGISTRO; las aprobadas solo se consultan.
      const accion = (fila: Element) => fila.querySelector('.celda-accion a')!;
      expect(accion(filas[0]).textContent?.trim()).toBe('Modificar');
      expect(accion(filas[0]).getAttribute('href')).toBe('/consolidados/CON-2/propuestas/PRO-1');
      expect(accion(filas[2]).textContent?.trim()).toBe('Ver');
    });

    it('filtra por Rubro, Sub Rubro, Tipo Plantilla y Tipo Documento', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;
      const filas = () => Array.from(elemento.querySelectorAll('tbody tr')).map((f) => f.textContent?.replace(/\s+/g, ' ') ?? '');
      const filtro = (indice: number, valor: string) => {
        const lista = elemento.querySelectorAll<HTMLSelectElement>('.filtros select')[indice];
        lista.value = valor;
        lista.dispatchEvent(new Event('change'));
        fixture.detectChanges();
      };

      expect(Array.from(elemento.querySelectorAll('.filtros label > span')).map((s) => s.textContent)).toEqual([
        'Rubro', 'Sub Rubro', 'Tipo Plantilla', 'Tipo Documento',
      ]);
      // El sub rubro espera al rubro.
      expect(elemento.querySelectorAll<HTMLSelectElement>('.filtros select')[1].disabled).toBe(true);

      filtro(2, 'TDO-004');
      expect(filas()).toHaveLength(1);
      expect(filas()[0]).toContain('Certificación');

      filtro(2, '');
      filtro(0, '1800');
      filtro(1, '1801');
      filtro(3, 'CDO-002');
      expect(filas()).toHaveLength(1);
      expect(filas()[0]).toContain('PLAN-20260001');

      botones(elemento, 'Limpiar filtros')[0].click();
      fixture.detectChanges();
      expect(filas()).toHaveLength(3);
    });

    it('pagina las filas y permite elegir cuántas mostrar', async () => {
      const muchas = Array.from({ length: 12 }, (_, i) => ({
        ...LISTA[1], id: `CON-${10 + i}`, numero: `PLAN-202600${10 + i}`, plantillas: [plantillaResumen(`PRO-${10 + i}`)],
      }));
      const fixture = await abrir('a.perez', undefined, muchas);
      const elemento: HTMLElement = fixture.nativeElement;
      const filas = () => elemento.querySelectorAll('tbody tr').length;

      expect(filas()).toBe(10);
      expect(Array.from(elemento.querySelectorAll('.paginacion button')).map((b) => b.textContent?.trim())).toEqual(['«', '1', '2', '»']);
      expect(elemento.querySelector('.rango')?.textContent).toBe('1–10 de 12');

      botones(elemento, '2')[0].click();
      fixture.detectChanges();
      expect(filas()).toBe(2);
      expect(elemento.querySelector('.rango')?.textContent).toBe('11–12 de 12');

      const porPagina = elemento.querySelector<HTMLSelectElement>('.por-pagina select')!;
      porPagina.value = '25';
      porPagina.dispatchEvent(new Event('change'));
      fixture.detectChanges();
      expect(filas()).toBe(12);
    });

    it('el analista crea un documento con «+ Nueva Plantilla» y se abre ahí mismo la creación de la plantilla', async () => {
      const fixture = await abrir('a.perez');
      const navegar = vi.spyOn(TestBed.inject(Router), 'navigate');

      expect(fixture.nativeElement.querySelector('h2')?.textContent).toBe('Documentos Plantillas');
      botones(fixture.nativeElement, 'Nueva Plantilla')[0].click();
      const creacion = http.expectOne('/api/consolidados');
      expect(creacion.request.method).toBe('POST');
      // El documento se crea en la unidad abierta.
      expect(creacion.request.body).toEqual({ unidadId: 'exoneraciones' });
      creacion.flush({ ...DETALLE.consolidado, id: 'CON-9', numero: 'PLAN-20260009' });
      await estabilizar(fixture);
      // La lista se vuelve a consultar: el documento nuevo aparece (con «Agregar» mientras no tenga plantillas).
      http.expectOne('/api/consolidados').flush([
        ...LISTA, { ...LISTA[0], id: 'CON-9', numero: 'PLAN-20260009', plantillas: [], totalPlantillas: 0, tiposDocumento: [] },
      ]);
      for (const url of ['/api/plantillas-vigentes', '/api/rubros', '/api/clases-documento', '/api/tipos-documento']) {
        http.match(url).forEach((p) => p.flush([]));
      }
      await estabilizar(fixture);

      // No se abre otra pantalla: el diálogo de crear (desde cero o clonando) se abre sobre la lista.
      expect(navegar).not.toHaveBeenCalled();
      const dialogo = fixture.nativeElement.querySelector('app-seleccion-plantilla dialog') as HTMLDialogElement;
      expect(dialogo.hasAttribute('open')).toBe(true);
      expect(dialogo.querySelector('[role="tab"][aria-selected="true"]')?.textContent).toContain('CREAR');
      const vacia = Array.from(fixture.nativeElement.querySelectorAll('tbody tr') as NodeListOf<HTMLElement>).find((f) =>
        f.textContent?.includes('PLAN-20260009'),
      )!;
      expect(vacia.querySelector('.celda-accion button')?.textContent?.trim()).toBe('Agregar');
      // El número del documento ya no abre otra pantalla.
      expect(vacia.querySelector('.numero-plan')?.tagName).toBe('SPAN');
    });

    it('el analista elimina su documento EN REGISTRO tras confirmar: pasa a ELIMINADO y se vuelve a consultar la lista', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;
      const filas = Array.from(elemento.querySelectorAll('tbody tr'));
      const eliminar = (fila: Element) => fila.querySelector<HTMLButtonElement>('.celda-accion .boton-eliminar');

      // Solo EN REGISTRO: el aprobado no se elimina.
      expect(eliminar(filas[0])?.textContent?.trim()).toBe('Eliminar');
      expect(eliminar(filas[2])).toBeNull();

      eliminar(filas[0])!.click();
      await estabilizar(fixture);
      const interaccion = TestBed.inject(InteraccionService);
      expect(interaccion.confirmacion()?.titulo).toBe('Eliminar PLAN-20260002');
      expect(interaccion.confirmacion()?.mensaje).toContain('con sus 2 plantillas pasará al estado ELIMINADO');
      interaccion.responder(true);
      await estabilizar(fixture);

      const peticion = http.expectOne('/api/consolidados/CON-2/eliminar');
      expect(peticion.request.method).toBe('POST');
      peticion.flush({ ...DETALLE.consolidado, estado: 'Eliminado' });
      await estabilizar(fixture);
      http.expectOne('/api/consolidados').flush([LISTA[1]]);
      await estabilizar(fixture);
      expect(elemento.querySelectorAll('tbody tr')).toHaveLength(1);
      expect(interaccion.avisos().map((x) => x.texto)).toContain('PLAN-20260002 quedó ELIMINADO.');
    });

    it('sin confirmar no se elimina', async () => {
      const fixture = await abrir('a.perez');
      const elemento: HTMLElement = fixture.nativeElement;
      elemento.querySelector<HTMLButtonElement>('.boton-eliminar')!.click();
      await estabilizar(fixture);
      TestBed.inject(InteraccionService).responder(false);
      await estabilizar(fixture);
      http.expectNone('/api/consolidados/CON-2/eliminar');
    });

    it('el Oficial Jurídico no elimina documentos', async () => {
      const fixture = await abrir('j.ramos');
      expect(fixture.nativeElement.querySelector('.boton-eliminar')).toBeNull();
    });

    it('el Oficial Jurídico no crea consolidados', async () => {
      const fixture = await abrir('j.ramos');

      expect(botones(fixture.nativeElement, 'Nueva Plantilla')).toHaveLength(0);
      expect(fixture.nativeElement.textContent).toContain('pendientes de revisión jurídica');
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
      expect(modificar.request.body).toEqual({ plantillaId: 'PLA-001', nombreReferencia: null });
      modificar.flush([propuesta('PRO-1'), propuesta('PRO-2')]);
      await estabilizar(fixture);
      http.expectOne('/api/plantillas-vigentes').flush([]);

      expect(agregadas[0].map((p) => p.id)).toEqual(['PRO-1', 'PRO-2']);
    });
    describe('CREAR desde cero o clonando', () => {
      const VIGENTE = {
        plantillaId: 'PLA-001', nombre: 'Resolución vigente', rubroId: '1100', subRubroId: '1107', tipoDocumentoId: 'TDO-001',
        tipoDocumento: 'Resolución', claseDocumentoId: 'CDO-001', versionId: 'VER-1', numeroVersion: 3, fechaPublicacion: '2026-10-01T00:00:00Z',
      };
      const responder = () => {
        http.match('/api/tipos-documento').forEach((p) => p.flush([{ id: 'TDO-001', version: 1, nombre: 'Resolución', activo: true }]));
        http.match('/api/plantillas-vigentes').forEach((p) => p.flush([VIGENTE]));
        http.match('/api/rubros').forEach((p) => p.flush([{ id: '1100', version: 1, nombre: 'Rubro 1100', activo: true }]));
        http.match('/api/clases-documento').forEach((p) =>
          p.flush([
            { id: 'CDO-001', version: 1, nombre: 'Original', activo: true },
            { id: 'CDO-002', version: 1, nombre: 'Modificación', activo: true },
          ]),
        );
        http.match((r) => r.url === '/api/subrubros').forEach((p) =>
          p.flush([{ id: '1107', version: 1, rubroId: '1100', nombre: 'Sub rubro 1107', activo: true }]),
        );
      };
      const abrirCrear = async () => {
        const fixture = TestBed.createComponent(SeleccionPlantilla);
        fixture.componentRef.setInput('consolidadoId', 'CON-2');
        fixture.componentRef.setInput('operacion', 'Crear');
        fixture.detectChanges();
        await estabilizar(fixture);
        responder();
        await estabilizar(fixture);
        return fixture;
      };
      const elegir = async (fixture: ComponentFixture<SeleccionPlantilla>, id: string, valor: string) => {
        const lista = fixture.nativeElement.querySelector(`#${id}`) as HTMLSelectElement;
        lista.value = valor;
        lista.dispatchEvent(new Event('change'));
        await estabilizar(fixture);
        responder();
        await estabilizar(fixture);
      };
      const combinacion = async (fixture: ComponentFixture<SeleccionPlantilla>, prefijo: string, clase: string) => {
        await elegir(fixture, `${prefijo}-rubro`, '1100');
        await elegir(fixture, `${prefijo}-subrubro`, '1107');
        await elegir(fixture, `${prefijo}-tipo`, 'TDO-001');
        await elegir(fixture, `${prefijo}-documento`, clase);
      };

      it('desde cero envía la combinación sin plantilla de origen', async () => {
        const fixture = await abrirCrear();
        expect(fixture.nativeElement.querySelector('app-selector-origen')).toBeNull();

        await combinacion(fixture, 'lista', 'CDO-002');
        // El «Nombre Plantilla» se escribe aquí, al crearla.
        const nombre = fixture.nativeElement.querySelector('.campo-nombre input') as HTMLInputElement;
        expect(fixture.nativeElement.querySelector('.campo-nombre span')?.textContent).toBe('Nombre Plantilla');
        nombre.value = '  Exoneración a docentes 2026 ';
        nombre.dispatchEvent(new Event('input'));
        botones(fixture.nativeElement, 'Crear plantilla')[0].click();

        const crear = http.expectOne('/api/consolidados/CON-2/propuestas/crear');
        expect(crear.request.body).toEqual({
          rubroId: '1100', subRubroId: '1107', tipoDocumentoId: 'TDO-001', claseDocumentoId: 'CDO-002', plantillaOrigenId: null,
          nombreReferencia: 'Exoneración a docentes 2026',
        });
        crear.flush([propuesta('PRO-9')]);
        await estabilizar(fixture);
        responder();
      });

      it('al clonar elige la plantilla de origen con las mismas cuatro listas', async () => {
        const fixture = await abrirCrear();
        const elemento: HTMLElement = fixture.nativeElement;
        elemento.querySelector<HTMLInputElement>('input[value="Clonar"]')!.click();
        await estabilizar(fixture);
        responder();
        await estabilizar(fixture);

        expect(elemento.querySelector('app-selector-origen')).not.toBeNull();
        await combinacion(fixture, 'lista', 'CDO-002');
        // Sin plantilla de origen todavía no se puede crear.
        expect(botones(elemento, 'Crear como copia')[0].disabled).toBe(true);

        await combinacion(fixture, 'origen', 'CDO-002');
        expect(elemento.textContent).toContain('No hay una plantilla vigente con esa combinación');
        expect(botones(elemento, 'Crear como copia')[0].disabled).toBe(true);

        await elegir(fixture, 'origen-documento', 'CDO-001');
        expect(elemento.textContent).toContain('Se copiará Resolución vigente');
        expect(elemento.textContent).toContain('versión vigente 3');
        botones(elemento, 'Crear como copia')[0].click();

        const crear = http.expectOne('/api/consolidados/CON-2/propuestas/crear');
        expect(crear.request.body).toEqual({
          rubroId: '1100', subRubroId: '1107', tipoDocumentoId: 'TDO-001', claseDocumentoId: 'CDO-002', plantillaOrigenId: 'PLA-001',
          nombreReferencia: null,
        });
        crear.flush([propuesta('PRO-9')]);
        await estabilizar(fixture);
        responder();
      });
    });
  });
});
