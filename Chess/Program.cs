using Chess.Hubs;
using Chess.Service;

const string CorsOrigins = "_myAllowedOrigins";


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsOrigins, policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;

                return origin.StartsWith("http://localhost")
                    || origin.StartsWith("http://127.0.0.1")
                    || origin.StartsWith("http://192.168.")
                    || origin.StartsWith("http://10.");
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(CorsOrigins);  
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChessHub>("/chesshub"); // Tus WebSockets

app.Run();
