using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Deepfake_analysis.Auth;
using Deepfake_analysis.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

builder.Services.Configure<JwtOptions>(jwtSection);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<DataStore>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS-редиректа нет намеренно: клиенты API при перенаправлении на другой порт
// отбрасывают заголовок Authorization, и запрос с токеном получает 401.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
