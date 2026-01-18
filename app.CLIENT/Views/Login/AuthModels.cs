using System;
using System.IO;
using System.Text.Json;

namespace app.CLIENT;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, string RefreshToken, UserProfile User);

public sealed record UserProfile(int Id, string Email, string FullName, string Role, string? PhotoUrl);

public sealed record SessionData(string AccessToken, string RefreshToken, UserProfile User, string ApiBaseUrl);

public static class Session
{
    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VendingService",
        "session.json");

    public static string? AccessToken { get; set; }
    public static string? RefreshToken { get; set; }
    public static UserProfile? User { get; set; }
    public static string? ApiBaseUrl { get; set; } = "https://localhost:7018/";

    public static void Save()
    {
        if (string.IsNullOrWhiteSpace(AccessToken) || User == null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(SessionFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sessionData = new SessionData(
                AccessToken,
                RefreshToken ?? string.Empty,
                User,
                ApiBaseUrl ?? "https://localhost:7018/");

            var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SessionFilePath, json);
        }
        catch
        {
            // Игнорируем ошибки сохранения
        }
    }

    public static bool Load()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
            {
                return false;
            }

            var json = File.ReadAllText(SessionFilePath);
            var sessionData = JsonSerializer.Deserialize<SessionData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (sessionData == null || string.IsNullOrWhiteSpace(sessionData.AccessToken) || sessionData.User == null)
            {
                return false;
            }

            AccessToken = sessionData.AccessToken;
            RefreshToken = sessionData.RefreshToken;
            User = sessionData.User;
            ApiBaseUrl = sessionData.ApiBaseUrl;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        User = null;
        ApiBaseUrl = "https://localhost:7018/";

        try
        {
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
            }
        }
        catch
        {
            // Игнорируем ошибки удаления
        }
    }
}
