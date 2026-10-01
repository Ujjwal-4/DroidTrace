namespace DroidTrace.Models;

public sealed record AppSettings(string AdbPath = "adb", string PostgreSqlConnection = "", string EvidenceRoot = "Evidence");
public sealed record DeviceInfo(string Serial, string State, string? Model, string? Manufacturer, string? AndroidVersion, string? BuildId, string? SecurityPatch);
public sealed record DeviceDetails(string Serial, string Model, string Manufacturer, string AndroidVersion, string SecurityPatch, string BuildId, string BatteryLevel, string CpuAbi);
public sealed record ArtifactResult(string Type, string FileName, int Records, bool Success, string? Error, string Sha256, DateTimeOffset AcquiredAt);
public sealed record TimelineEvent(DateTimeOffset Timestamp, string Artifact, string Summary, string Source);
public sealed record EvidenceManifest(string CaseId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, string DeviceSerial, IReadOnlyList<ArtifactResult> Artifacts, string ManifestSha256);
public sealed record AdbResult(int ExitCode, string StdOut, string StdErr) { public bool Success => ExitCode == 0; }
