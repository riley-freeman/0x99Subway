var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Map("/station_clock_in", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) return;
    
    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionID = Guid.NewGuid().ToString();
});

app.MapGet("/", () => "Hello World!")
    .WithName("Landing Page");

// Map stations to the web application
app.MapGet("/@{id}", (string id) => $"Station ID {id}");

app.UseHttpsRedirection();
app.Run();