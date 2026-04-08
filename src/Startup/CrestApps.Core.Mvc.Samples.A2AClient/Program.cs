using CrestApps.Core.Mvc.Samples.A2AClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<A2AClientFactory>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapRazorPages();

await app.RunAsync();
