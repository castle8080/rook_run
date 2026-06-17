using Microsoft.AspNetCore.ResponseCompression;
using RookRun.Job.DependencyInjection;
using RookRun.ObjectStore.DependencyInjection;
using RookRun.Strava.DependencyInjection;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/octet-stream",
        "application/wasm"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services
    .AddObjectStore(builder.Configuration)
    .AddStravaActivities(builder.Configuration)
    .AddJobs(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseResponseCompression();

app.UseDefaultFiles();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<RookRun.Api.Middleware.RequestResponseLoggingMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
