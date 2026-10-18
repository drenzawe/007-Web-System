import { environment } from '../environments/environment';

export function apiUrl(path: string): string {
  const baseUrl = environment.apiBaseUrl.replace(/\/+$/, '');

  if (environment.production) {
    if (baseUrl.startsWith('//')) {
      throw new Error('The production API URL must be same-origin or use HTTPS.');
    }

    const resolvedBaseUrl = new URL(baseUrl, window.location.origin);
    if (resolvedBaseUrl.protocol !== 'https:') {
      throw new Error('The production API URL must use HTTPS.');
    }
  }

  return `${baseUrl}/${path.replace(/^\/+/, '')}`;
}
