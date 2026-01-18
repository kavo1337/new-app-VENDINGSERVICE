using System.Collections.Concurrent;

namespace app.API.Services.Auth
{
    public class InMemoryTokenStore
    {
        private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new();

        public void Store(string refreshToken, RefreshTokenEntry entry)
        {
            _tokens[refreshToken] = entry;
        }

        public bool TryGet(string refreshToken, out RefreshTokenEntry entry)
        {
            if (_tokens.TryGetValue(refreshToken, out entry))
            {
                if (entry.ExpiresAtUts > DateTime.UtcNow)
                {
                    return true;
                }
                _tokens.TryRemove(refreshToken, out _);
            }
            entry = default;
            return false;
        }
        public void Remove(string refreshToken)
        {
            _tokens.TryRemove(refreshToken, out _);
        }
    }
    public readonly record struct RefreshTokenEntry(int UserIdAccount, DateTime ExpiresAtUts);
}
