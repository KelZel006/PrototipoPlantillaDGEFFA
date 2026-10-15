import { TestBed } from '@angular/core/testing';
import { ContextoService } from './contexto.service';
import { Rubro, SubRubro, TipoDocumento } from './modelos';

const rubroA: Rubro = { id: 'RUB-001', version: 1, nombre: 'Rubro A', activo: true };
const rubroB: Rubro = { id: 'RUB-002', version: 1, nombre: 'Rubro B', activo: true };
const subDeA: SubRubro = { id: 'SUB-001', version: 1, rubroId: 'RUB-001', nombre: 'Sub A', activo: true };
const subDeB: SubRubro = { id: 'SUB-003', version: 1, rubroId: 'RUB-002', nombre: 'Sub B', activo: true };
const tipo: TipoDocumento = { id: 'TDO-001', version: 1, nombre: 'Tipo', activo: true };

describe('ContextoService', () => {
  let contexto: ContextoService;

  beforeEach(() => {
    contexto = TestBed.inject(ContextoService);
  });

  it('está completo solo con las tres listas seleccionadas', () => {
    contexto.seleccionarRubro(rubroA);
    contexto.seleccionarSubRubro(subDeA);
    expect(contexto.completo()).toBe(false);

    contexto.seleccionarTipoDocumento(tipo);
    expect(contexto.completo()).toBe(true);
  });

  it('descarta el sub rubro al cambiar de rubro', () => {
    contexto.seleccionarRubro(rubroA);
    contexto.seleccionarSubRubro(subDeA);
    contexto.seleccionarTipoDocumento(tipo);

    contexto.seleccionarRubro(rubroB);

    expect(contexto.subRubro()).toBeNull();
    expect(contexto.tipoDocumento()).toEqual(tipo);
    expect(contexto.completo()).toBe(false);
  });

  it('conserva el sub rubro si se vuelve a elegir el mismo rubro', () => {
    contexto.seleccionarRubro(rubroA);
    contexto.seleccionarSubRubro(subDeA);

    contexto.seleccionarRubro({ ...rubroA });

    expect(contexto.subRubro()).toEqual(subDeA);
  });

  it('lleva los cambios sin guardar por sección', () => {
    contexto.marcarCambios('Cabecera', true);
    contexto.marcarCambios('Considerando', true);
    contexto.marcarCambios('Cabecera', false);
    expect([...contexto.seccionesConCambios()]).toEqual(['Considerando']);
    expect(contexto.cambiosSinGuardar()).toBe(true);

    contexto.descartarCambios();
    expect(contexto.cambiosSinGuardar()).toBe(false);
  });

  it('rechaza un sub rubro de otro rubro', () => {
    contexto.seleccionarRubro(rubroA);
    contexto.seleccionarSubRubro(subDeB);
    expect(contexto.subRubro()).toBeNull();
  });
});
