using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

using System.Security.Claims;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
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
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegistrationInput input, UserManager<ApplicationUser> users) =>
        {
            // ЛР 03: завершіть DTO validation та перевірки довірчої межі.
            // Етап 2, крок 2: прибираємо крайні пробіли лише в полях профілю.
            // Пароль передаємо без Trim() та інших перетворень.
            var userName = input.UserName?.Trim();
            var email = input.Email?.Trim();
            var displayName = input.DisplayName?.Trim();

            // Етап 2, крок 1: перевіряємо обов'язковість полів.
            if (string.IsNullOrWhiteSpace(userName)
                || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrEmpty(input.Password))
            {
                return Results.BadRequest(new { message = "Registration failed" });
            }

            // Межі довжини відповідають обмеженням моделі бази даних.
            if (userName.Length > 256
                || email.Length > 256
                || displayName.Length > 120)
            {
                return Results.BadRequest(new { message = "Registration failed" });
            }

            // Ім'я користувача: латинські літери, цифри та символи - . _ @ +
            if (!Regex.IsMatch(userName, @"\A[a-zA-Z0-9._@+\-]+\z"))
            {
                return Results.BadRequest(new { message = "Registration failed" });
            }

            // Перевіряємо формат email та забороняємо пробіли в адресі.
            // У відображуваному імені забороняємо керувальні символи.
            if (!new EmailAddressAttribute().IsValid(email)
                || email.Any(char.IsWhiteSpace)
                || email.Any(char.IsControl)
                || displayName.Any(char.IsControl))
            {
                return Results.BadRequest(new { message = "Registration failed" });
            }

            // Етап 2, крок 3: створюємо користувача без обчислення PasswordHash.
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                DisplayName = displayName
            };


            // Стандартний Identity перевіряє пароль та обчислює його хеш.
            var result = await users.CreateAsync(user, input.Password);

            if (!result.Succeeded)
            {
                // Не повертаємо клієнту внутрішні описи помилок Identity.
                var isDuplicate = result.Errors.Any(error =>
                    error.Code == "DuplicateUserName"
                    || error.Code == "DuplicateEmail");

                if (isDuplicate)
                {
                    return Results.Conflict(new { message = "Registration failed" });
                }

                return Results.BadRequest(new { message = "Registration failed" });
            }


            var role = await users.AddToRoleAsync(user, DbSeeder.ReporterRole);
            if (!role.Succeeded) throw new InvalidOperationException("Default role assignment failed.");


            return Results.Created(
                "/api/me",
                new RegistrationResponse(user.Id, userName, displayName));

        });
   

        app.MapPost("/api/auth/login", async (
            LoginInput input,
            SignInManager<ApplicationUser> signIn,
            ILogger<Program> logger,
            HttpContext context) =>
        {
            // Перевіряємо обов'язкові поля.
            if (string.IsNullOrWhiteSpace(input.UserName) ||
                string.IsNullOrEmpty(input.Password))
            {
                logger.LogInformation(
                    "Auth event {EventType}: {Result}, time {Time}, trace {TraceId}",
                    "login", "failed", DateTimeOffset.UtcNow, context.TraceIdentifier);

                return Results.Unauthorized();
            }

            // Перевіряємо облікові дані через Identity.
            var result = await signIn.PasswordSignInAsync(
                input.UserName, input.Password, false, false);

            // Записуємо результат без пароля та cookie.
            logger.LogInformation(
                "Auth event {EventType}: {Result}, time {Time}, trace {TraceId}",
                "login", result.Succeeded ? "success" : "failed",
                DateTimeOffset.UtcNow, context.TraceIdentifier);

            return result.Succeeded
                ? Results.NoContent()
                : Results.Unauthorized();

        }).RequireRateLimiting("auth-login");






        app.MapPost("/api/auth/logout", async (
            SignInManager<ApplicationUser> signIn,
            ILogger<Program> logger,
            HttpContext context) =>
        {
            await signIn.SignOutAsync();

            // Записуємо успішний вихід без конфіденційних даних.
            logger.LogInformation(
                "Auth event {EventType}: {Result}, time {Time}, trace {TraceId}",
                "logout", "success", DateTimeOffset.UtcNow, context.TraceIdentifier);

            return Results.NoContent();

        }).RequireAuthorization();

            
       
        // Навмисно хибна навчальна довіра: ЛР 03 замінює заявлений id на verified principal.
  
        app.MapGet("/api/me", async (HttpContext context, SecureLabDbContext db) =>
        {
            // Отримуємо ID з перевіреного облікового запису.
            var userId = context.User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userId, out var verifiedId))
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError);

            // Знаходимо користувача за перевіреним ID.
            var user = await db.Users.FindAsync(verifiedId);

            return user is null
                ? Results.NotFound()
                : Results.Ok(new { user.Id, user.UserName, user.DisplayName });

        }).RequireAuthorization();



        
        // Приклад лише mechanics: не повертає identity і не реалізує оцінювану operation.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }
}

public sealed record RegistrationInput(string? UserName, string? Email, string? DisplayName, string? Password);
public sealed record LoginInput(string? UserName, string? Password);

public sealed record RegistrationResponse(
    Guid Id,
    string UserName,
    string DisplayName);