import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SignedInUser {
  name: string;
  email: string;
  isAdmin: boolean;
}

export interface RegisterDetails {
  name: string;
  email: string;
  phone: string;
  password: string;
}

export interface SocialProviders {
  google: boolean;
  facebook: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly endpoint = 'http://localhost:5075/api/auth';
  readonly user = signal<SignedInUser | null>(null);
  readonly checkingSession = signal(false);
  readonly socialProviders = signal<SocialProviders>({ google: false, facebook: false });

  constructor(private readonly http: HttpClient) {}

  async loadSession(): Promise<SignedInUser | null> {
    this.checkingSession.set(true);
    try {
      const user = await firstValueFrom(
        this.http.get<SignedInUser>(`${this.endpoint}/me`, { withCredentials: true }),
      );
      this.user.set(user);
      return user;
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && [401, 403].includes(error.status)) {
        this.user.set(null);
        return null;
      }
      throw error;
    } finally {
      this.checkingSession.set(false);
    }
  }

  async login(email: string, password: string, rememberMe: boolean): Promise<SignedInUser> {
    const user = await firstValueFrom(
      this.http.post<SignedInUser>(
        `${this.endpoint}/login`,
        { email, password, rememberMe },
        { withCredentials: true },
      ),
    );
    this.user.set(user);
    return user;
  }

  async register(details: RegisterDetails): Promise<string> {
    const response = await firstValueFrom(
      this.http.post<{ message: string }>(`${this.endpoint}/register`, details, {
        withCredentials: true,
      }),
    );
    return response.message;
  }

  async confirmEmail(userId: string, token: string): Promise<string> {
    const response = await firstValueFrom(
      this.http.post<{ message: string }>(
        `${this.endpoint}/confirm-email`,
        { userId, token },
        { withCredentials: true },
      ),
    );
    return response.message;
  }

  async loadSocialProviders(): Promise<void> {
    const providers = await firstValueFrom(
      this.http.get<SocialProviders>(`${this.endpoint}/providers`),
    );
    this.socialProviders.set(providers);
  }

  async logout(): Promise<void> {
    await firstValueFrom(
      this.http.post<void>(`${this.endpoint}/logout`, {}, { withCredentials: true }),
    );
    this.user.set(null);
  }

  socialLoginUrl(provider: 'Google' | 'Facebook', returnUrl: string): string {
    const params = new URLSearchParams({ returnUrl });
    return `${this.endpoint}/external/${provider}?${params.toString()}`;
  }
}
