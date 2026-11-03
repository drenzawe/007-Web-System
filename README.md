# Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.2.2.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## API configuration

Development builds send API requests to `http://localhost:5075/api`. Production builds use the same-origin `/api` path by default. If the backend is hosted on a separate origin, set `apiBaseUrl` in `src/environments/environment.production.ts` to its HTTPS API base URL and rebuild. Production configuration rejects insecure absolute API URLs.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.



## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.

# Backend setup

The backend uses SQLite and creates or upgrades its tables when it starts. Back up `app.db` before deploying a version that changes the reservation schema.

## Configure the first administrator

Initialize .NET user-secrets once:

```powershell
dotnet user-secrets init --project backend\backend.csproj
dotnet user-secrets set "AdminBootstrap:Email" "admin@example.com" --project backend\backend.csproj
dotnet user-secrets set "AdminBootstrap:Password" "<choose-a-unique-strong-password>" --project backend\backend.csproj
```

Passwords must be at least 8 characters. The first configured admin is created only if the database has no admin accounts. Use a unique password and never commit secrets. Additional admins can be created in `/admin`.

## Configure email verification

Customer email/password registration is unavailable until SMTP is configured. Store these values in .NET user-secrets or environment variables, not source control:

```powershell
dotnet user-secrets set "Email:From" "appointments@example.com" --project backend\backend.csproj
dotnet user-secrets set "Email:Smtp:Host" "<your-smtp-host>" --project backend\backend.csproj
dotnet user-secrets set "Email:Smtp:Port" "<your-smtp-port>" --project backend\backend.csproj
dotnet user-secrets set "Email:Smtp:Username" "<your-smtp-username>" --project backend\backend.csproj
dotnet user-secrets set "Email:Smtp:Password" "<your-smtp-password>" --project backend\backend.csproj
dotnet user-secrets set "Email:Smtp:EnableSsl" "true" --project backend\backend.csproj
```

The same SMTP settings send a notification to each administrator when a new booking is received and notify customers when a reservation is approved or rejected. Rejection emails include the admin's reason; approval emails include a home-service travel fee when applicable. Notification failures are logged and do not undo a saved booking decision. The verification link and admin link default to `http://localhost:4200`; set `Frontend:BaseUrl` when the frontend uses a different origin.

Each customer may have one pending or approved reservation per date. A rejected reservation does not prevent the customer from requesting that date again. Admins must provide a rejection reason, which is shown to the customer with their reservation status.

The admin dashboard reports reservation totals by status and distinct customers who made reservations for the selected month or year. Customer account creation dates are not stored, so these figures are not monthly or yearly registration counts.

## Optional Google and Facebook sign-in

Create OAuth applications with the callback URL `http://localhost:5075/api/auth/external-callback`, then set their credentials locally:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "<google-client-id>" --project backend\backend.csproj
dotnet user-secrets set "Authentication:Google:ClientSecret" "<google-client-secret>" --project backend\backend.csproj
dotnet user-secrets set "Authentication:Facebook:AppId" "<facebook-app-id>" --project backend\backend.csproj
dotnet user-secrets set "Authentication:Facebook:AppSecret" "<facebook-app-secret>" --project backend\backend.csproj
```

Only providers with both credentials configured are offered on the sign-in and registration pages.

## Run locally

From the repository root, start the backend and frontend in separate terminals:

```powershell
dotnet run --project backend\backend.csproj
```

```powershell
cd frontend
npm start
```

Reservations require a confirmed customer account. Pending requests hold a calendar time and daily capacity until approved or rejected. Admins see the customer's city, address, and contact details; public calendar responses contain availability and reservation counts only. Home-service fees are set by an admin when approving a request.

For deployment, configure HTTPS, production frontend origins, a durable SMTP service, OAuth redirect URIs, and secrets through the hosting provider's secret manager.
