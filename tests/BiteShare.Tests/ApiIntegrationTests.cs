using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BiteShare.Data;
using BiteShare.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BiteShare.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789-abcdef");
        builder.ConfigureServices(services =>
        {
            var existing = services.Where(d => d.ServiceType == typeof(DbContextOptions<BiteShareDbContext>)).ToList();
            foreach (var d in existing) services.Remove(d);
            services.AddDbContext<BiteShareDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

/// <summary>End-to-end walk of the API the client uses: register -> host -> guest -> cart -> order.</summary>
public class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ApiIntegrationTests(ApiFactory factory) => _factory = factory;

    private static HttpClient WithToken(HttpClient c, string token)
    {
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private HttpClient Anon() => _factory.CreateClient();

    private async Task<AuthResponse> RegisterAsync(string email, string password = "Demo1234!")
    {
        var resp = await Anon().PostAsJsonAsync("api/auth/register", new RegisterRequest(email, password, "Demo Host"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Register_Login_And_Rejections()
    {
        var email = $"a{Guid.NewGuid():N}@t.test";
        await RegisterAsync(email);

        var dup = await Anon().PostAsJsonAsync("api/auth/register", new RegisterRequest(email, "Demo1234!", "X"));
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        var shortPw = await Anon().PostAsJsonAsync("api/auth/register", new RegisterRequest("b@t.test", "123", "X"));
        Assert.Equal(HttpStatusCode.BadRequest, shortPw.StatusCode);

        var ok = await Anon().PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Demo1234!"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var bad = await Anon().PostAsJsonAsync("api/auth/login", new LoginRequest(email, "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Sessions_Require_Identity_Token()
    {
        var resp = await Anon().PostAsJsonAsync("api/sessions", new CreateSessionRequest("x", null));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Full_Ordering_Flow()
    {
        var auth = await RegisterAsync($"h{Guid.NewGuid():N}@t.test");
        var host = WithToken(_factory.CreateClient(), auth.Token);

        // Host creates a session
        var created = await (await host.PostAsJsonAsync("api/sessions", new CreateSessionRequest("Lunch", DateTime.UtcNow.AddHours(1))))
            .Content.ReadFromJsonAsync<SessionWithTokenDto>();
        Assert.NotNull(created);
        var sid = created!.Session.Id;
        var hostP = WithToken(_factory.CreateClient(), created.ParticipantToken);

        // My sessions lists it
        var mine = await host.GetFromJsonAsync<List<SessionSummaryDto>>("api/sessions");
        Assert.Contains(mine!, s => s.Id == sid && s.JoinCode == created.Session.JoinCode);

        // Host adds menu items
        var m1 = await (await hostP.PostAsJsonAsync($"api/sessions/{sid}/menuitems", new { Name = "Pizza", Description = (string?)null, Price = 12.50m }))
            .Content.ReadFromJsonAsync<JsonMenu>();
        var m2 = await (await hostP.PostAsJsonAsync($"api/sessions/{sid}/menuitems", new { Name = "Fries", Description = (string?)null, Price = 4.00m }))
            .Content.ReadFromJsonAsync<JsonMenu>();
        Assert.NotNull(m1); Assert.NotNull(m2);

        // Guest joins with the code
        var guestResp = await Anon().PostAsJsonAsync("api/auth/guest-join", new GuestJoinRequest(created.Session.JoinCode.ToLowerInvariant(), "Kofi"));
        Assert.Equal(HttpStatusCode.OK, guestResp.StatusCode);
        var guest = (await guestResp.Content.ReadFromJsonAsync<GuestJoinResponse>())!;
        Assert.Equal(sid, guest.SessionId);
        var guestP = WithToken(_factory.CreateClient(), guest.GuestToken);

        // Bad code
        var badCode = await Anon().PostAsJsonAsync("api/auth/guest-join", new GuestJoinRequest("ZZZZZZ", "Nope"));
        Assert.Equal(HttpStatusCode.NotFound, badCode.StatusCode);

        // Guest cannot add menu items, cannot submit the order
        var guestMenu = await guestP.PostAsJsonAsync($"api/sessions/{sid}/menuitems", new { Name = "Hack", Description = (string?)null, Price = 1m });
        Assert.Equal(HttpStatusCode.Forbidden, guestMenu.StatusCode);

        // Participants + menu visible to guest
        var participants = await guestP.GetFromJsonAsync<List<JsonParticipant>>($"api/sessions/{sid}/participants");
        Assert.Equal(2, participants!.Count);
        var menu = await guestP.GetFromJsonAsync<List<JsonMenu>>($"api/sessions/{sid}/menuitems");
        Assert.Equal(2, menu!.Count);

        // Both add to the cart
        Assert.Equal(HttpStatusCode.OK, (await hostP.PostAsJsonAsync($"api/sessions/{sid}/cart", new AddCartItemRequest(m1!.Id, 1, null))).StatusCode);
        var guestCartAdd = await guestP.PostAsJsonAsync($"api/sessions/{sid}/cart", new AddCartItemRequest(m2!.Id, 2, "no salt"));
        Assert.Equal(HttpStatusCode.OK, guestCartAdd.StatusCode);
        var guestItem = (await guestCartAdd.Content.ReadFromJsonAsync<CartItemDto>())!;

        var cart = await hostP.GetFromJsonAsync<List<CartItemDto>>($"api/sessions/{sid}/cart");
        Assert.Equal(2, cart!.Count);

        // Host can't remove the guest's item? (record behavior) -> guest removes own item then re-adds
        Assert.Equal(HttpStatusCode.NoContent, (await guestP.DeleteAsync($"api/sessions/{sid}/cart/{guestItem.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guestP.PostAsJsonAsync($"api/sessions/{sid}/cart", new AddCartItemRequest(m2.Id, 2, null))).StatusCode);

        // Guest cannot submit; host can
        var guestSubmit = await guestP.PostAsJsonAsync($"api/sessions/{sid}/orders/submit", new SubmitOrderRequest("Equal", 0, 0, 0, new()));
        Assert.Equal(HttpStatusCode.Forbidden, guestSubmit.StatusCode);

        var submit = await hostP.PostAsJsonAsync($"api/sessions/{sid}/orders/submit", new SubmitOrderRequest("PerItem", 1.00m, 2.00m, 3.00m, new()));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var order = (await submit.Content.ReadFromJsonAsync<OrderSummaryDto>())!;
        Assert.Equal(20.50m, order.Subtotal);
        Assert.Equal(2, order.Receipts.Count);
        Assert.Equal(20.50m + 6.00m, order.Receipts.Sum(r => r.AmountOwed));

        // Second submit is rejected
        var again = await hostP.PostAsJsonAsync($"api/sessions/{sid}/orders/submit", new SubmitOrderRequest("Equal", 0, 0, 0, new()));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        // Status pipeline
        foreach (var s in new[] { "Preparing", "OutForDelivery", "Delivered" })
            Assert.Equal(HttpStatusCode.NoContent, (await hostP.PostAsJsonAsync($"api/sessions/{sid}/orders/{order.OrderId}/status", s)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hostP.PostAsJsonAsync($"api/sessions/{sid}/orders/{order.OrderId}/status", "Bogus")).StatusCode);

        // Receipts + PDF
        var pdf = await hostP.GetAsync($"api/sessions/{sid}/orders/{order.OrderId}/receipts/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.True((await pdf.Content.ReadAsByteArrayAsync()).Length > 100);

        // A finished session no longer takes guests
        var late = await Anon().PostAsJsonAsync("api/auth/guest-join", new GuestJoinRequest(created.Session.JoinCode, "Late"));
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);

        // Host can still re-open their own session from My Sessions (join-by-code with identity token)
        var reopen = await host.PostAsJsonAsync("api/sessions/join", new JoinSessionRequest(created.Session.JoinCode));
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_Touch_Another_Session()
    {
        var auth = await RegisterAsync($"x{Guid.NewGuid():N}@t.test");
        var host = WithToken(_factory.CreateClient(), auth.Token);
        var s1 = (await (await host.PostAsJsonAsync("api/sessions", new CreateSessionRequest("A", null))).Content.ReadFromJsonAsync<SessionWithTokenDto>())!;
        var s2 = (await (await host.PostAsJsonAsync("api/sessions", new CreateSessionRequest("B", null))).Content.ReadFromJsonAsync<SessionWithTokenDto>())!;
        var p1 = WithToken(_factory.CreateClient(), s1.ParticipantToken);
        var resp = await p1.GetAsync($"api/sessions/{s2.Session.Id}/cart");
        Assert.True(resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
            $"Expected cross-session access to be denied, got {resp.StatusCode}");
    }

    private record JsonMenu(Guid Id, string Name, string? Description, decimal Price, bool Available);
    private record JsonParticipant(Guid Id, string DisplayName, bool IsHost, bool IsGuest);
}
