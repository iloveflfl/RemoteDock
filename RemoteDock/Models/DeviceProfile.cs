using System;

namespace RemoteDock.Models;

public sealed class DeviceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Device";
    public string MountName { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "";
    public string RemotePath { get; set; } = "/home";
    public string DriveLetter { get; set; } = "R";
    public string WebUrl { get; set; } = "";
    public string VsCodeRemotePath { get; set; } = "";
    public string SshKeyPath { get; set; } = "";
    public string DeviceType { get; set; } = "Auto"; // Auto, Linux, Windows, Network/Web
    public int SortOrder { get; set; }
    public bool AutoMount { get; set; }
    public string EncryptedPasswordBase64 { get; set; } = "";
    public string GroupName { get; set; } = "Default";
    public string Tags { get; set; } = "";
    public string Notes { get; set; } = "";
    public string FavoritePathsText { get; set; } = "";
    public string CommandPresetsText { get; set; } = "";
    public string ServicesText { get; set; } = "";
    public string DockerComposePath { get; set; } = "";
    public string RoutesText { get; set; } = "";
    public string WorkspaceLocalPath { get; set; } = "";
    public string WorkspaceRemotePath { get; set; } = "";
    public string WorkspaceWebUrl { get; set; } = "";
    public string BackupRemotePath { get; set; } = "";
    public string BackupLocalPath { get; set; } = "";
}
