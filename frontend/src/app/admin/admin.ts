import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminAccount, AdminReservation, AdminService, AdminStatistics } from './admin.service';
import { ScheduleDay, ScheduleMonth, ScheduleService } from '../schedule/schedule.service';

@Component({
  imports: [FormsModule],
  selector: 'app-admin-panel',
  templateUrl: './admin.html',
})
export class AdminPanel implements OnInit {
  protected readonly statusFilters = ['Pending', 'Approved', 'Rejected', 'All'] as const;
  protected readonly month = signal(this.currentMonth());
  protected readonly calendar = signal<ScheduleMonth | null>(null);
  protected readonly reservations = signal<AdminReservation[]>([]);
  protected readonly statistics = signal<AdminStatistics | null>(null);
  protected readonly statisticsPeriod = signal<'month' | 'year'>('month');
  protected readonly statisticsMonth = signal(this.currentMonth());
  protected readonly statisticsYear = signal(this.currentDate().slice(0, 4));
  protected readonly admins = signal<AdminAccount[]>([]);
  protected readonly section = signal<'requests' | 'availability' | 'admins'>('requests');
  protected readonly statusFilter = signal<'All' | 'Pending' | 'Approved' | 'Rejected'>('Pending');
  protected readonly selectedDate = signal<string | null>(null);
  protected readonly newAdminName = signal('');
  protected readonly newAdminEmail = signal('');
  protected readonly newAdminPassword = signal('');
  protected readonly travelFees = signal<Record<number, number>>({});
  protected readonly rejectionReasons = signal<Record<number, string>>({});
  protected readonly loading = signal(false);
  protected readonly saving = signal<number | null>(null);
  protected readonly error = signal('');
  protected readonly notice = signal('');

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
    return [
      ...Array<null>(new Date(year, month - 1, 1).getDay()).fill(null),
      ...calendar.days,
    ];
  });
  protected readonly selectedDay = computed(
    () => this.calendar()?.days.find((day) => day.date === this.selectedDate()) ?? null,
  );
  protected readonly visibleReservations = computed(() =>
    this.statusFilter() === 'All'
      ? this.reservations()
      : this.reservations().filter((reservation) => reservation.status === this.statusFilter()),
  );

  constructor(
    private readonly adminService: AdminService,
    private readonly scheduleService: ScheduleService,
  ) {}

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [calendar, reservations, admins, statistics] = await Promise.all([
        this.scheduleService.getMonth(this.month()),
        this.adminService.getReservations(this.month()),
        this.adminService.getAdmins(),
        this.adminService.getStatistics(
          this.statisticsPeriod(),
          this.statisticsPeriod() === 'month' ? this.statisticsMonth() : this.statisticsYear(),
        ),
      ]);
      this.calendar.set(calendar);
      this.reservations.set(reservations);
      this.admins.set(admins);
      this.statistics.set(statistics);
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    } finally {
      this.loading.set(false);
    }
  }

  protected async loadStatistics(): Promise<void> {
    try {
      const statistics = await this.adminService.getStatistics(
        this.statisticsPeriod(),
        this.statisticsPeriod() === 'month' ? this.statisticsMonth() : this.statisticsYear(),
      );
      this.statistics.set(statistics);
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    }
  }

  protected setStatisticsPeriod(period: 'month' | 'year'): void {
    this.statisticsPeriod.set(period);
    void this.loadStatistics();
  }

  protected changeMonth(offset: number): void {
    const [year, month] = this.month().split('-').map(Number);
    const updated = new Date(year, month - 1 + offset, 1);
    this.month.set(
      `${updated.getFullYear()}-${String(updated.getMonth() + 1).padStart(2, '0')}`,
    );
    this.selectedDate.set(null);
    void this.load();
  }

  protected dayIsPast(date: string): boolean {
    return date <= this.currentDate();
  }

  protected async toggleDate(day: ScheduleDay): Promise<void> {
    if (this.dayIsPast(day.date) || this.loading()) {
      return;
    }

    this.selectedDate.set(day.date);
    this.error.set('');
    this.notice.set('');
    try {
      if (day.isAvailable) {
        await this.scheduleService.setUnavailable(day.date);
        this.notice.set(`${day.date} is now unavailable.`);
      } else {
        await this.scheduleService.setAvailable(day.date);
        this.notice.set(`${day.date} is available for reservations.`);
      }
      await this.load();
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    }
  }

  protected async approve(reservation: AdminReservation): Promise<void> {
    this.saving.set(reservation.id);
    this.error.set('');
    this.notice.set('');
    try {
      const travelFee = reservation.serviceType === 'Home'
        ? this.travelFees()[reservation.id] ?? 0
        : 0;
      await this.adminService.approve(reservation.id, Math.round(travelFee * 100));
      this.notice.set('Reservation approved.');
      await this.load();
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    } finally {
      this.saving.set(null);
    }
  }

  protected setTravelFee(id: number, amount: number): void {
    this.travelFees.update(fees => ({ ...fees, [id]: Number(amount) }));
  }

  protected setRejectionReason(id: number, reason: string): void {
    this.rejectionReasons.update(reasons => ({ ...reasons, [id]: reason }));
  }

  protected async reject(reservation: AdminReservation): Promise<void> {
    const reason = this.rejectionReasons()[reservation.id]?.trim() ?? '';
    if (reason.length < 5) {
      this.error.set('Enter a rejection reason with at least five characters.');
      return;
    }

    this.saving.set(reservation.id);
    this.error.set('');
    this.notice.set('');
    try {
      await this.adminService.reject(reservation.id, reason);
      this.notice.set('Reservation rejected. Its time slot is available again.');
      await this.load();
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    } finally {
      this.saving.set(null);
    }
  }

  protected async createAdmin(): Promise<void> {
    this.error.set('');
    this.notice.set('');
    try {
      await this.adminService.createAdmin(
        this.newAdminName().trim(),
        this.newAdminEmail().trim(),
        this.newAdminPassword(),
      );
      this.newAdminName.set('');
      this.newAdminEmail.set('');
      this.newAdminPassword.set('');
      this.notice.set('Admin account created.');
      await this.load();
    } catch (error: unknown) {
      this.error.set(this.describeError(error));
    }
  }

  protected dayStatus(day: ScheduleDay): string {
    if (this.dayIsPast(day.date) || !day.isAvailable) {
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

  private currentMonth(): string {
    return this.currentDate().slice(0, 7);
  }

  private currentDate(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }

  private describeError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error?.message === 'string') {
        return error.error.message;
      }
      return error.status === 401 || error.status === 403
        ? 'Your admin session has expired. Sign in with an admin account and try again.'
        : `The admin request failed${error.status ? ` (${error.status})` : ''}. Please try again.`;
    }
    return 'An unexpected error occurred. Please try again.';
  }
}
