namespace CameraVision.Core;

/// <summary>Absolute filesystem locations resolved at startup.</summary>
public sealed record StoragePaths(string DatabasePath, string OutputRoot);
