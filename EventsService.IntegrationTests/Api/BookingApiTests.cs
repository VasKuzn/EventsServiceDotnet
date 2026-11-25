using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using EventsService.Domain.Enums;
using EventsService.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EventsService.IntegrationTests.Api;

public sealed class BookingApiTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TestPassword = "test-password";

    [Fact]
    public async Task Auth_RegistersAndIssuesToken_RejectsInvalidCredentialsAndDuplicateLogin()
    {
        await using var app = CreateApi();
        using var client = app.CreateClient();
        var credentials = new { login = "reader", password = TestPassword };

        var registration = await Task.WhenAll(client.PostAsJsonAsync("/auth/register", credentials),
            client.PostAsJsonAsync("/auth/register", credentials));
        Assert.Single(registration, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(registration, response => response.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/register",
            new { login = "invalid-role", password = TestPassword, role = "editor" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/login",
            new { login = "reader", password = "wrong" })).StatusCode);

        var login = await client.PostAsJsonAsync("/auth/login", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()));

        await using var context = CreateContext();
        var stored = await context.Users.SingleAsync(user => user.Login == "reader");
        Assert.Equal(UserRole.User, stored.Role);
        Assert.NotEqual(TestPassword, stored.PasswordHash);
    }

    [Fact]
    public async Task EventManagement_RequiresAdminAndSwaggerShowsBearerSecurity()
    {
        await using var app = CreateApi();
        using var anonymous = app.CreateClient();
        using var ordinary = app.CreateClient();
        using var admin = app.CreateClient();
        await SignInAsync(ordinary, "ordinary");
        await SignInAsync(admin, "admin", "Admin");
        var eventId = Guid.NewGuid();
        var payload = NewEvent();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/events")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/events", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"/events/{eventId}", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/events/{eventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync("/events", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PutAsJsonAsync($"/events/{eventId}", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.DeleteAsync($"/events/{eventId}")).StatusCode);

        var created = await admin.PostAsJsonAsync("/events", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(location, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(location)).StatusCode);

        var swagger = await anonymous.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.Equal("bearer", swagger.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = swagger.GetProperty("paths");
        Assert.False(paths.GetProperty("/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/events").GetProperty("post").GetProperty("security")[0].TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Bookings_PersistTokenOwner_AndOnlyOwnerOrAdminCanCancel()
    {
        var eventEntity = TestData.CreateEvent(totalSeats: 2);
        await SeedAsync(eventEntity);
        await using var app = CreateApi();
        using var anonymous = app.CreateClient();
        using var owner = app.CreateClient();
        using var stranger = app.CreateClient();
        using var admin = app.CreateClient();
        await SignInAsync(owner, "owner");
        await SignInAsync(stranger, "stranger");
        await SignInAsync(admin, "admin", "Admin");
        var bookPath = $"/events/{eventEntity.Id}/book";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(bookPath, null)).StatusCode);
        var first = await owner.PostAsync(bookPath, null);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstLocation = first.Headers.Location;
        var booking = await first.Content.ReadFromJsonAsync<JsonElement>();
        await using var context = CreateContext();
        var userId = await context.Users.Where(user => user.Login == "owner").Select(user => user.Id).SingleAsync();
        Assert.Equal(userId, booking.GetProperty("userId").GetGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(firstLocation)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync(firstLocation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(firstLocation)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync(firstLocation)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.DeleteAsync(firstLocation)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.DeleteAsync($"/bookings/{Guid.NewGuid()}")).StatusCode);

        var second = await owner.PostAsync(bookPath, null);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/events/{eventEntity.Id}", NewEvent(totalSeats: 3))).StatusCode);
        var cancellations = await Task.WhenAll(owner.DeleteAsync(firstLocation), admin.DeleteAsync(firstLocation),
            admin.DeleteAsync(second.Headers.Location));
        Assert.All(cancellations, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.Equal(3, await context.Events.Where(item => item.Id == eventEntity.Id).Select(item => item.AvailableSeats).SingleAsync());
        Assert.Equal(2, await context.Bookings.CountAsync(item => item.UserId == userId && item.Status == BookingStatus.Cancelled));
    }

    [Fact]
    public async Task CreateBooking_RejectsStartedEventAndEnforcesQuotaAcrossConcurrentRequests()
    {
        var started = TestData.CreateEvent(startAt: DateTime.UtcNow.AddMinutes(-30));
        var future = Enumerable.Range(0, 12).Select(_ => TestData.CreateEvent(totalSeats: 2)).ToArray();
        await SeedAsync([started, .. future]);
        await using var app = CreateApi();
        using var client = app.CreateClient();
        using var anotherUser = app.CreateClient();
        await SignInAsync(client, "booker");
        await SignInAsync(anotherUser, "another-user");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/events/{started.Id}/book", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/events/{Guid.NewGuid()}/book", null)).StatusCode);
        var responses = await Task.WhenAll(future.Take(11).Select(item => client.PostAsync($"/events/{item.Id}/book", null)));
        Assert.Equal(10, responses.Count(response => response.StatusCode == HttpStatusCode.Accepted));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Accepted, (await anotherUser.PostAsync($"/events/{future[^1].Id}/book", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(responses.First(response => response.IsSuccessStatusCode).Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync($"/events/{future[^1].Id}/book", null)).StatusCode);
    }

    private WebApplicationFactory<Program> CreateApi() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = Fixture.ConnectionString,
            ["Jwt:Secret"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }));
    });

    private static async Task SignInAsync(HttpClient client, string login, string? role = null)
    {
        var registration = role is null
            ? await client.PostAsJsonAsync("/auth/register", new { login, password = TestPassword })
            : await client.PostAsJsonAsync("/auth/register", new { login, password = TestPassword, role });
        Assert.Equal(HttpStatusCode.NoContent, registration.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login", new { login, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object NewEvent(int totalSeats = 1)
    {
        var startAt = DateTime.UtcNow.AddDays(1);
        return new { title = "API event", startAt, endAt = startAt.AddHours(1), totalSeats };
    }
}
