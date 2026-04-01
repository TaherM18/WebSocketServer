using WebSocketServer.Middlewares;
using WebSocketServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// WebSocket services
builder.Services.AddTransient<WebSocketServerMiddleware>();
builder.Services.AddSingleton<WebSockerServerConnectionManager>();
builder.Services.AddSingleton<ChatRoomService>();
builder.Services.AddSingleton<MessageHistoryService>();

var app = builder.Build();

app.UseWebSockets();

// app.Use(async (context, next) =>
// {
//     WriteRequestParams(context);
//     await next();
// });

app.UseMiddleware<WebSocketServerMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

// 

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}