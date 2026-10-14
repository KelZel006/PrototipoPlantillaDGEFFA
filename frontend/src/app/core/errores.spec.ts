import { HttpErrorResponse } from '@angular/common/http';
import { mensajeError } from './errores';

describe('mensajeError', () => {
  it('explica cuando el servidor no responde', () => {
    const error = new HttpErrorResponse({ status: 0 });
    expect(mensajeError(error)).toContain('No se pudo conectar con el servidor');
  });

  it('une el título y los errores de validación del servidor', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'Los datos enviados no son válidos.',
        errors: { subRubroId: ['El sub rubro no pertenece al rubro seleccionado.'] },
      },
    });
    expect(mensajeError(error)).toBe(
      'Los datos enviados no son válidos. El sub rubro no pertenece al rubro seleccionado.',
    );
  });

  it('muestra el detalle de un conflicto de versión', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflicto con los datos guardados.', detail: 'Recargue los datos antes de guardar.' },
    });
    expect(mensajeError(error)).toBe('Conflicto con los datos guardados. Recargue los datos antes de guardar.');
  });

  it('usa un mensaje genérico para errores sin detalle', () => {
    expect(mensajeError(new HttpErrorResponse({ status: 502 }))).toBe('El servidor respondió con el error 502.');
    expect(mensajeError(new Error('x'))).toBe('Ocurrió un error inesperado.');
  });
});
