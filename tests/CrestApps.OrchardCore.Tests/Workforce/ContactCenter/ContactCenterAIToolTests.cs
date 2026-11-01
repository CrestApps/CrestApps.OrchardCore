using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.ContactCenter.AI;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Forecasting;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// The contact center AI tools: the forecast summary of a day, and both tools refusing people without the permission.
/// </summary>
public sealed class ContactCenterAIToolTests
{
    private static readonly DateOnly _day = new(2026, 11, 9);

    [Fact]
    public async Task TheForecastSummary_GivesTheDaysWorkAndItsPeak()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Sales", TeamKind.Team);

        await harness.RunAsync(async services =>
        {
            var groups = services.GetRequiredService<PlanningGroupManager>();
            var group = await groups.NewAsync();

            group.Name = "Sales voice";
            group.QueueIds = ["sales"];
            group.TeamId = team.ItemId;
            await groups.CreateAsync(group);

            // 9:00 in the team's time zone.
            var zone = await services.GetRequiredService<IWorkerTimeZoneService>().GetTeamAsync(team.ItemId);
            var start = CalendarTime.ToUtc(_day.ToDateTime(new TimeOnly(9, 0)), zone);

            await services.GetRequiredService<ISession>().SaveAsync(new Forecast
            {
                ItemId = "forecast",
                PlanningGroupId = group.ItemId,
                FromDate = _day,
                ToDate = _day,
                Status = ForecastStatus.Active,
                Intervals =
                [
                    new ForecastInterval { Date = _day, StartUtc = start, Volume = 10, AverageHandleSeconds = 300, RequiredScheduled = 3 },
                    new ForecastInterval { Date = _day, StartUtc = start.AddMinutes(15), Volume = 30, AverageHandleSeconds = 200, RequiredScheduled = 7 },
                    new ForecastInterval { Date = _day, StartUtc = start.AddMinutes(30), Volume = 0, AverageHandleSeconds = 0, RequiredScheduled = 0 },
                ],
            }, collection: WorkforceConstants.CollectionName);
        });

        var result = await InvokeAsync<ForecastSummaryTool>(harness, CalendarTestHarness.User("planner", WorkforcePermissions.ManageForecasts.Name), new() { ["planningGroupName"] = "sales VOICE", ["date"] = "2026-11-09" });
        var forecast = JsonDocument.Parse(result).RootElement.GetProperty("forecasts")[0];

        Assert.Equal(40, forecast.GetProperty("contacts").GetDouble());
        Assert.Equal(225, forecast.GetProperty("averageHandleSeconds").GetDouble());
        Assert.Equal(7, forecast.GetProperty("mostAgentsToSchedule").GetInt32());
        Assert.EndsWith("T09:15", forecast.GetProperty("mostAgentsAt").GetString());
        Assert.EndsWith("T09:30", forecast.GetProperty("lastWork").GetString());
    }

    [Fact]
    public async Task BothTools_RefusePeopleWithoutThePermission()
    {
        await using var harness = await CreateAsync();
        var agent = CalendarTestHarness.User("agent");

        Assert.Equal("You aren't allowed to see forecasts.", await InvokeAsync<ForecastSummaryTool>(harness, agent, []));
        Assert.Equal("You aren't allowed to see adherence.", await InvokeAsync<OutOfAdherenceTool>(harness, agent, []));
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddSingleton(new Mock<IIntervalStatisticsReader>().Object);
                services.AddScoped<PlanningGroupStore>();
                services.AddScoped<PlanningGroupManager>();
                services.AddScoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>();
                services.AddScoped<ForecastEventStore>();
                services.AddScoped<ForecastEventManager>();
                services.AddScoped<ForecastService>();
            },
            ForecastingIndexSchema.CreateAsync,
            [new PlanningGroupIndexProvider(), new ForecastEventIndexProvider(), new ForecastIndexProvider()]);
    }

    private static Task<string> InvokeAsync<TTool>(CalendarTestHarness harness, ClaimsPrincipal user, Dictionary<string, object> arguments)
        where TTool : AIFunction
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            var tool = ActivatorUtilities.CreateInstance<TTool>(services);
            var result = await tool.InvokeAsync(new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = services });

            return result?.ToString();
        });
    }
}
