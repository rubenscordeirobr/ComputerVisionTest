using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using CameraVision.Core;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Capture = CameraVision.Core.Entities.Capture;

namespace CameraVision.Infrastructure;

/// <summary>
/// Imports recordings from {outputRoot}/{yyyy-MM-dd}/{camera}/*.mp4 into the Capture
/// table. Idempotent: files already indexed (by relative path) are skipped, and rows
/// whose file disappeared are removed. Cameras unknown to the database are created
/// (disabled, no stream URL). Thumbnails are extracted best-effort with ffmpeg.
/// </summary>
public sealed partial class CaptureIndexer(
    StoragePaths storage,
    ICaptureRepository captures,
    ICameraRepository cameras,
    ILogger<CaptureIndexer> logger) : ICaptureIndexer
{
    // e.g. "person_15-50-49_to_15-50-52.mp4", "person_14-00-20_to_14-01-33_full.mp4",
    //      "dog_10-00-00_to_10-00-05_track7.mp4" — class names may contain spaces.
    [GeneratedRegex(@"^(?<class>.+)_(?<start>\d{2}-\d{2}-\d{2})_to_(?<end>\d{2}-\d{2}-\d{2})(?<full>_full)?(_track(?<track>\d+))?\.mp4$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FileNameRegex();

    private static readonly TimeSpan StillBeingWritten = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private bool? _ffmpegAvailable;

    public async Task<IndexResult> ScanAsync(CancellationToken ct = default)
    {
        await _scanLock.WaitAsync(ct);
        try
        {
            return await ScanCoreAsync(ct);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task<IndexResult> ScanCoreAsync(CancellationToken ct)
    {
        if (!Directory.Exists(storage.OutputRoot))
            return new IndexResult([], 0);

        var known = (await captures.GetKnownFilePathsAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var onDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = new List<Capture>();
        var pending = new List<Capture>();
        var cameraCache = new Dictionary<string, Camera?>(StringComparer.OrdinalIgnoreCase);

        foreach (var dateDir in Directory.EnumerateDirectories(storage.OutputRoot))
        {
            var dateName = Path.GetFileName(dateDir);
            if (!DateOnly.TryParseExact(dateName, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                continue;

            foreach (var cameraDir in Directory.EnumerateDirectories(dateDir))
            {
                var cameraName = Path.GetFileName(cameraDir);
                foreach (var file in Directory.EnumerateFiles(cameraDir, "*.mp4"))
                {
                    ct.ThrowIfCancellationRequested();
                    var capture = await TryBuildCaptureAsync(file, date, dateName, cameraName,
                        known, onDisk, cameraCache, ct);
                    if (capture == null)
                        continue;

                    pending.Add(capture);
                    if (pending.Count >= 50)
                        await FlushAsync(pending, added, ct);
                }
            }
        }

        await FlushAsync(pending, added, ct);

        var removedPaths = known.Where(path => !onDisk.Contains(path)).ToList();
        var removed = await captures.RemoveByFilePathsAsync(removedPaths, ct);

        if (added.Count > 0 || removed > 0)
            logger.LogInformation("Capture scan: {Added} added, {Removed} removed.", added.Count, removed);

        return new IndexResult(added, removed);
    }

    private async Task<Capture?> TryBuildCaptureAsync(
        string file, DateOnly date, string dateName, string cameraName,
        HashSet<string> known, HashSet<string> onDisk,
        Dictionary<string, Camera?> cameraCache, CancellationToken ct)
    {
        var fileName = Path.GetFileName(file);
        if (fileName.StartsWith(".recording_", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = FileNameRegex().Match(fileName);
        if (!match.Success)
            return null;

        var info = new FileInfo(file);
        if (DateTime.Now - info.LastWriteTime < StillBeingWritten)
            return null;

        var relPath = $"{dateName}/{cameraName}/{fileName}";
        onDisk.Add(relPath);
        if (known.Contains(relPath))
            return null;

        if (!TryParseTime(match.Groups["start"].Value, out var startTime) ||
            !TryParseTime(match.Groups["end"].Value, out var endTime))
            return null;

        var startedAt = date.ToDateTime(startTime);
        var endedAt = date.ToDateTime(endTime);
        if (endedAt < startedAt)
            endedAt = endedAt.AddDays(1); // crossed midnight

        var camera = await GetOrCreateCameraAsync(cameraName, cameraCache, ct);

        return new Capture
        {
            CameraId = camera?.Id,
            CameraName = cameraName,
            ObjectClass = match.Groups["class"].Value,
            TrackId = match.Groups["track"].Success
                ? int.Parse(match.Groups["track"].Value, CultureInfo.InvariantCulture)
                : null,
            StartedAt = startedAt,
            EndedAt = endedAt,
            FilePath = relPath,
            ThumbnailPath = await TryCreateThumbnailAsync(file, relPath, ct),
            IsMerged = match.Groups["full"].Success,
            FileSizeBytes = info.Length,
            IndexedAt = DateTime.Now,
        };
    }

    private async Task FlushAsync(List<Capture> pending, List<Capture> added, CancellationToken ct)
    {
        if (pending.Count == 0)
            return;
        await captures.AddRangeAsync(pending, ct);
        added.AddRange(pending);
        pending.Clear();
    }

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH-mm-ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out time);

    private async Task<Camera?> GetOrCreateCameraAsync(
        string name, Dictionary<string, Camera?> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(name, out var cached))
            return cached;

        var camera = await cameras.GetByNameAsync(name, ct);
        if (camera == null)
        {
            try
            {
                camera = new Camera { Name = name, StreamUrl = "", Enabled = false };
                await cameras.AddAsync(camera, ct);
                logger.LogInformation(
                    "Auto-created camera '{Name}' from the output folder (no stream URL, disabled).", name);
            }
            catch (DbUpdateException)
            {
                // unique-name race with a concurrent insert — fetch the winner
                camera = await cameras.GetByNameAsync(name, ct);
            }
        }

        cache[name] = camera;
        return camera;
    }

    private async Task<string?> TryCreateThumbnailAsync(string videoPath, string relPath, CancellationToken ct)
    {
        var thumbPath = Path.ChangeExtension(videoPath, ".jpg");
        var relThumb = Path.ChangeExtension(relPath, ".jpg");
        if (File.Exists(thumbPath))
            return relThumb;

        if (_ffmpegAvailable == null)
        {
            _ffmpegAvailable = await RunFfmpegAsync("-version", ct);
            if (_ffmpegAvailable == false)
                logger.LogWarning("ffmpeg not found on PATH — captures are indexed without thumbnails.");
        }
        if (_ffmpegAvailable == false)
            return null;

        // -ss 1 skips a possibly dark first frame; clips shorter than 1 s fall back to -ss 0.
        foreach (var seek in new[] { "1", "0" })
        {
            var args = $"-hide_banner -loglevel error -ss {seek} -i \"{videoPath}\" " +
                       $"-frames:v 1 -vf scale=320:-2 -y \"{thumbPath}\"";
            if (await RunFfmpegAsync(args, ct) &&
                File.Exists(thumbPath) && new FileInfo(thumbPath).Length > 0)
                return relThumb;
        }
        return null;
    }

    private static async Task<bool> RunFfmpegAsync(string args, CancellationToken ct)
    {
        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo("ffmpeg", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process == null)
                return false;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            try { process?.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return false;
        }
        finally
        {
            process?.Dispose();
        }
    }
}
