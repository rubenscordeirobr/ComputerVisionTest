using System.Diagnostics;
using CameraVision.Config;
using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CameraVision.Inference;

/// <summary>
/// Wraps a single shared YoloPredictor. Inference calls are serialized with a semaphore so
/// multiple camera pipelines can share one model/session safely.
/// </summary>
public sealed class InferenceEngine : IDisposable
{
    private readonly YoloPredictor _predictor;
    private readonly YoloConfiguration _yoloConfig;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string DeviceDescription { get; }
    public IReadOnlyList<string> ClassNames { get; }

    private InferenceEngine(YoloPredictor predictor, YoloConfiguration yoloConfig, string deviceDescription)
    {
        _predictor = predictor;
        _yoloConfig = yoloConfig;
        DeviceDescription = deviceDescription;
        ClassNames = predictor.Metadata.Names.Select(n => n.Name).ToArray();
    }

    public static InferenceEngine Create(AppConfig config)
    {
        var modelPath = config.ResolvePath(config.ModelPath);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"YOLO model not found at '{modelPath}'. Run scripts/download-model.ps1 to download/export it.");
        }

        var yoloConfig = new YoloConfiguration { Confidence = config.Detection.ConfidenceThreshold };
        var device = config.InferenceDevice.Trim().ToLowerInvariant();

        if (device is not ("auto" or "cuda" or "cpu"))
            throw new InvalidOperationException($"Invalid inferenceDevice '{config.InferenceDevice}' (expected auto, cuda or cpu).");

        if (device is "auto" or "cuda")
        {
            try
            {
                var predictor = new YoloPredictor(modelPath, new YoloPredictorOptions { UseCuda = true });
                Warmup(predictor); // fails here if CUDA/cuDNN native libraries are missing
                return new InferenceEngine(predictor, yoloConfig, $"CUDA ({QueryGpuName() ?? "NVIDIA GPU"})");
            }
            catch (Exception ex) when (device == "auto")
            {
                Log.Warn("inference", $"CUDA unavailable, falling back to CPU. Reason: {FirstLine(ex.Message)}");
                Log.Warn("inference", "For GPU inference install CUDA Toolkit 12.x and cuDNN 9.x (see README).");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "inferenceDevice is forced to 'cuda' but the CUDA execution provider failed to initialize. " +
                    "Install CUDA Toolkit 12.x and cuDNN 9.x and make sure their bin directories are on PATH. " +
                    $"Underlying error: {FirstLine(ex.Message)}", ex);
            }
        }

        // Explicit UseCuda = false: the YoloSharp.Gpu build defaults UseCuda to true.
        var cpuPredictor = new YoloPredictor(modelPath, new YoloPredictorOptions { UseCuda = false });
        Warmup(cpuPredictor);
        return new InferenceEngine(cpuPredictor, yoloConfig, "CPU");
    }

    public YoloResult<Detection> Detect(Image<Rgb24> image)
    {
        _gate.Wait();
        try
        {
            return _predictor.Detect(image, _yoloConfig);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void Warmup(YoloPredictor predictor)
    {
        using var image = new Image<Rgb24>(640, 640);
        predictor.Detect(image);
    }

    private static string? QueryGpuName()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name --format=csv,noheader",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process == null)
                return null;
            var name = process.StandardOutput.ReadLine()?.Trim();
            process.WaitForExit(3000);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    private static string FirstLine(string text)
    {
        var index = text.IndexOfAny(['\r', '\n']);
        return index < 0 ? text : text[..index];
    }

    public void Dispose()
    {
        _gate.Dispose();
        _predictor.Dispose();
    }
}
