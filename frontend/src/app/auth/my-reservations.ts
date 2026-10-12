import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

interface CustomerReservation {
  id: number;
  date: string;
  time: string;
  serviceType: 'Home' | 'Workshop';
  status: 'Pending' | 'Approved' | 'Rejected';
  rejectionReason: string;
  travelFeeCents: number;
}

@Component({
  selector: 'app-my-reservations',
  templateUrl: './my-reservations.html',
})
export class MyReservations implements OnInit {
  protected readonly reservations = signal<CustomerReservation[]>([]);
  protected readonly error = signal('');

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    void this.loadReservations();
  }

  private async loadReservations(): Promise<void> {
    try {
      const reservations = await firstValueFrom(
        this.http.get<CustomerReservation[]>(
          'http://localhost:5075/api/auth/my-reservations',
          { withCredentials: true },
        ),
      );
      this.reservations.set(reservations);
    } catch (error: unknown) {
      this.error.set(
        error instanceof HttpErrorResponse
          ? 'Your reservations could not be loaded. Sign in again and try once more.'
          : 'Your reservations could not be loaded. Please try again.',
      );
    }
  }
}
