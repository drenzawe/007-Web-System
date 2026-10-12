import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  imports: [RouterLink],
  selector: 'app-navbar',
  styleUrl: './navbar.css',
  templateUrl: './navbar.html',
})
export class Navbar implements OnInit {
  protected readonly loginModalOpen = signal(false);
  protected readonly loginMessage = signal('');

  constructor(
    protected readonly auth: AuthService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    void Promise.all([this.auth.loadSession(), this.auth.loadSocialProviders()]).catch(() => {
      this.loginMessage.set('Could not check your sign-in status. Please refresh and try again.');
    });
  }

  protected openLoginModal(): void {
    this.loginMessage.set('');
    this.loginModalOpen.set(true);
  }

  protected closeLoginModal(): void {
    this.loginModalOpen.set(false);
  }

  protected async onLoginSubmit(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    const form = event.currentTarget as HTMLFormElement;
    const values = new FormData(form);
    try {
      const user = await this.auth.login(
        String(values.get('email') ?? ''),
        String(values.get('password') ?? ''),
        values.get('rememberMe') === 'on',
      );
      this.loginModalOpen.set(false);
      await this.router.navigateByUrl(user.isAdmin ? '/admin' : '/schedule');
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse) {
        this.loginMessage.set(
          typeof error.error?.message === 'string'
            ? error.error.message
            : 'Sign-in failed. Please check your connection and try again.',
        );
        return;
      }
      this.loginMessage.set('Sign-in failed. Please try again.');
    }
  }

  protected async logout(): Promise<void> {
    try {
      await this.auth.logout();
      await this.router.navigateByUrl('/');
    } catch (error: unknown) {
      this.loginMessage.set(
        error instanceof HttpErrorResponse
          ? 'Sign-out failed. Please check your connection and try again.'
          : 'Sign-out failed. Please try again.',
      );
    }
  }

  protected socialLoginUrl(provider: 'Google' | 'Facebook'): string {
    return this.auth.socialLoginUrl(provider, this.router.url);
  }
}
