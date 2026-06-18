namespace RemoteDock.Models;

public sealed class DiscoveryResult
{
    public string IpAddress { get; set; } = "";
    public string HostName { get; set; } = "";
    public string Services { get; set; } = "";
    public string SuggestedType { get; set; } = "";
    public string SuggestedName { get; set; } = "";
    public bool IsGateway { get; set; }
}
