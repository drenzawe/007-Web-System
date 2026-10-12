import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { Schedule } from './schedule';
import { AuthService } from '../auth/auth.service';

describe('Schedule', () => {
  let fixture: ComponentFixture<Schedule>;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Schedule],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal({ name: 'Test customer', email: 'test@example.com', isAdmin: false }),
            loadSession: () => Promise.resolve(null),
          },
        },
      ],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Schedule);
  });

  afterEach(() => httpTesting.verify());

  it('colors available dates by reservation count and unavailable dates gray', async () => {
    fixture.detectChanges();
    const request = httpTesting.expectOne(
      (req) => req.url === 'http://localhost:5075/api/schedule',
    );
    const dates = [0, 1, 2, 3, 4, 5].map((offset) => {
      const date = new Date();
      date.setDate(date.getDate() + offset);
      return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
    });
    const days = [
      { date: dates[0], isAvailable: true, reservationCount: 0, reservedTimes: [] },
      { date: dates[1], isAvailable: true, reservationCount: 0, reservedTimes: [] },
      { date: dates[2], isAvailable: true, reservationCount: 1, reservedTimes: ['09:00'] },
      { date: dates[3], isAvailable: true, reservationCount: 2, reservedTimes: [] },
      { date: dates[4], isAvailable: true, reservationCount: 3, reservedTimes: [] },
      { date: dates[5], isAvailable: false, reservationCount: 0, reservedTimes: [] },
    ];

    request.flush({
      month: request.request.params.get('month'),
      dailyCapacity: 3,
      days,
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const buttons = dates.map(
      (date) => fixture.nativeElement.querySelector(`button[aria-label^="${date}"]`) as HTMLButtonElement,
    );
    expect(buttons[0].disabled).toBe(true);
    expect(buttons[0].classList.contains('bg-slate-100')).toBe(true);
    expect(buttons[1].classList.contains('bg-white')).toBe(true);
    expect(buttons[2].classList.contains('bg-emerald-100')).toBe(true);
    expect(buttons[3].classList.contains('bg-amber-100')).toBe(true);
    expect(buttons[4].classList.contains('bg-red-100')).toBe(true);
    expect(buttons[5].classList.contains('bg-slate-100')).toBe(true);

    buttons[2].click();
    fixture.detectChanges();
    const bookedTime = fixture.nativeElement.querySelector(
      'option[value="09:00"]',
    ) as HTMLOptionElement;
    const openTime = fixture.nativeElement.querySelector(
      'option[value="10:00"]',
    ) as HTMLOptionElement;
    expect(bookedTime.disabled).toBe(true);
    expect(bookedTime.textContent).toContain('booked');
    expect(openTime.disabled).toBe(false);
  });

  it('does not allow selecting a past date', async () => {
    fixture.detectChanges();
    const request = httpTesting.expectOne(
      (req) => req.url === 'http://localhost:5075/api/schedule',
    );
    const date = (daysAgo: number) => {
      const value = new Date();
      value.setDate(value.getDate() - daysAgo);
      return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
    };
    const yesterday = date(1);

    request.flush({
      month: request.request.params.get('month'),
      dailyCapacity: 3,
      days: [
        { date: yesterday, isAvailable: true, reservationCount: 0, reservedTimes: [] },
      ],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const pastDateButton = fixture.nativeElement.querySelector(
      `button[aria-label^="${yesterday}"]`,
    ) as HTMLButtonElement;
    expect(pastDateButton.disabled).toBe(true);
    expect(pastDateButton.classList.contains('bg-slate-100')).toBe(true);
  });
});
