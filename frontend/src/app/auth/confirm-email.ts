import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  imports: [RouterLink],
  selector: 'app-confirm-email',
  templateUrl: './confirm-email.html',
})
export class ConfirmEmail implements OnInit {
  protected readonly message = signal('Confirming your email...');
  protected readonly confirmed = signal(false);

  constructor(
    private readonly route: ActivatedRoute,
    private readonly auth: AuthService,
  ) {}

  ngOnInit(): void {
    void this.confirm();
  }

  private async confirm(): Promise<void> {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!userId || !token) {
      this.message.set('This confirmation link is incomplete or invalid.');
      return;
    }

    try {
      this.message.set(await this.auth.confirmEmail(userId, token));
      this.confirmed.set(true);
    } catch (error: unknown) {
      this.message.set(
        error instanceof HttpErrorResponse && typeof error.error?.message === 'string'
          ? error.error.message
          : 'This confirmation link is invalid or has expired.',
      );
    }
  }
}
