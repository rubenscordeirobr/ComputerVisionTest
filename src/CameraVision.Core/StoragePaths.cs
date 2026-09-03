namespace CameraVision.Core;

/// <summary>Absolute filesystem locations resolved at startup.</summary>
public sealed record StoragePaths(string DatabasePath, string OutputRoot)
{
    /// <summary>Voice notes received by the WhatsApp agent, kept next to the database until transcribed (SPEC-19).</summary>
    public string InboundAudioRoot => Path.Combine(Path.GetDirectoryName(DatabasePath) ?? ".", "inbound-audio");
}
