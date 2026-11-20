using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkoutAgentic.Api.Data;
using WorkoutAgentic.Api.Data.Entities;
using WorkoutAgentic.Api.Features.Legal;

namespace WorkoutAgentic.Api.Features.Auth;

public sealed record RegisterRequest(string Name, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AccountView(int StudentId, string Name, string Email, bool AgreementAccepted = false);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string Password);

internal static class AuthModule
{
    public static IServiceCollection AddStudentAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<CapturasDbContext>().AddDefaultTokenProviders();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "WorkoutAgentic.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnSigningIn = context =>
            {
                context.Properties.Items.TryAdd("absolute-expiry", DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds().ToString());
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                if (!context.Properties.Items.TryGetValue("absolute-expiry", out var value)
                    || !long.TryParse(value, out var expiry) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiry)
                {
                    context.RejectPrincipal();
                    return;
                }
                await SecurityStampValidator.ValidatePrincipalAsync(context);
            };
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        // Validate on every request so logout/password reset revoke old cookies.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        var database = new SqliteConnectionStringBuilder(configuration.GetConnectionString("Sqlite")).DataSource;
        var keys = configuration["Auth:DataProtectionKeysPath"] ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(database))!, "auth-keys");
        Directory.CreateDirectory(keys);
        services.AddDataProtection().SetApplicationName("WorkoutAgentic").PersistKeysToFileSystem(new DirectoryInfo(keys));
        services.AddScoped<StudentAccessFilter>();
        services.AddSingleton<AgreementDocument>();
        services.AddScoped<IAccountEmailSender, AccountEmailSender>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static IEndpointRouteBuilder MapStudentAuth(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Cuenta");
        group.MapGet("/csrf", (HttpContext http, IAntiforgery antiforgery, IHostEnvironment environment) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
            { HttpOnly = false, Secure = !environment.IsDevelopment() || http.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/" });
            return Results.Ok();
        });
        group.MapGet("/me", async (HttpContext http, UserManager<ApplicationUser> users, CapturasDbContext db, AgreementDocument agreement) =>
        {
            var user = await users.GetUserAsync(http.User);
            var student = user is null ? null : await db.Alumnos.FindAsync(user.StudentId);
            return student is null || student.Estado != "ACTIVO" ? Results.Unauthorized()
                : Results.Ok(new AccountView(student.Id, student.Nombre, user!.Email!, await agreement.HasAcceptedAsync(db, user.Id)));
        }).RequireAuthorization();
        group.MapPost("/login", Login).RequireRateLimiting("accounts");
        group.MapPost("/logout", async (HttpContext http, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
        {
            var user = await users.GetUserAsync(http.User);
            if (user is not null) await users.UpdateSecurityStampAsync(user);
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPassword).RequireRateLimiting("accounts");
        group.MapPost("/reset-password", async (ResetPasswordRequest request, UserManager<ApplicationUser> users) =>
        {
            if (!ValidPassword(request.Password)) return InvalidPassword();
            var user = await users.FindByEmailAsync(request.Email?.Trim() ?? "");
            if (user is null || string.IsNullOrWhiteSpace(request.Token)) return InvalidReset();
            var result = await users.ResetPasswordAsync(user, request.Token, request.Password);
            return result.Succeeded ? Results.NoContent() : InvalidReset();
        }).RequireRateLimiting("accounts");
        // Explicit account + student creation replaces the former name-only API.
        endpoints.MapPost("/api/alumnos", Register).WithTags("Cuenta").RequireRateLimiting("accounts");
        return endpoints;
    }

    private static async Task<IResult> Register(RegisterRequest request, CapturasDbContext db, UserManager<ApplicationUser> users)
    {
        var name = request.Name?.Trim() ?? "";
        var email = request.Email?.Trim() ?? "";
        if (name.Length is < 1 or > 120) return Results.Problem(statusCode: 400, title: "Escribí un nombre de hasta 120 caracteres.");
        if (email.Length > 180 || !new EmailAddressAttribute().IsValid(email))
            return Results.Problem(statusCode: 400, title: "Escribí un correo válido.");
        if (!ValidPassword(request.Password)) return InvalidPassword();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = email, Email = email, Student = new Alumno { Nombre = name, Email = email } };
        try
        {
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return RegistrationFailed();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException) { return RegistrationFailed(); }
        return Results.Created("/api/auth/me", new AccountView(user.StudentId, name, email));
    }

    private static async Task<IResult> Login(LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, CapturasDbContext db)
    {
        var user = await users.FindByEmailAsync(request.Email?.Trim() ?? "");
        if (user is null || string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
            return InvalidLogin();
        if (!await db.Alumnos.AnyAsync(a => a.Id == user.StudentId && a.Estado == "ACTIVO")) return InvalidLogin();
        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded ? Results.NoContent() : InvalidLogin();
    }

    private static async Task<IResult> ForgotPassword(ForgotPasswordRequest request, UserManager<ApplicationUser> users,
        IAccountEmailSender emailSender, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email?.Trim() ?? "");
        var origin = configuration["Auth:PublicOrigin"];
        if (user is not null && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == "https")
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var url = $"{uri.GetLeftPart(UriPartial.Authority)}/restablecer-clave?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
            await emailSender.SendResetAsync(user.Email!, url, cancellationToken);
        }
        // Same result for unknown addresses or unavailable SMTP: no account disclosure.
        return Results.Ok(new { message = "Si la cuenta existe, recibirás un enlace para restablecer tu contraseña. Revisá también correo no deseado." });
    }

    private static bool ValidPassword(string? password) => password is { Length: >= 12 and <= 128 };
    private static IResult InvalidPassword() => Results.Problem(statusCode: 400, title: "Usá una contraseña de entre 12 y 128 caracteres.");
    private static IResult InvalidLogin() => Results.Problem(statusCode: 401, title: "No pudimos iniciar sesión. Revisá tus datos o intentá más tarde.");
    private static IResult InvalidReset() => Results.Problem(statusCode: 400, title: "El enlace no es válido o venció. Solicitá uno nuevo.");
    private static IResult RegistrationFailed() => Results.Problem(statusCode: 400, title: "No pudimos crear la cuenta. Revisá el correo o intentá iniciar sesión.");
}
