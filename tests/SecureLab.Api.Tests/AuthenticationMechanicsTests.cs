
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SecureLab.Api.Data.Entities;

using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

public sealed class AuthenticationMechanicsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    // Початковий тест — без змін
    [Fact]
    public async Task RealCookie_ReachesSessionCheck()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");
        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/auth/session-check");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // T-01: успішна реєстрація користувача
    [Fact]
    public async Task Register_ValidUser_Returns201()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        var userName = "reg" + Guid.NewGuid().ToString("N")[..8];

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                userName,
                email = userName + "@example.test",
                displayName = "Student Test",
                password
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(userName, root.GetProperty("userName").GetString());

        // Конфіденційні дані не повинні повертатися
        foreach (var field in new[]
        {
            "password", "passwordHash", "securityStamp",
            "cookie", "token", "confirmPassword"
        })
        {
            Assert.False(root.TryGetProperty(field, out _));
        }
    }

    // T-02: повторна реєстрація користувача
    [Fact]
    public async Task Register_DuplicateUser_Returns409()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

        var userName = "reg" + Guid.NewGuid().ToString("N")[..8];

        using var client = factory.CreateClient();

        using var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                userName,
                email = userName + "@example.test",
                displayName = "First User",
                password
            });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                userName,
                email = userName + "2@example.test",
                displayName = "Second User",
                password
            });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    
    // A-02: перевірка профілю Alice після входу.
    [Fact]
    public async Task A02_LoginAlice_ReturnsAliceProfile()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Виконуємо вхід як Alice.
        using var client = await factory.LoginAsync("alice", password);

        // Отримуємо профіль авторизованого користувача.
        using var response = await client.GetAsync("/api/me");

        // Перевіряємо статус відповіді 200 OK.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        // Перевіряємо ім'я та ідентифікатор Alice.
        Assert.Equal("alice",
            json.RootElement.GetProperty("userName").GetString());

        Assert.Equal(
            "10000000-0000-0000-0000-000000000001",
            json.RootElement.GetProperty("id").GetString());
    }

    // A-03: підроблений заголовок не змінює користувача.
    [Fact]
    public async Task A03_ForgedHeader_DoesNotChangeUser()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Авторизуємося під обліковим записом Alice.
        using var client = await factory.LoginAsync("alice", password);

        // Створюємо запит із підробленим ID користувача Bob.
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/me");

        request.Headers.Add(
            "X-Demo-UserId",
            "10000000-0000-0000-0000-000000000002");

        // Надсилаємо запит із cookie Alice та заголовком Bob.
        using var response = await client.SendAsync(request);

        // Сервер повинен повернути профіль авторизованого користувача.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        // Перевіряємо, що відповідь належить Alice, а не Bob.
        Assert.Equal("alice",
            json.RootElement.GetProperty("userName").GetString());

        Assert.Equal(
            "10000000-0000-0000-0000-000000000001",
            json.RootElement.GetProperty("id").GetString());
    }

 // A-06: перевірка завершення сеансу після logout.
    [Fact]
    public async Task A06_Logout_ProtectedEndpointReturns401()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Виконуємо вхід як Alice.
        using var client = await factory.LoginAsync("alice", password);

        // Перевіряємо доступ до профілю перед виходом.
        using var before = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Виконуємо вихід із системи.
        using var logout = await client.PostAsync(
            "/api/auth/logout", null);

        // Перевіряємо успішне завершення сеансу.
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Повторно запитуємо профіль тим самим клієнтом.
        using var after = await client.GetAsync("/api/me");

        // Після logout доступ повинен бути заборонений.
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

         // A-07: однакова відповідь для неправильного пароля
    // та неіснуючого користувача.
    [Fact]
    public async Task A07_InvalidLogin_ReturnsSameError()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        using var client = factory.CreateClient();

        // Генеруємо ім'я користувача, якого немає в базі.
        var unknownName = "missing" + Guid.NewGuid().ToString("N")[..8];

        // Сценарій 1: Alice вводить неправильний пароль.
        using var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                userName = "alice",
                password = password + "_wrong"
            });

        // Сценарій 2: вхід під неіснуючим користувачем.
        using var unknownUser = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                userName = unknownName,
                password
            });

        // Обидва запити повинні отримати 401.
        Assert.Equal(HttpStatusCode.Unauthorized,
            wrongPassword.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            unknownUser.StatusCode);

        // Перевіряємо однаковий Content-Type.
        Assert.Equal(
            wrongPassword.Content.Headers.ContentType?.MediaType,
            unknownUser.Content.Headers.ContentType?.MediaType);

        // Отримуємо JSON-відповіді.
        using var json1 = JsonDocument.Parse(
            await wrongPassword.Content.ReadAsStringAsync());

        using var json2 = JsonDocument.Parse(
            await unknownUser.Content.ReadAsStringAsync());

        // Порівнюємо стабільні поля помилок.
        foreach (var field in new[]
        {
            "type", "title", "status", "detail"
        })
        {
            var hasFirst =
                json1.RootElement.TryGetProperty(field, out var first);

            var hasSecond =
                json2.RootElement.TryGetProperty(field, out var second);

            Assert.Equal(hasFirst, hasSecond);

            if (hasFirst)
            {
                Assert.Equal(first.GetRawText(), second.GetRawText());
            }
        }
    } 

     // Перевірка атрибутів cookie через HTTPS.
    [Fact]
    public async Task CookieFlags_Https_ReturnsSecureCookie()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Створюємо тестовий HTTPS-клієнт.
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        // Виконуємо вхід під обліковим записом Alice.
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName = "alice", password });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Шукаємо основну Identity cookie.
        Assert.True(
            response.Headers.TryGetValues("Set-Cookie", out var cookies));

        const string cookieName = ".AspNetCore.Identity.Application=";

        var matching = cookies!
            .Where(value => value.StartsWith(cookieName))
            .ToArray();

        Assert.Single(matching);

        // Аналізуємо тільки атрибути, без значення cookie.
        var attributes = matching[0]
            .Split(';')
            .Skip(1)
            .Select(part => part.Trim().ToLowerInvariant())
            .ToArray();

        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
    }


    // A-04: перевіряємо, що власник інциденту — Bob.
    [Fact]
    public async Task A04_BobCreatesIncident_OwnerIsBob()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Авторизуємося як Bob.
        using var client = await factory.LoginAsync("bob", password);

        // Створюємо інцидент без поля OwnerUserId.
        using var response = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "LR3 Bob " + Guid.NewGuid().ToString("N"),
                description = "Навчальний інцидент для перевірки власника.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow
            });

        // Перевіряємо успішне створення.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var incidentId = json.RootElement
            .GetProperty("id").GetGuid();

        // Знаходимо створений інцидент у базі даних.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        var incident = await db.Incidents
            .AsNoTracking()
            .SingleAsync(row => row.Id == incidentId);

        // Власником повинен бути Bob, а не Alice.
        Assert.Equal(DbSeeder.BobId, incident.OwnerUserId);
    }

    // A-05: підроблений OwnerUserId не змінює власника.
    [Fact]
    public async Task A05_ForgedOwner_OwnerRemainsBob()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Set local SeedUsers__Password outside Git.");

        // Авторизуємося як Bob.
        using var client = await factory.LoginAsync("bob", password);

        // Створюємо інцидент, підставляючи ID Alice.
        using var response = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "LR3 Forged Owner " + Guid.NewGuid().ToString("N"),
                description = "Перевірка захисту від підроблення власника інциденту.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow,
                ownerUserId = DbSeeder.AliceId
            });

        // Сервер повинен успішно створити інцидент.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var incidentId = json.RootElement
            .GetProperty("id").GetGuid();

        // Отримуємо створений інцидент із бази даних.
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        var incident = await db.Incidents
            .AsNoTracking()
            .SingleAsync(row => row.Id == incidentId);

        // Власником залишається Bob, а не підставлена Alice.
        Assert.Equal(DbSeeder.BobId, incident.OwnerUserId);
    }


    // A-01: анонімний користувач не може створити інцидент.
    [Fact]
    public async Task A01_AnonymousCreate_Returns401()
    {
        // Створюємо клієнта без авторизації.
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // Надсилаємо коректний запит без cookie.
        using var response = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "LR3 Anonymous " + Guid.NewGuid().ToString("N"),
                description = "Перевірка заборони створення без входу.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow
            });

        // Сервер повинен повернути 401 Unauthorized.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // Перевіряємо відсутність перенаправлення.
        Assert.Null(response.Headers.Location);

        // Не повинно бути Bearer challenge.
        Assert.Empty(response.Headers.WwwAuthenticate);

        // Перевіряємо безпечний формат відповіді.
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    // Перевірка обмеження спроб входу.
    [Fact]
    public async Task LoginRateLimit_Returns429_ThenRecovers()
    {
        // Окремий сервер із незалежним лічильником.
        using var host = factory.WithWebHostBuilder(_ => { });
        using var client = host.CreateClient();

        // Перші 20 запитів дозволені, але вхід невдалий.
        for (var i = 0; i < 20; i++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // 21-й запит повинен бути заблокований.
        using var blocked = await client.PostAsJsonAsync(
            "/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);

        // Другий сервер має власний лічильник.
        using var otherHost = factory.WithWebHostBuilder(_ => { });
        using var otherClient = otherHost.CreateClient();

        using var independent = await otherClient.PostAsJsonAsync(
            "/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, independent.StatusCode);

        // Чекаємо завершення 30-секундного вікна.
        await Task.Delay(TimeSpan.FromSeconds(32));

        // Після завершення вікна запити знову обробляються.
        using var recovered = await client.PostAsJsonAsync(
            "/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, recovered.StatusCode);
    }


    // Перевірка готовності PasswordHasher до оновлення старого хешу.
    [Fact]
    public void PasswordHasher_OldHash_RequiresRehash()
    {
        var user = new ApplicationUser
        {
            UserName = "rehash-test",
            DisplayName = "Rehash Test"
        };
        var password = Guid.NewGuid().ToString("N");

        // Створюємо хеш зі старими параметрами (10000 ітерацій).
        var oldHasher = new PasswordHasher<ApplicationUser>(
            Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = 10000
            }));

        // Створюємо hasher з оновленими параметрами (100000 ітерацій).
        var newHasher = new PasswordHasher<ApplicationUser>(
            Options.Create(new PasswordHasherOptions
            {
                CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
                IterationCount = 100000
            }));

        var oldHash = oldHasher.HashPassword(user, password);

        // Старий хеш повинен потребувати оновлення.
        var result = newHasher.VerifyHashedPassword(
            user, oldHash, password);

        Assert.Equal(
            PasswordVerificationResult.SuccessRehashNeeded, result);

        // Новий хеш повинен проходити перевірку без оновлення.
        var newHash = newHasher.HashPassword(user, password);

        Assert.Equal(
            PasswordVerificationResult.Success,
            newHasher.VerifyHashedPassword(user, newHash, password));
    }



}


