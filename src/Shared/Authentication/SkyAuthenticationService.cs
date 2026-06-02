using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shared.Connections;

namespace Shared.Authentication;

public sealed class SkyAuthenticationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectionRepository _connectionRepository;

    public SkyAuthenticationService(
        IHttpClientFactory httpClientFactory,
        IConnectionRepository connectionRepository)
    {
        _httpClientFactory = httpClientFactory;
        _connectionRepository = connectionRepository;
    }

    /// <summary>
    /// Gets a SKY API access token for the given connection.
    /// </summary>
    /// <param name="connection">The connection to authenticate.</param>
    /// <param name="forceRotate">
    /// When true, always rotates the refresh token.
    /// When false (default), only rotates if the refresh token expires within 6 months.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token string.</returns>
    public async Task<string> GetAccessTokenAsync(
        ConnectionRecord connection,
        bool forceRotate = false,
        CancellationToken cancellationToken = default)
    {
        if (connection.RefreshTokenValidToUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                $"The refresh token for connection '{connection.Name}' is expired. Update the connection and try again.");
        }

        var shouldRotate = forceRotate || connection.RefreshTokenValidToUtc <= DateTime.UtcNow.AddMonths(6);

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.sky.blackbaud.com/token");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.ClientId}:{connection.ClientSecret}")));

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = connection.RefreshToken,
            ["preserve_refresh_token"] = shouldRotate ? "false" : "true"
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"SKY authentication failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }

        var tokenResponse = JsonSerializer.Deserialize<SkyTokenResponse>(responseBody)
            ?? throw new InvalidOperationException("Failed to parse SKY token response.");

        if (shouldRotate && !string.IsNullOrWhiteSpace(tokenResponse.refresh_token))
        {
            var newRefreshToken = tokenResponse.refresh_token;
            var newExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.refresh_token_expires_in);

            connection.RefreshToken = newRefreshToken;
            connection.RefreshTokenValidToUtc = newExpiry;
            _connectionRepository.UpdateRefreshToken(connection.Id, newRefreshToken, newExpiry);
        }

        return tokenResponse.access_token;
    }

    public async Task<(string RefreshToken, DateTime ExpiresAtUtc)> ExchangeAuthCodeAsync(
        string clientId,
        string clientSecret,
        string authCode,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.sky.blackbaud.com/token");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authCode,
            ["redirect_uri"] = "https://localhost:7178/"
        });

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = TryParseBlackbaudErrorMessage(responseBody)
                ?? $"HTTP {(int)response.StatusCode}: {responseBody}";
            throw new InvalidOperationException($"SKY authentication failed: {errorMessage}");
        }

        var tokenResponse = JsonSerializer.Deserialize<SkyTokenResponse>(responseBody)
            ?? throw new InvalidOperationException("Failed to parse SKY token response.");

        return (tokenResponse.refresh_token, DateTime.UtcNow.AddSeconds(tokenResponse.refresh_token_expires_in));
    }

    private static string? TryParseBlackbaudErrorMessage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_description", out var descProp))
            {
                var desc = descProp.GetString();
                if (!string.IsNullOrWhiteSpace(desc))
                    return desc;
            }

            if (root.TryGetProperty("error", out var errProp))
                return errProp.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string GetAuthorizationUrl(string clientId) =>
        $"https://oauth2.sky.blackbaud.com/authorization?client_id={clientId}&response_type=code&redirect_uri=https://localhost:7178";
}
