let siguiente = 0;

/** Identificador único en la página, para enlazar etiquetas y controles de componentes que se repiten. */
export function identificadorUnico(prefijo: string): string {
  return `${prefijo}-${++siguiente}`;
}
