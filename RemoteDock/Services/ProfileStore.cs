using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using RemoteDock.Models;

namespace RemoteDock.Services;

public sealed class ProfileStore
{
    private readonly string _appDir;
    private readonly string _profilePath;
    private readonly string _keyDir;

    public ProfileStore()
    {
        _appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteDock");
        _profilePath = Path.Combine(_appDir, "profiles.json");
        _keyDir = Path.Combine(_appDir, "keys");
        Directory.CreateDirectory(_appDir);
        Directory.CreateDirectory(_keyDir);
    }

    public string AppDir => _appDir;
    public string ProfilePath => _profilePath;
    public string KeyDir => _keyDir;

    public List<DeviceProfile> Load()
    {
        if (!File.Exists(_profilePath)) return new List<DeviceProfile>();
        try
        {
            var json = File.ReadAllText(_profilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<DeviceProfile>>(json) ?? new List<DeviceProfile>();
        }
        catch
        {
            return new List<DeviceProfile>();
        }
    }

    public void Save(List<DeviceProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_profilePath, json, Encoding.UTF8);
    }

    public string ImportKeyForProfile(DeviceProfile profile, string sourcePath)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(sourcePath)) return "";
        sourcePath = sourcePath.Trim().Trim('"');
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("SSH key file was not found.", sourcePath);

        var profileKeyDir = Path.Combine(_keyDir, profile.Id);
        Directory.CreateDirectory(profileKeyDir);

        var originalName = Path.GetFileName(sourcePath);
        var safeName = string.Concat(originalName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var hash = ShortFileHash(sourcePath);
        var target = Path.Combine(profileKeyDir, $"{hash}_{safeName}");

        File.Copy(sourcePath, target, true);
        try { File.SetAttributes(target, (File.GetAttributes(target) | FileAttributes.Hidden) & ~FileAttributes.ReadOnly); } catch { }
        TryFixPrivateKeyAcl(target);
        return target;
    }

    /// <summary>SHA-256 fingerprint (first 12 hex chars) of a key file, used for collision-free storage.</summary>
    public static string ShortFileHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant()[..12];
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..12];
        }
    }

    public static void TryFixPrivateKeyAcl(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var user = WindowsIdentity.GetCurrent().Name;
            RunHidden("icacls", $"\"{path}\" /inheritance:r");
            RunHidden("icacls", $"\"{path}\" /remove:g *S-1-1-0 *S-1-5-32-545 *S-1-5-11");
            RunHidden("icacls", $"\"{path}\" /grant:r \"{user}:(R)\"");
        }
        catch { }
    }

    private static void RunHidden(string fileName, string args)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit(3000);
        }
        catch { }
    }

    public bool IsInsideKeyStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path.Trim().Trim('"'));
            var keyRoot = Path.GetFullPath(_keyDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(keyRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string DecryptPassword(string encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64)) return "";
        try
        {
            var protectedBytes = Convert.FromBase64String(encryptedBase64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}
