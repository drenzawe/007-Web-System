import { Component } from '@angular/core';
import { Navbar } from './navbar/navbar';
import { RouterOutlet } from '@angular/router';


@Component({
  standalone: true,
  imports: [
    Navbar,
    RouterOutlet
  ],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
}
