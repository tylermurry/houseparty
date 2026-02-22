using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace HouseParty.E2E;

public class FrontendSmokeTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task Frontend_Homepage_Loads_In_Browser()
    {
        testOutputHelper.WriteLine("Building application...");

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.HouseParty_AppHost>();
        await using var app = await appHost.BuildAsync();

        testOutputHelper.WriteLine("Starting application...");

        await app.StartAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await app.ResourceNotifications.WaitForResourceHealthyAsync("backend", timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("houseparty-frontend", timeout.Token);

        testOutputHelper.WriteLine("Running tests...");

        using var frontendClient = app.CreateHttpClient("houseparty-frontend");
        var homeUrl = frontendClient.BaseAddress ?? throw new InvalidOperationException("Frontend URL was not available.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false
        });

        var page = await browser.NewPageAsync();
        var response = await page.GotoAsync(homeUrl.ToString(), new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected a successful homepage response but got HTTP {response.Status}.");
    }
}
