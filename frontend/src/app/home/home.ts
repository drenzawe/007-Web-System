import { Component } from '@angular/core';
import { Hero } from '../hero/hero';
import { Stays } from '../stays/stays';

@Component({
  imports: [Hero, Stays],
  selector: 'app-home',
  templateUrl: './home.html',
})
export class HomePage {}
