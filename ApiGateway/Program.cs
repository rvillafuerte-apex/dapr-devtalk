using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprClient();
builder.Services.AddControllers().AddDapr();

var app = builder.Build();
app.UseCloudEvents();
app.MapSubscribeHandler();

// ── 1. Service Invocation ─────────────────────────────────────────────────────
// Dapr routes the call to "weather-service" — no hardcoded URLs, no retries to write
app.MapGet("/weather", async (DaprClient dapr) =>
{
    var forecasts = await dapr.InvokeMethodAsync<WeatherForecast[]>(
        HttpMethod.Get, "weather-service", "forecast");
    return Results.Ok(forecasts);
});

// ── 2. Pub/Sub ────────────────────────────────────────────────────────────────
// Publish an event — WeatherService subscribes and handles it
app.MapGet("/weather/publish", async (DaprClient dapr) =>
{
    await dapr.PublishEventAsync("pubsub", "weather-requested", new { requestedAt = DateTime.UtcNow });
    return Results.Ok(new { message = "Event published to 'weather-requested' topic" });
});

// ── 3. Secrets ────────────────────────────────────────────────────────────────
// Dapr abstracts the secret store (local file, Azure Key Vault, AWS Secrets Manager...)
app.MapGet("/secrets", async (DaprClient dapr) =>
{
    var secret = await dapr.GetSecretAsync("localsecretstore", "github-token");
    return Results.Ok(new { hint = $"Secret length: {secret["github-token"]?.Length ?? 0} chars" });
});

// ── 4. State Management ───────────────────────────────────────────────────────
app.MapGet("/state/save", async (DaprClient dapr) =>
{
    await dapr.SaveStateAsync("statestore", "last-request", DateTime.UtcNow.ToString("O"));
    return Results.Ok(new { message = "State saved" });
});

app.MapGet("/state/get", async (DaprClient dapr) =>
{
    var value = await dapr.GetStateAsync<string>("statestore", "last-request");
    return Results.Ok(new { lastRequest = value });
});

// ── 5. AI — calls GitHub Models, key pulled from Dapr secret store ────────────
// Dapr abstracts the secret store — swap to Key Vault, AWS, etc. via one YAML line
app.MapGet("/weather/summarize/debug", async (DaprClient dapr) =>
{
    var forecasts = await dapr.InvokeMethodAsync<WeatherForecast[]>(
        HttpMethod.Get, "weather-service", "forecast");

    var forecastText = string.Join("\n", forecasts.Select(
        f => $"{f.Date:ddd}: {f.TemperatureC}°C, {f.Summary}"));

    return Results.Ok(new { forecastCount = forecasts.Length, forecastText, prompt = $"Summarize this 5-day weather forecast in 2 friendly sentences:\n{forecastText}" });
});

app.MapGet("/weather/summarize", async (DaprClient dapr) =>
{
    var forecasts = await dapr.InvokeMethodAsync<WeatherForecast[]>(
        HttpMethod.Get, "weather-service", "forecast");

    var forecastText = string.Join("\n", forecasts.Select(
        f => $"{f.Date:ddd}: {f.TemperatureC}°C, {f.Summary}"));

    // Pull API key from Dapr secret store (same line works for Key Vault, AWS, Vault...)
    var secret = await dapr.GetSecretAsync("localsecretstore", "github-token");
    var apiKey = secret["github-token"];

    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    var chatRequest = new
    {
        model = "gpt-4o-mini",
        messages = new[]
        {
            new { role = "user", content = $"Summarize this 5-day weather forecast in 2 friendly sentences:\n{forecastText}" }
        }
    };

    var response = await httpClient.PostAsJsonAsync(
        "https://models.inference.ai.azure.com/chat/completions", chatRequest);

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync();
        return Results.Problem($"GitHub Models error ({(int)response.StatusCode}): {errorBody}");
    }

    var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
    var summary = result?.Choices?.FirstOrDefault()?.Message?.Content;
    return Results.Ok(new { summary });
});

app.Run();

record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
record ChatMessage(string Role, string Content);
record ChatChoice(ChatMessage Message);
record ChatResponse(ChatChoice[]? Choices);
