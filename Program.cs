using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PhilosophyTester;
using PhilosophyTester.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<TestCatalog>();
builder.Services.AddScoped<DeviceStore>();
builder.Services.AddScoped<ProfileBuilder>();
builder.Services.AddScoped<BehaviorAnalyzer>();
builder.Services.AddScoped<ChallengePlanner>();

// Cross-test consistency checks. Add a class implementing IConsistencyRule and register it here.
builder.Services.AddScoped<IConsistencyRule, FamilyVersusStrangersRule>();
builder.Services.AddScoped<IConsistencyRule, StatedVersusWeekendRule>();
builder.Services.AddScoped<IConsistencyRule, LeverVersusPushRule>();
builder.Services.AddScoped<IConsistencyRule, LoyaltyClosenessRule>();
builder.Services.AddScoped<IConsistencyRule, LibertyVersusMonitoringRule>();
builder.Services.AddScoped<IConsistencyRule, SacrificeCurveRule>();
builder.Services.AddScoped<IConsistencyRule, TeachVersusLearnRule>();
builder.Services.AddScoped<IConsistencyRule, PublicChallengeRule>();
builder.Services.AddScoped<IConsistencyRule, OwnershipVersusBoundariesRule>();
builder.Services.AddScoped<IConsistencyRule, HonestyUnderDeadlineRule>();
builder.Services.AddScoped<IConsistencyRule, NeedVersusAccountabilityRule>();

await builder.Build().RunAsync();
