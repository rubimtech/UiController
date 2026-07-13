using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class RecordVideoStartCommand : ICommand
{
    public string Name => "record-video";
    public string Description => "Start FFmpeg screen recording: record-video [--fps 5] [--quality medium|fast|slow]";
    public string Usage => "record-video [--fps 5] [--quality medium|fast|slow]";
    public ProgramOptions Options { get; set; } = new();

    private static Process? _ffmpegProcess;
    private static string? _outputPath;

    public static Process? FfmpegProcess => _ffmpegProcess;
    public static string? OutputPath => _outputPath;
    public static bool IsRecording => _ffmpegProcess != null && !_ffmpegProcess.HasExited;

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (IsRecording)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-video",
                Success = false,
                Error = "Video recording already in progress"
            }, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var fps = 5;
        var quality = "medium";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--fps" && i + 1 < args.Length && int.TryParse(args[++i], out var f))
                fps = f;
            else if (args[i] == "--quality" && i + 1 < args.Length)
                quality = args[++i];
        }

        var preset = quality.ToLowerInvariant() switch
        {
            "fast" => "ultrafast",
            "slow" => "slow",
            _ => "medium"
        };

        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        Directory.CreateDirectory(outputDir);

        _outputPath = Path.Combine(outputDir, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-f gdigrab -framerate {fps} -i desktop -c:v libx264 -preset {preset} -pix_fmt yuv420p \"{_outputPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _ffmpegProcess = Process.Start(psi);

            if (_ffmpegProcess == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "record-video",
                    Success = false,
                    Error = "Failed to start FFmpeg. Ensure ffmpeg is in PATH."
                }, Program.GlobalOptions));
                return Task.FromResult(1);
            }

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-video",
                Success = true,
                Data = new
                {
                    status = "started",
                    outputPath = _outputPath,
                    fps,
                    quality,
                    preset
                }
            }, Program.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("VideoStartError", _outputPath ?? "", [ex.Message]));
            _ffmpegProcess = null;
            _outputPath = null;
            return Task.FromResult(1);
        }
    }

    public static void Cleanup()
    {
        _ffmpegProcess = null;
        _outputPath = null;
    }
}

public class RecordVideoStopCommand : ICommand
{
    public string Name => "record-video-stop";
    public string Description => "Stop FFmpeg recording and save .mp4";
    public string Usage => "record-video-stop";
    public ProgramOptions Options { get; set; } = new();

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var process = RecordVideoStartCommand.FfmpegProcess;
        var outputPath = RecordVideoStartCommand.OutputPath;

        if (process == null || process.HasExited)
        {
            var savedPath = outputPath != null && File.Exists(outputPath) ? outputPath : null;
            RecordVideoStartCommand.Cleanup();
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-video-stop",
                Success = savedPath != null,
                Error = savedPath == null ? "No active recording to stop" : null,
                Data = savedPath != null ? new { savedTo = savedPath } : null
            }, Program.GlobalOptions));
            return Task.FromResult(savedPath != null ? 0 : 1);
        }

        try
        {
            process.StandardInput.Close();

            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(1000);
            }

            var outputPath2 = outputPath;

            if (outputPath2 != null && File.Exists(outputPath2))
            {
                TryAttachToAllure(outputPath2);

                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "record-video-stop",
                    Success = true,
                    Data = new
                    {
                        savedTo = outputPath2,
                        fileSize = new FileInfo(outputPath2).Length
                    }
                }, Program.GlobalOptions));
            }
            else
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "record-video-stop",
                    Success = false,
                    Error = "Video file not found after stopping FFmpeg"
                }, Program.GlobalOptions));
            }
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("VideoStopError", outputPath ?? "", [ex.Message]));
            return Task.FromResult(1);
        }
        finally
        {
            RecordVideoStartCommand.Cleanup();
        }

        return Task.FromResult(0);
    }

    private static void TryAttachToAllure(string videoPath)
    {
        try
        {
            var allureResults = "allure-results";
            if (!Directory.Exists(allureResults))
                return;

            var videoBytes = File.ReadAllBytes(videoPath);
            var b64 = Convert.ToBase64String(videoBytes);
            var attachmentJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                name = Path.GetFileName(videoPath),
                type = "video/mp4",
                source = b64
            });

            var allureFile = Path.Combine(allureResults, $"{Path.GetFileNameWithoutExtension(videoPath)}-attachment.json");
            File.WriteAllText(allureFile, attachmentJson);

            LoggingService.Info("Allure", $"Attached video to Allure: {allureFile}");
        }
        catch (Exception ex)
        {
            LoggingService.Warn("Allure", $"Failed to attach video to Allure: {ex.Message}");
        }
    }
}
