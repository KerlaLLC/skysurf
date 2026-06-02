namespace Shared.Authentication;

public sealed record SkyTokenResponse(string access_token, string refresh_token, int refresh_token_expires_in);
