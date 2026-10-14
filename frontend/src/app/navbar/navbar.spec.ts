import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal, WritableSignal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { AuthService, SignedInUser } from '../auth/auth.service';
import { Navbar } from './navbar';

describe('Navbar', () => {
  let component: Navbar;
  let fixture: ComponentFixture<Navbar>;
  let user: WritableSignal<SignedInUser | null>;

  beforeEach(async () => {
    user = signal(null);
    await TestBed.configureTestingModule({
      imports: [Navbar],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user,
            loadSession: () => Promise.resolve(null),
            loadSocialProviders: () => Promise.resolve(),
            socialProviders: signal({ google: false, facebook: false }),
            login: () => Promise.reject(new HttpErrorResponse({
              error: { message: 'Confirm your email address before signing in.' },
              status: 401,
            })),
            logout: () => Promise.resolve(),
            socialLoginUrl: () => '',
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Navbar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('keeps Home and Services navigation links available', () => {
    const links = fixture.nativeElement.querySelectorAll(
      '.site-nav__links .site-nav__link',
    ) as NodeListOf<HTMLAnchorElement>;

    expect(Array.from(links).map((link) => link.textContent?.trim())).toEqual([
      'Home',
      'Services',
    ]);
  });

  it('welcomes a signed-in user by name', () => {
    user.set({ name: 'Drenz', email: 'drenz@example.com', isAdmin: true });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Welcome, Drenz!');
  });

  it('opens and closes the login dialog', () => {
    const loginButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    loginButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeTruthy();

    const closeButton = fixture.nativeElement.querySelector(
      '[aria-label="Close login dialog"]',
    ) as HTMLButtonElement;
    closeButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('shows the authentication error when sign-in is rejected', async () => {
    const loginButton = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    loginButton.click();
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain(
      'Confirm your email address',
    );
  });
});
