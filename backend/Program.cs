using System.Threading.RateLimiting;
using backend;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200", "http://127.0.0.1:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "007.Retrofit.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

var authentication = builder.Services.AddAuthentication();
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle("Google", options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClaimActions.MapJsonKey("email_verified", "verified_email", "boolean");
    });
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    authentication.AddFacebook("Facebook", options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Fields.Add("email");
        options.Fields.Add("name");
        options.SaveTokens = false;
    });
}

builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitializeAsync(database);

    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roles.RoleExistsAsync(AppRoles.Customer))
    {
        var result = await roles.CreateAsync(new IdentityRole(AppRoles.Customer));
        EnsureIdentitySuccess(result, "Could not create the customer role.");
    }

    if (!await roles.RoleExistsAsync(AppRoles.Admin))
    {
        var result = await roles.CreateAsync(new IdentityRole(AppRoles.Admin));
        EnsureIdentitySuccess(result, "Could not create the admin role.");
    }

    var adminEmail = builder.Configuration["AdminBootstrap:Email"]?.Trim();
    var adminPassword = builder.Configuration["AdminBootstrap:Password"];
    if (string.IsNullOrEmpty(adminEmail) != string.IsNullOrEmpty(adminPassword))
    {
        throw new InvalidOperationException(
            "Configure both AdminBootstrap:Email and AdminBootstrap:Password in .NET user-secrets or environment variables.");
    }

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingAdmin = await users.GetUsersInRoleAsync(AppRoles.Admin);
        if (existingAdmin.Count == 0)
        {
            var admin = await users.FindByEmailAsync(adminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    DisplayName = "Administrator"
                };
                var created = await users.CreateAsync(admin, adminPassword);
                EnsureIdentitySuccess(created, "Could not create the configured bootstrap admin.");
            }

            var assigned = await users.AddToRoleAsync(admin, AppRoles.Admin);
            EnsureIdentitySuccess(assigned, "Could not assign the configured bootstrap admin role.");
        }
    }
}

app.UseCors("AllowAngular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void EnsureIdentitySuccess(IdentityResult result, string message)
{
    if (!result.Succeeded)
    {
        var details = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message} {details}");
    }
}
