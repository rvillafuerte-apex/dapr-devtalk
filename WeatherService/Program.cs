using Dapr;
using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprClient();
builder.Services.AddControllers().AddDapr();

var app = builder.Build();
app.UseCloudEvents();
app.MapSubscribeHandler();

var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot" };

// ── Service Invocation target ─────────────────────────────────────────────────
app.MapGet("/forecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
    (
        Date: DateTime.Now.AddDays(index),
        TemperatureC: Random.Shared.Next(-10, 35),
        Summary: summaries[Random.Shared.Next(summaries.Length)]
    )).ToArray();

    return Results.Ok(forecast);
});

// ── Pub/Sub subscriber ────────────────────────────────────────────────────────
// Dapr automatically subscribes this endpoint to the "weather-requested" topic
app.MapPost("/weather-requested", [Topic("pubsub", "weather-requested")] (WeatherRequestedEvent evt) =>
{
    Console.WriteLine($"[PubSub] Weather was requested at: {evt.RequestedAt}");
    return Results.Ok();
});

app.Run();

record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
record WeatherRequestedEvent(DateTime RequestedAt);
