import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AdminReservation {
  id: number;
  date: string;
  time: string;
  name: string;
  email: string;
  city: string;
  address: string;
  serviceType: 'Home' | 'Workshop';
  travelFeeCents: number;
  status: 'Pending' | 'Approved' | 'Rejected';
  rejectionReason: string;
  createdAtUtc: string;
}

export interface AdminAccount {
  id: string;
  name: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly endpoint = 'http://localhost:5075/api/admin';

  constructor(private readonly http: HttpClient) {}

  getReservations(month: string): Promise<AdminReservation[]> {
    return firstValueFrom(
      this.http.get<AdminReservation[]>(`${this.endpoint}/reservations`, {
        params: { month },
        withCredentials: true,
      }),
    );
  }

  getAdmins(): Promise<AdminAccount[]> {
    return firstValueFrom(
      this.http.get<AdminAccount[]>(`${this.endpoint}/admins`, { withCredentials: true }),
    );
  }

  approve(id: number, travelFeeCents: number): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(
        `${this.endpoint}/reservations/${id}/approve`,
        { travelFeeCents },
        { withCredentials: true },
      ),
    );
  }

  reject(id: number, reason: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(
        `${this.endpoint}/reservations/${id}/reject`,
        { reason },
        { withCredentials: true },
      ),
    );
  }

  createAdmin(name: string, email: string, password: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(
        `${this.endpoint}/admins`,
        { name, email, password },
        { withCredentials: true },
      ),
    );
  }
}
