namespace TrackNTrash.Tracking.Api.Auth;

/// <summary>
/// Applies <c>RequireAuthorization()</c> only when this deployment actually has a sign-in method
/// configured. Keeps local/dev runs (no Auth section) usable without tokens, while the deployed
/// environment protects the console surface.
/// </summary>
public static class AuthEndpointExtensions
{
    public static TBuilder RequireAuthorizationWhenConfigured<TBuilder>(this TBuilder builder, AuthOptions options)
        where TBuilder : IEndpointConventionBuilder
    {
        if (options.LocalEnabled || options.EntraEnabled) builder.RequireAuthorization();
        return builder;
    }
}
