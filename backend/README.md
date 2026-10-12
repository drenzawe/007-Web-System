# Backend setup

The backend uses SQLite and creates or upgrades its tables when it starts. Back up `app.db` before deploying a version that changes the reservation schema.

## Configure the first administrator

Initialize .NET user-secrets once:

```powershell
dotnet user-secrets init --project backend\backend.csproj
dotnet user-secrets set "AdminBootstrap:Email" "admin@example.com" --project backend\backend.csproj
dotnet user-secrets set "AdminBootstrap:Password" "<choose-a-unique-strong-password>" --project backend\backend.csproj
```

The first configured admin is created only if the database has no admin accounts. Never use a default password or commit secrets. Additional admins can be created in `/admin`.

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

The verification link defaults to `http://localhost:4200`. Set `Frontend:BaseUrl` when the frontend uses a different origin.

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
