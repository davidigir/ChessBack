using Chess.Db;
using Chess.Hubs;
using Chess.Service;
using Chess.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;




var builder = WebApplication.CreateBuilder(args);

//Default variables

builder.Services.Configure<ConfigSettings>(builder.Configuration.GetSection("ChessSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var chessSettings = builder.Configuration.GetSection("ChessSettings").Get<ConfigSettings>()
    ?? throw new Exception("ChessSettings Not Found");
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new Exception("JWT Not found");

const string CorsOrigins = "_myAllowedOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsOrigins, policy =>
    {
        policy
            .AllowCredentials()
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


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ChessDbContext>(options =>
    options.UseSqlServer(connectionString)
);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<GameService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Cookies[chessSettings.Auth.CookieName];
                var path = context.HttpContext.Request.Path;

                //if (!string.IsNullOrEmpty(accessToken) &&
                //(path.StartsWithSegments("/chesshub") || path.StartsWithSegments("/")))
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Si usas SignalR, configúralo también así:
builder.Services.AddSignalR()
    .AddJsonProtocol(options => {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

app.UseCors(CorsOrigins);
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChessHub>("/chesshub"); // ws

app.Run();
