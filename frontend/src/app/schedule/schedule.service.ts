import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ScheduleDay {
  date: string;
  isAvailable: boolean;
  reservationCount: number;
  reservedTimes: string[];
}

export interface ScheduleMonth {
  month: string;
  dailyCapacity: number;
  days: ScheduleDay[];
}

export interface ReservationDetails {
  date: string;
  time: string;
  serviceType: 'Home' | 'Workshop';
  city: string;
  locationAddress: string;
}

@Injectable({ providedIn: 'root' })
export class ScheduleService {
  private readonly endpoint = 'http://localhost:5075/api/schedule';

  constructor(private readonly http: HttpClient) {}

  getMonth(month: string): Promise<ScheduleMonth> {
    return firstValueFrom(
      this.http.get<ScheduleMonth>(this.endpoint, { params: { month } }),
    );
  }

  setAvailable(date: string): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.endpoint}/availability`, { date }, { withCredentials: true }),
    );
  }

  setUnavailable(date: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>(`${this.endpoint}/availability/${date}`, { withCredentials: true }),
    );
  }

  reserve(details: ReservationDetails): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.endpoint}/reservations`, details, { withCredentials: true }),
    );
  }
}
