import { Component } from '@angular/core';
import { EstadoDatos } from './editor/estado-datos/estado-datos';
import { PanelPlantilla } from './editor/panel-plantilla/panel-plantilla';
import { SelectorContexto } from './editor/selector-contexto/selector-contexto';

@Component({
  selector: 'app-root',
  imports: [SelectorContexto, PanelPlantilla, EstadoDatos],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {}
