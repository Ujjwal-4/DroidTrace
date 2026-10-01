using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DroidTrace.Models;
using Npgsql;

namespace DroidTrace.Services;

public sealed class AppPaths
{
    public string BaseDirectory { get; } = AppContext.BaseDirectory;
    public string SettingsFile => Path.Combine(BaseDirectory, "droidtrace.settings.json");
    public string EvidenceDirectory => Path.Combine(BaseDirectory, "Evidence");
}

public static class SettingsService
{
    public static AppSettings Load(string path)
    {
        try { if (File.Exists(path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new(); }
        catch { }
        return new();
    }
    public static void Save(string path, AppSettings settings) => File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
}

public sealed class AdbService
{
    private readonly string _adb;
    public AdbService(string adbPath) => _adb = string.IsNullOrWhiteSpace(adbPath) ? "adb" : adbPath;

    public async Task<AdbResult> RunAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo { FileName = _adb, Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        using var p = new Process { StartInfo = psi };
        try { p.Start(); } catch (Exception ex) { return new(-1, "", ex.Message); }
        var stdout = p.StandardOutput.ReadToEndAsync(ct); var stderr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        return new(p.ExitCode, await stdout, await stderr);
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        var r = await RunAsync("devices -l", ct);
        if (!r.Success) return [];
        var list = new List<DeviceInfo>();
        foreach (var line in r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var serial = parts[0]; var state = parts[1];
            string? model = Get(parts, "model:"); string? transport = Get(parts, "transport_id:");
            list.Add(new(serial, state, model, null, null, transport, null));
        }
        return list;
    }

    private static string? Get(string[] parts, string key) => parts.FirstOrDefault(x => x.StartsWith(key, StringComparison.OrdinalIgnoreCase))?[key.Length..];

    public async Task<string> ShellAsync(string serial, string command, CancellationToken ct = default)
    {
        var escaped = command.Replace("\"", "\\\"");
        var r = await RunAsync($"-s \"{serial}\" shell \"{escaped}\"", ct);
        return r.Success ? r.StdOut : $"[ADB ERROR] {r.StdErr}";
    }

    public async Task<string> GetAdbVersionAsync(CancellationToken ct = default)
    {
        var r = await RunAsync("version", ct);
        if (r.Success && !string.IsNullOrWhiteSpace(r.StdOut))
        {
            var firstLine = r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return firstLine?.Trim() ?? "ADB connected";
        }
        return string.IsNullOrWhiteSpace(r.StdErr) ? "ADB not detected" : r.StdErr.Trim();
    }

    public async Task<DeviceDetails> GetDeviceDetailsAsync(string serial, CancellationToken ct = default)
    {
        var model = (await ShellAsync(serial, "getprop ro.product.model", ct)).Trim();
        var manufacturer = (await ShellAsync(serial, "getprop ro.product.manufacturer", ct)).Trim();
        var version = (await ShellAsync(serial, "getprop ro.build.version.release", ct)).Trim();
        var securityPatch = (await ShellAsync(serial, "getprop ro.build.version.security_patch", ct)).Trim();
        var buildId = (await ShellAsync(serial, "getprop ro.build.id", ct)).Trim();
        var cpuAbi = (await ShellAsync(serial, "getprop ro.product.cpu.abi", ct)).Trim();

        var batteryRaw = await ShellAsync(serial, "dumpsys battery", ct);
        var match = Regex.Match(batteryRaw, @"level:\s*(\d+)");
        var batteryLevel = match.Success ? match.Groups[1].Value + "%" : "Unknown";

        return new DeviceDetails(
            serial,
            string.IsNullOrWhiteSpace(model) || model.StartsWith("[ADB") ? "Unknown" : model,
            string.IsNullOrWhiteSpace(manufacturer) || manufacturer.StartsWith("[ADB") ? "Unknown" : manufacturer,
            string.IsNullOrWhiteSpace(version) || version.StartsWith("[ADB") ? "Unknown" : version,
            string.IsNullOrWhiteSpace(securityPatch) || securityPatch.StartsWith("[ADB") ? "Unknown" : securityPatch,
            string.IsNullOrWhiteSpace(buildId) || buildId.StartsWith("[ADB") ? "Unknown" : buildId,
            batteryLevel,
            string.IsNullOrWhiteSpace(cpuAbi) || cpuAbi.StartsWith("[ADB") ? "Unknown" : cpuAbi
        );
    }
}

public sealed class AcquisitionService
{
    private readonly AdbService _adb;
    public AcquisitionService(AdbService adb) => _adb = adb;

    public async Task<EvidenceManifest> AcquireAsync(string serial, string caseId, string root, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var dir = Path.Combine(root, Sanitize(caseId) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        var artifacts = new List<ArtifactResult>();
        var jobs = new (string Name, string Command, string File)[]
        {
            ("device_info", "getprop", "device_info.txt"),
            ("cpu_memory", "cat /proc/cpuinfo; echo '---MEMORY---'; cat /proc/meminfo", "cpu_memory.txt"),
            ("battery", "dumpsys battery", "battery.txt"),
            ("packages", "pm list packages -f", "packages.txt"),
            ("contacts", "content query --uri content://com.android.contacts/contacts --projection display_name:contact_id", "contacts.txt"),
            ("sms", "content query --uri content://sms --projection _id:address:body:date:type", "sms.txt"),
            ("calls", "content query --uri content://call_log/calls --projection _id:number:name:duration:date:type", "calls.txt")
        };
        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested(); progress?.Report($"Acquiring {job.Name}…");
            var text = await _adb.ShellAsync(serial, job.Command, ct);
            var path = Path.Combine(dir, job.File);
            await File.WriteAllTextAsync(path, text, Encoding.UTF8, ct);
            var hash = await HashFileAsync(path, ct);
            var records = CountRecords(job.Name, text);
            var success = !text.StartsWith("[ADB ERROR]", StringComparison.Ordinal);
            artifacts.Add(new(job.Name, job.File, records, success, success ? null : text, hash, DateTimeOffset.UtcNow));
        }
        var completed = DateTimeOffset.UtcNow;
        // Hash the manifest content without its self-referential hash field.
        // This gives the examiner a stable digest that can be independently recomputed.
        var manifestContent = JsonSerializer.Serialize(new
        {
            CaseId = caseId,
            StartedAt = started,
            CompletedAt = completed,
            DeviceSerial = serial,
            Artifacts = artifacts
        }, new JsonSerializerOptions { WriteIndented = true });
        var manifestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestContent))).ToLowerInvariant();
        var manifest = new EvidenceManifest(caseId, started, completed, serial, artifacts, manifestHash);
        var manifestPath = Path.Combine(dir, "evidence-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);
        progress?.Report($"Evidence saved: {dir}");
        return manifest;
    }

    private static int CountRecords(string type, string text) => type is "contacts" or "sms" or "calls" ? Regex.Matches(text, @"Row:\s*\d+", RegexOptions.IgnoreCase).Count : text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    public static async Task<string> HashFileAsync(string path, CancellationToken ct = default) { await using var s = File.OpenRead(path); var h = await SHA256.HashDataAsync(s, ct); return Convert.ToHexString(h).ToLowerInvariant(); }
    private static string Sanitize(string s) => string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

public static class TimelineService
{
    public static List<TimelineEvent> Build(string evidenceDirectory)
    {
        var events = new List<TimelineEvent>();
        AddProvider(events, Path.Combine(evidenceDirectory, "sms.txt"), "SMS", "content://sms");
        AddProvider(events, Path.Combine(evidenceDirectory, "calls.txt"), "CALL", "content://call_log/calls");
        return events.OrderByDescending(x => x.Timestamp).ToList();
    }
    private static void AddProvider(List<TimelineEvent> events, string file, string artifact, string source)
    {
        if (!File.Exists(file)) return;
        var text = File.ReadAllText(file);
        foreach (Match row in Regex.Matches(text, @"Row:\s*\d+\s+(?<data>.+)", RegexOptions.IgnoreCase))
        {
            var data = row.Groups["data"].Value;
            var dateMatch = Regex.Match(data, @"\bdate=(\d{10,13})");
            if (!dateMatch.Success) continue;
            if (!long.TryParse(dateMatch.Groups[1].Value, out var epoch)) continue;
            if (epoch < 10_000_000_000) epoch *= 1000;
            var ts = DateTimeOffset.FromUnixTimeMilliseconds(epoch);
            events.Add(new(ts, artifact, data.Length > 180 ? data[..180] + "…" : data, source));
        }
    }

    public static List<TimelineEvent> Filter(IEnumerable<TimelineEvent> events, string? query, string? artifactType = null)
    {
        var filtered = events;
        if (!string.IsNullOrWhiteSpace(artifactType) && !artifactType.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(x => x.Artifact.Equals(artifactType, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filtered = filtered.Where(x =>
                x.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Source.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Artifact.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss").Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        return filtered.OrderByDescending(x => x.Timestamp).ToList();
    }
}

public static class ExportService
{
    public static async Task ExportJsonAsync<T>(string path, T data) => await File.WriteAllTextAsync(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    public static async Task ExportCsvAsync(string path, IEnumerable<TimelineEvent> events)
    {
        var sb = new StringBuilder("Timestamp,Artifact,Summary,Source\n");
        foreach (var e in events) sb.AppendLine($"\"{e.Timestamp:O}\",\"{Esc(e.Artifact)}\",\"{Esc(e.Summary)}\",\"{Esc(e.Source)}\"");
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
    }
    private static string Esc(string s) => s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
}

public sealed class PostgreSqlService
{
    private readonly string _connectionString;
    public PostgreSqlService(string connectionString) => _connectionString = connectionString;
    public async Task<(bool Success, string Message)> TestAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return (false, "PostgreSQL is not configured. Acquisition does not require it.");
        try { await using var c = new NpgsqlConnection(_connectionString); await c.OpenAsync(); return (true, "PostgreSQL connection successful."); }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
