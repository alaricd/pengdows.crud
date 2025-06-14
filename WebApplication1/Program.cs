#region

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Tokens;
using pengdows.crud;
using WebApplication1;

#endregion

var builder = WebApplication.CreateBuilder(args);

// === Configure Authentication ===
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority =
            builder.Configuration["OAuth2:Authority"]; // e.g., https://login.microsoftonline.com/{tenant}/v2.0
        options.Audience = builder.Configuration["OAuth2:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["OAuth2:Issuer"], // Optional override
            ValidateAudience = true,
            ValidateLifetime = true,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });

// === Core Services for pengdows.crud ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITypeMapRegistry, TypeMapRegistry>();
builder.Services.AddScoped<IDatabaseContext>(sp =>
    new DatabaseContext(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        SqliteFactory.Instance,
        sp.GetRequiredService<ITypeMapRegistry>()));

builder.Services.AddScoped<IAuditValueResolver, HttpContextAuditValueResolver>();
builder.Services.AddScoped<IEntityHelper<UserEntity, int>, UserEntityHelper>();
builder.Services.AddScoped<UserService>();

// === Add Controllers and Swagger ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization(); // 🔁 was incorrectly after app.Build()

var app = builder.Build();

// === Middleware ===
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // ✅ must be BEFORE UseAuthorization
app.UseAuthorization();

app.MapControllers();
app.Run();