using System.Security.Claims;

namespace TrackNTrash.Tracking.Api.Auth;

/// <summary>
/// Service accounts for unattended hardware.
///
/// A dock camera runs on a device nobody watches, mounted somewhere a contractor can reach.
/// Its credentials are the most likely in the system to leak, so a device account is refused
/// by the default policy and permitted only on the endpoints it actually needs. A stolen
/// camera credential therefore reads no orders, creates no trips and moves no stock.
/// </summary>
public static class DeviceRoles
{
    /// <summary>Role carried by camera service accounts.</summary>
    public const string Camera = "CameraDevice";

    /// <summary>Policy name for the endpoints a device is allowed to call.</summary>
    public const string DevicePolicy = "DeviceOrOperator";

    /// <summary>
    /// True when the caller holds only device roles. A human who happens to also carry a
    /// device role (an administrator testing a camera) is not restricted.
    /// </summary>
    public static bool IsDeviceOnly(ClaimsPrincipal user)
    {
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Length == 0) return false;
        return roles.All(r => string.Equals(r, Camera, StringComparison.OrdinalIgnoreCase));
    }
}
