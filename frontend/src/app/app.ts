import { Component, inject } from '@angular/core';
import { Avisos } from './compartido/avisos';
import { DialogoConfirmacion } from './compartido/dialogo-confirmacion';
import { ContextoService } from './core/contexto.service';
import { PlantillaActualService } from './core/plantilla-actual.service';
import { EstadoDatos } from './editor/estado-datos/estado-datos';
import { PanelPlantilla } from './editor/panel-plantilla/panel-plantilla';
import { PestanasSecciones } from './editor/pestanas-secciones/pestanas-secciones';
import { SelectorContexto } from './editor/selector-contexto/selector-contexto';

@Component({
  selector: 'app-root',
  imports: [SelectorContexto, PanelPlantilla, PestanasSecciones, EstadoDatos, DialogoConfirmacion, Avisos],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly plantillaActual = inject(PlantillaActualService);
  protected readonly contexto = inject(ContextoService);
}
