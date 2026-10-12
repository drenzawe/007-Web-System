import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  imports: [FormsModule, RouterLink],
  selector: 'app-register',
  templateUrl: './register.html',
})
export class Register implements OnInit {
  protected readonly name = signal('');
  protected readonly email = signal('');
  protected readonly phone = signal('');
  protected readonly password = signal('');
  protected readonly message = signal('');
  protected readonly error = signal('');
  protected readonly submitting = signal(false);

  constructor(private readonly auth: AuthService) {}

  ngOnInit(): void {
    void this.auth.loadSocialProviders().catch(() => {
      this.error.set('Social sign-in options could not be loaded.');
    });
  }

  protected async submit(): Promise<void> {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.error.set('');
    this.message.set('');
    try {
      this.message.set(await this.auth.register({
        name: this.name().trim(),
        email: this.email().trim(),
        phone: this.phone().trim(),
        password: this.password(),
      }));
    } catch (error: unknown) {
      this.error.set(
        error instanceof HttpErrorResponse && typeof error.error?.message === 'string'
          ? error.error.message
          : 'Your account could not be created. Please check the details and try again.',
      );
    } finally {
      this.submitting.set(false);
    }
  }

  protected socialLoginUrl(provider: 'Google' | 'Facebook'): string {
    return this.auth.socialLoginUrl(provider, '/schedule');
  }
}
