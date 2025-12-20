using Chess.Db;
using Chess.Hubs;
using Chess.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

const string CorsOrigins = "_myAllowedOrigins";


var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<GameService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSignalR();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Extraemos el token de la cookie que llamamos "chess_token"
                var accessToken = context.Request.Cookies["X-Access-Token"];
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
app.MapHub<ChessHub>("/chesshub"); // Tus WebSockets

app.Run();
