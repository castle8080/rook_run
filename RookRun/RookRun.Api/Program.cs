using RookRun.GoogleHealth.DependencyInjection;
using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore.DependencyInjection;
using RookRun.Strava.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddObjectStore(builder.Configuration)
    .AddStravaActivities(builder.Configuration)
    .AddGoogleHealth(builder.Configuration)
    .AddJobs(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
