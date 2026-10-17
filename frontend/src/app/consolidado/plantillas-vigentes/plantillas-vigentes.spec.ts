import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { provideRouter } from '@angular/router';
import { PlantillaConVersiones } from '../../core/modelos';
import { PlantillasVigentes } from './plantillas-vigentes';

registerLocaleData(localeEs);

const PLANTILLAS: PlantillaConVersiones[] = [
  {
    plantillaId: 'PLA-001', nombre: 'Aprobación de prueba', rubroId: '1100', subRubroId: '1107', tipoDocumentoId: 'TDO-001',
    tipoDocumento: 'Resolución', claseDocumentoId: 'CDO-001',
    versiones: [
      { id: 'VER-2', numeroVersion: 2, vigencia: 'Vigente', cargaInicial: false, publicadaPor: 'j.ramos', fechaPublicacion: '2026-10-17T10:00:00Z',
        inicioVigencia: '2026-10-17T10:00:00Z', consolidadoId: 'CON-2', consolidadoNumero: 'PLAN-20260002', versionAnteriorId: 'VER-1' },
      { id: 'VER-1', numeroVersion: 1, vigencia: 'NoVigente', cargaInicial: true, publicadaPor: 'carga.inicial', fechaPublicacion: '2026-10-16T10:00:00Z',
        inicioVigencia: '2026-10-16T10:00:00Z', finVigencia: '2026-10-17T10:00:00Z' },
    ],
  },
  {
    plantillaId: 'PLA-002', nombre: 'Dictamen de prueba', rubroId: '1800', subRubroId: '1801', tipoDocumentoId: 'TDO-002',
    tipoDocumento: 'Dictamen', claseDocumentoId: 'CDO-001',
    versiones: [{ id: 'VER-3', numeroVersion: 1, vigencia: 'Vigente', cargaInicial: true, publicadaPor: 'carga.inicial',
      fechaPublicacion: '2026-10-16T10:00:00Z', inicioVigencia: '2026-10-16T10:00:00Z' }],
  },
];

describe('PlantillasVigentes', () => {
  it('muestra la versión vigente, filtra por tipo y despliega las versiones anteriores con su origen', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: LOCALE_ID, useValue: 'es' }],
    });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PlantillasVigentes);
    fixture.detectChanges();
    TestBed.tick();
    http.match('/api/usuarios').forEach((p) => p.flush([]));
    http.expectOne('/api/consolidado-plantillas').flush(PLANTILLAS);
    await new Promise((resolver) => setTimeout(resolver));
    TestBed.tick();
    fixture.detectChanges();
    const elemento: HTMLElement = fixture.nativeElement;

    const filas = () => Array.from(elemento.querySelectorAll('tbody > tr')).map((f) => f.textContent?.replace(/\s+/g, ' ') ?? '');
    expect(filas()).toHaveLength(2);
    expect(filas()[0]).toContain('PLAN-20260002');
    expect(filas()[1]).toContain('Carga inicial');

    const tipo = elemento.querySelector<HTMLSelectElement>('.filtros select')!;
    tipo.value = 'Resolución';
    tipo.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(filas()).toHaveLength(1);

    Array.from(elemento.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent?.includes('Versiones (2)'))!.click();
    fixture.detectChanges();
    const versiones = Array.from(elemento.querySelectorAll('.versiones li')).map((v) => v.textContent?.replace(/\s+/g, ' ') ?? '');
    expect(versiones[0]).toContain('VIGENTE');
    expect(versiones[0]).toContain('publicada por PLAN-20260002');
    expect(versiones[1]).toContain('NO VIGENTE');
    expect(versiones[1]).toContain('carga inicial');
    http.verify();
  });
});
