using CrestApps.OrchardCore.Samples.A2AClient.Services;

var builder = WebApplication.CreateBuilder(args);

// A sample client for trying the server locally: it calls whatever endpoint, with whatever key, its page is given, and
// has no sign-in of its own, so it never runs outside Development.
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("This sample client runs only in the Development environment.");
}

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<A2AClientFactory>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapRazorPages();

await app.RunAsync();
