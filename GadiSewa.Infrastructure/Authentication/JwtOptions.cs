namespace GadiSewa.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "GadiSewa.API";

    public string Audience { get; set; } = "GadiSewa.Client";

    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 120;
}
