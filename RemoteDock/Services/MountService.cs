using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using RemoteDock.Models;

namespace RemoteDock.Services;

/// <summary>
/// SSHFS-Win mount support: generate correct UNC provider paths, resolve mount
/// hosts, manage drive letters, and translate raw <c>net use</c> errors into
/// actionable English guidance.
/// </summary>
public static class MountService
{
    /// <summary>
    /// Build the SSHFS-Win UNC path. Provider rules (see README / handoff):
    ///   home-relative + password -> \\sshfs\user@host\path
    ///   home-relative + key      -> \\sshfs.k\user@host\path
    ///   absolute + password      -> \\sshfs.r\user@host\path
    ///   absolute + key           -> \\sshfs.kr\user@host\path
    /// </summary>
    public static string BuildSshfsUnc(DeviceProfile p, string mountHost, bool useKeyAuth, out string note)
    {
        var user = string.IsNullOrWhiteSpace(p.User) ? "user" : p.User;
        var portPart = p.Port == 22 ? "" : "!" + p.Port;
        var rawPath = (string.IsNullOrWhiteSpace(p.RemotePath) ? "" : p.RemotePath).Trim();
        var isAbsolutePath = rawPath.StartsWith("/") || rawPath.StartsWith("\\");

        // Suffix letters are concatenated without a separator (k + r -> "kr"),
        // then attached to the base with a single dot. This yields sshfs.kr,
        // never the previously-broken sshfs.k.r.
        var suffix = (useKeyAuth ? "k" : "") + (isAbsolutePath ? "r" : "");
        var provider = suffix.Length > 0 ? "sshfs." + suffix : "sshfs";

        var path = rawPath.Replace('/', '\\').TrimStart('\\');
        var baseUnc = $@"\\{provider}\{user}@{mountHost}{portPart}";
        note = $"SSHFS mode: {provider} ({(isAbsolutePath ? "server-root path" : "home-relative path")}, {(useKeyAuth ? "key auth" : "password auth")})";
        return string.IsNullOrWhiteSpace(path) ? baseUnc : $@"{baseUnc}\{path}";
    }

    /// <summary>
    /// SSHFS-Win is less reliable with mDNS (.local) names than plain OpenSSH,
    /// so resolve to IPv4 for mount attempts when possible.
    /// </summary>
    public static string ResolveMountHostForSshfs(string host)
    {
        var trimmed = (host ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return trimmed;
        if (IPAddress.TryParse(trimmed, out _)) return trimmed;
        try
        {
            var ipv4 = Dns.GetHostAddresses(trimmed).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 != null) return ipv4.ToString();
        }
        catch { }
        return trimmed;
    }

    public static string NormalizeDrive(string value)
    {
        var trimmed = (value ?? "").Trim().TrimEnd(':');
        if (trimmed.Length == 0) return "";
        return trimmed[0].ToString().ToUpperInvariant();
    }

    public static string NextDriveLetter()
    {
        var used = DriveInfoSafe();
        for (var c = 'R'; c <= 'Z'; c++) if (!used.Contains(c)) return c.ToString();
        return "R";
    }

    public static bool IsDriveAvailable(string drive)
    {
        drive = NormalizeDrive(drive);
        if (string.IsNullOrWhiteSpace(drive)) return false;
        try { if (System.IO.Directory.Exists(drive + @":\")) return true; } catch { }
        try { return System.IO.DriveInfo.GetDrives().Any(d => d.Name.StartsWith(drive + ":", StringComparison.OrdinalIgnoreCase)); }
        catch { return false; }
    }

    private static System.Collections.Generic.HashSet<char> DriveInfoSafe()
    {
        try { return System.IO.DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet(); }
        catch { return new System.Collections.Generic.HashSet<char>(); }
    }

    /// <summary>
    /// Translate a raw <c>net use</c> failure into a clear, actionable message.
    /// net.exe prints "System error NN has occurred." rather than using the exit
    /// code, so this scans the combined stdout/stderr text.
    /// </summary>
    public static string? DescribeNetUseError(string combinedText)
    {
        if (string.IsNullOrWhiteSpace(combinedText)) return null;
        var t = combinedText.ToLowerInvariant();

        if (t.Contains("error 85"))
            return "Error 85: that drive letter is already in use. Unmount it first, or pick a different drive letter, then mount again.";
        if (t.Contains("error 67"))
            return "Error 67: Windows could not find the SSHFS network provider. Install or repair WinFsp and SSHFS-Win, then restart and try again.";
        if (t.Contains("error 53"))
            return "Error 53: the network path was not found. Check the host/IP, that SSH is reachable, and that SSHFS-Win is running.";
        if (t.Contains("error 1219"))
            return "Error 1219: conflicting credentials for this host. Disconnect existing mappings to the same server, then mount again.";
        if (t.Contains("error 86") || t.Contains("error 1326"))
            return "Authentication failed: the password or key was rejected. Verify the SSH password or key for this device.";
        return null;
    }
}
