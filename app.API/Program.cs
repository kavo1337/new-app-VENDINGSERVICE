using app.API.Contracts;
using app.API.Data;
using app.API.Data.Entity;
using app.API.Services;
using app.API.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using static app.API.Services.Auth.InMemoryTokenStore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDBContext>(opt =>
{
    var connectionString = builder.Configuration.GetConnectionString("SqlServer");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("connection string is null");
    }
    opt.UseSqlServer(connectionString);
});
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<HasherPassword>();
builder.Services.AddSingleton<InMemoryTokenStore>();
builder.Services.AddSingleton<MonitoringStatusGenerator>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "ab657a7a546ab5aarta565a7567aba12345678901234567890";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "app";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "app";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
var authGroup = app.MapGroup("/api/auth");

authGroup.MapPost("/login", async (
    app.API.Contracts.LoginRequest request,
    AppDBContext db,
    JwtService tokenService,
    InMemoryTokenStore refreshTokenStore) =>
{
    var user = await db.UserAccount
        .Include(u => u.UserRole)
        .FirstOrDefaultAsync(u => u.Email == request.Email);

    if (user is null || !user.IsActive)
    {
        return Results.Unauthorized();
    }

    if (!HasherPassword.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
    {
        return Results.Unauthorized();
    }

    var accessToken = tokenService.CreateToken(user);
    var refreshToken = tokenService.CreateRefreshToken();

    refreshTokenStore.Store(
        refreshToken,
        new RefreshTokenEntry(user.UserAccountId, tokenService.GetRefreshTokenExpiry()));

    var profile = new UserProfile(
        user.UserAccountId,
        user.Email,
        BuildFullName(user),
        user.UserRole.Name,
        user.PhotoUrl);

    return Results.Ok(new LoginResponse(accessToken, refreshToken, profile));
});

authGroup.MapPost("/refresh-token", async (
    app.API.Contracts.RefreshRequest request,
    AppDBContext db,
    JwtService tokenService,
    InMemoryTokenStore refreshTokenStore) =>
{
    if (!refreshTokenStore.TryGet(request.RefreshToken, out var entry))
    {
        return Results.Unauthorized();
    }

    var user = await db.UserAccount
        .Include(u => u.UserRole)
        .FirstOrDefaultAsync(u => u.UserAccountId == entry.UserIdAccount);

    if (user is null || !user.IsActive)
    {
        return Results.Unauthorized();
    }

    var accessToken = tokenService.CreateToken(user);
    var newRefreshToken = tokenService.CreateRefreshToken();

    refreshTokenStore.Remove(request.RefreshToken);
    refreshTokenStore.Store(
        newRefreshToken,
        new RefreshTokenEntry(user.UserAccountId, tokenService.GetRefreshTokenExpiry()));

    var profile = new UserProfile(
        user.UserAccountId,
        user.Email,
        BuildFullName(user),
        user.UserRole.Name,
        user.PhotoUrl);

    return Results.Ok(new LoginResponse(accessToken, newRefreshToken, profile));
});

authGroup.MapPost("/logout", (LogoutRequest request, InMemoryTokenStore refreshTokenStore) =>
{
    refreshTokenStore.Remove(request.RefreshToken);
    return Results.Ok();
});
var monitoringGroup = app.MapGroup("/api/monitoring").RequireAuthorization();

monitoringGroup.MapGet("/machines", async (
    AppDBContext db,
    MonitoringStatusGenerator generator,
    string? status,
    string? connectionTypeId,
    string? additionalStatus) =>
{
    int? parsedConnectionTypeId = null;
    if (!string.IsNullOrWhiteSpace(connectionTypeId))
    {
        if (!int.TryParse(connectionTypeId, out var parsed))
        {
            return Results.BadRequest(new { message = "Некорректный тип подключения." });
        }

        parsedConnectionTypeId = parsed;
    }

    var machines = await db.VendingMachine
        .AsNoTracking()
        .Include(vm => vm.VendingMachineStatus)
        .Include(vm => vm.Modem)
            .ThenInclude(m => m!.Provider)
        .Include(vm => vm.VendingMachineEvent)
        .Include(vm => vm.VendingMachineEquipment)
            .ThenInclude(e => e.EquipmentType)
        .ToListAsync();

    var incomeByMachine = await db.VendingMachineIncome
        .AsNoTracking()
        .ToDictionaryAsync(i => i.VendingMachineId, i => i.TotalIncome ?? 0m);

    var result = new List<MonitoringMachineItem>();

    foreach (var vm in machines)
    {
        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(vm.VendingMachineStatus.Name, status, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (parsedConnectionTypeId.HasValue && vm.Modem?.ConnectionTypeId != parsedConnectionTypeId.Value)
        {
            continue;
        }

        var generated = generator.Generate(vm.VendingMachineId);

        if (!string.IsNullOrWhiteSpace(additionalStatus) &&
            !string.Equals(generated.Additional, additionalStatus, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var events = vm.VendingMachineEvent
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => e.Message)
            .FirstOrDefault() ?? "-";

        var equipment = vm.VendingMachineEquipment.Count == 0
            ? "-"
            : string.Join(", ", vm.VendingMachineEquipment.Select(e => e.EquipmentType.Name));

        incomeByMachine.TryGetValue(vm.VendingMachineId, out var income);

        result.Add(new MonitoringMachineItem(
            vm.VendingMachineId,
            vm.Name,
            vm.Modem?.Provider?.Name ?? "-",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            income,
            generated.ConnectionState,
            generated.CashInMachine,
            events,
            equipment,
            generated.InfoStatus,
            generated.Additional,
            generated.LoadItems));
    }

    return Results.Ok(result);
});
// Временный эндпоинт для генерации хеша и соли (удалите после использования)
authGroup.MapGet("/generate-hash", (string password) =>
{
    var saltBytes = RandomNumberGenerator.GetBytes(32);
    var salt = Convert.ToBase64String(saltBytes);
    var hash = HasherPassword.HashPassword(password, salt);
    return Results.Ok(new { Password = password, PasswordSalt = salt, PasswordHash = hash });
});
app.UseHttpsRedirection();
app.Run();

static string BuildFullName(UserAccount user)
{
    var parts = new[] { user.LastName, user.FirstName, user.Patronymic };
    return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
