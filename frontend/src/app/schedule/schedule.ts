import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import {
  ReservationDetails,
  ScheduleDay,
  ScheduleMonth,
  ScheduleService,
} from './schedule.service';

@Component({
  imports: [FormsModule, RouterLink],
  selector: 'app-schedule',
  templateUrl: './schedule.html',
})
export class Schedule implements OnInit {
  protected readonly month = signal(this.currentMonth());
  protected readonly calendar = signal<ScheduleMonth | null>(null);
  protected readonly selectedDate = signal<string | null>(null);
  protected readonly selectedTime = signal('');
  protected readonly city = signal('');
  protected readonly locationAddress = signal('');
  protected readonly serviceType = signal<'Home' | 'Workshop'>('Workshop');
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly timeSlots = Array.from({ length: 8 }, (_, index) => {
    const hour = index + 9;
    return {
      value: `${String(hour).padStart(2, '0')}:00`,
      label: `${hour % 12 || 12}:00 ${hour < 12 ? 'AM' : 'PM'}`,
    };
  });
  protected readonly monthLabel = computed(() => {
    const [year, month] = this.month().split('-').map(Number);
    return new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric' }).format(
      new Date(year, month - 1, 1),
    );
  });
  protected readonly calendarCells = computed(() => {
    const calendar = this.calendar();
    if (!calendar) {
      return [];
    }

    const [year, month] = calendar.month.split('-').map(Number);
    const firstDayOffset = new Date(year, month - 1, 1).getDay();
    return [
      ...Array<null>(firstDayOffset).fill(null),
      ...calendar.days,
    ];
  });
  protected readonly selectedDay = computed(
    () => this.calendar()?.days.find((day) => day.date === this.selectedDate()) ?? null,
  );

  private requestId = 0;

  constructor(
    private readonly scheduleService: ScheduleService,
    protected readonly auth: AuthService,
  ) {}

  ngOnInit(): void {
    if (!this.auth.user()) {
      void this.auth.loadSession().catch(() => {
        this.errorMessage.set('Your sign-in status could not be checked. Please reload and try again.');
      });
    }
    void this.loadMonth();
  }

  protected async loadMonth(): Promise<void> {
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.errorMessage.set('');
    try {
      const result = await this.scheduleService.getMonth(this.month());
      if (requestId === this.requestId) {
        this.calendar.set(result);
      }
    } catch (error: unknown) {
      if (requestId === this.requestId) {
        this.errorMessage.set(this.describeError(error));
      }
    } finally {
      if (requestId === this.requestId) {
        this.loading.set(false);
      }
    }
  }

  protected changeMonth(offset: number): void {
    const [year, month] = this.month().split('-').map(Number);
    const newMonth = new Date(year, month - 1 + offset, 1);
    this.month.set(
      `${newMonth.getFullYear()}-${String(newMonth.getMonth() + 1).padStart(2, '0')}`,
    );
    this.selectedDate.set(null);
    this.selectedTime.set('');
    this.successMessage.set('');
    void this.loadMonth();
  }

  protected selectDate(day: ScheduleDay): void {
    if (!this.isUnselectable(day.date)) {
      this.selectedDate.set(day.date);
      this.selectedTime.set('');
      this.errorMessage.set('');
      this.successMessage.set('');
    }
  }

  protected dayStatus(day: ScheduleDay): 'unavailable' | 'open' | 'one' | 'almost-full' | 'full' {
    if (this.isUnselectable(day.date) || !day.isAvailable) {
      return 'unavailable';
    }
    if (day.reservationCount >= (this.calendar()?.dailyCapacity ?? 3)) {
      return 'full';
    }
    if (day.reservationCount === 2) {
      return 'almost-full';
    }
    if (day.reservationCount === 1) {
      return 'one';
    }
    return 'open';
  }

  protected isUnselectable(date: string): boolean {
    return date <= this.currentDate();
  }

  protected async toggleAvailability(): Promise<void> {
    const day = this.selectedDay();
    if (!day || this.loading()) {
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');
    try {
      if (day.isAvailable) {
        await this.scheduleService.setUnavailable(day.date);
        this.successMessage.set('Date marked unavailable.');
      } else {
        await this.scheduleService.setAvailable(day.date);
        this.successMessage.set('Date is now open for reservations.');
      }
      await this.loadMonth();
    } catch (error: unknown) {
      this.errorMessage.set(this.describeError(error));
    }
  }

  protected async submitReservation(): Promise<void> {
    const day = this.selectedDay();
    if (
      !day ||
      !day.isAvailable ||
      this.dayStatus(day) === 'full' ||
      !this.auth.user() ||
      !this.selectedTime() ||
      day.reservedTimes.includes(this.selectedTime()) ||
      this.submitting()
    ) {
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    const details: ReservationDetails = {
      date: day.date,
      time: this.selectedTime(),
      serviceType: this.serviceType(),
      city: this.city().trim(),
      locationAddress: this.locationAddress().trim(),
    };

    try {
      await this.scheduleService.reserve(details);
      this.city.set('');
      this.locationAddress.set('');
      this.selectedTime.set('');
      this.successMessage.set('Your reservation has been received.');
      await this.loadMonth();
    } catch (error: unknown) {
      this.errorMessage.set(this.describeError(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private currentMonth(): string {
    const [year, month] = this.currentDate().split('-');
    return `${year}-${month}`;
  }

  private currentDate(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const message = error.error?.message ?? error.error?.title;
      return typeof message === 'string'
        ? message
        : `The schedule request failed${error.status ? ` (${error.status})` : ''}. Please try again.`;
    }
    return 'An unexpected error occurred while updating the schedule. Please try again.';
  }
}
