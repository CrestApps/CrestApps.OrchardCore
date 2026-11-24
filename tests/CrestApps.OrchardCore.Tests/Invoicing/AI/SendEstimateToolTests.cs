using System.Text.Json;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>sendEstimate</c> does what the estimate's Send button does: with the site's e-mail set up it numbers the draft and
/// e-mails it to the customer; without it, it only numbers it so staff share its link. It refuses a caller without the
/// permission and never returns the signed share link.
/// </summary>
public sealed class SendEstimateToolTests
{
    private readonly InvoicingCreditToolContext _context = new();

    [Fact]
    public async Task Send_IsRefused_WithoutThePermission()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var estimate = await InvoicingCreditToolContext.CreateEstimateAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new SendEstimateTool(), new() { ["estimate"] = estimate.ItemId }, InvoicingCreditToolContext.AllBut(InvoicingPermissions.ManageEstimates));

        // Assert
        Assert.Contains("not allowed", answer, StringComparison.Ordinal);
        Assert.Equal(EstimateStatus.Draft, (await FindAsync(harness, estimate.ItemId)).Status);
        Assert.Empty(harness.Emails);
    }

    [Fact]
    public async Task Send_NumbersTheDraft_AndEmailsItToTheCustomer()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var estimate = await InvoicingCreditToolContext.CreateEstimateAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new SendEstimateTool(), new() { ["estimate"] = estimate.ItemId });

        // Assert
        using var json = JsonDocument.Parse(answer);
        Assert.True(json.RootElement.GetProperty("emailed").GetBoolean());
        Assert.Equal("ap@fabrikam.example", json.RootElement.GetProperty("emailedTo").GetString());
        Assert.DoesNotContain("token", answer, StringComparison.OrdinalIgnoreCase);

        var sent = await FindAsync(harness, estimate.ItemId);
        Assert.Equal(EstimateStatus.Sent, sent.Status);
        Assert.False(string.IsNullOrEmpty(sent.Number));
        var (email, _) = Assert.Single(harness.Emails);
        Assert.Equal((InvoicingConstants.EmailKinds.EstimateSent, "ap@fabrikam.example"), (email.Kind, email.To));
    }

    [Fact]
    public async Task Send_WithoutEmail_OnlyNumbersTheEstimate()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        harness.CanSendEmail = false;
        var estimate = await InvoicingCreditToolContext.CreateEstimateAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new SendEstimateTool(), new() { ["estimate"] = estimate.ItemId });

        // Assert
        using var json = JsonDocument.Parse(answer);
        Assert.False(json.RootElement.GetProperty("emailed").GetBoolean());
        Assert.Equal(EstimateStatus.Sent, (await FindAsync(harness, estimate.ItemId)).Status);
        Assert.Empty(harness.Emails);
    }

    [Fact]
    public async Task DraftThenSend_InOneChatMessage_SendsTheDraftItMade()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();

        // Act: both calls share the message's scope and database session.
        var (id, answer) = await _context.InMessageAsync(harness, async invoke =>
        {
            var drafted = await invoke(new DraftEstimateTool(), new()
            {
                ["customerName"] = "Contoso Ltd.",
                ["customerEmail"] = "ap@contoso.example",
                ["currency"] = "USD",
                ["lines"] = JsonSerializer.SerializeToElement(new[] { new { description = "Training", quantity = 3, unitPrice = 20 } }),
            });

            using var json = JsonDocument.Parse(drafted);
            var estimateId = json.RootElement.GetProperty("id").GetString();

            return (estimateId, await invoke(new SendEstimateTool(), new() { ["estimate"] = estimateId }));
        });

        // Assert
        using var sentJson = JsonDocument.Parse(answer);
        Assert.True(sentJson.RootElement.GetProperty("emailed").GetBoolean());
        Assert.Equal(EstimateStatus.Sent, (await FindAsync(harness, id)).Status);
        Assert.Equal("ap@contoso.example", Assert.Single(harness.Emails).Email.To);

        var stored = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateManager>().PageAsync(1, 10, new EstimateQuery(), TestContext.Current.CancellationToken));
        Assert.Equal(1, stored.Count);
    }

    // The e-mail carries the estimate's signed link, built by the services the site registers for it.
    private static Task<InvoicingTestHarness> CreateHarnessAsync()
        => InvoicingTestHarness.CreateAsync(configureServices: services =>
        {
            services.AddScoped<SignedTokenService>();
            services.AddScoped<SiteUrlGenerator>();
            services.AddSingleton(Mock.Of<LinkGenerator>());
        });

    private static Task<Estimate> FindAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IEstimateStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());
}
