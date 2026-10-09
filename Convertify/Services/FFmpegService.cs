using System.ComponentModel;
using System.Diagnostics;

namespace Convertify.Services;

public class FFmpegService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    private readonly string _ffmpegPath;
    private readonly ILogger<FFmpegService> _logger;

    public FFmpegService(IConfiguration configuration, ILogger<FFmpegService> logger)
    {
        _ffmpegPath = configuration["Convertify:FFmpegPath"] ?? "ffmpeg";
        _logger = logger;
    }

    public Task<(bool Success, string? Error)> ConvertToMp3Async(string inputPath, string outputPath, CancellationToken ct)
    {
        // Equivalente a: ffmpeg -i input.mp4 -vn -codec:a libmp3lame output.mp3
        var args = new[]
        {
            "-nostdin", "-hide_banner", "-y",
            "-i", inputPath,
            "-vn",
            "-codec:a", "libmp3lame",
            "-q:a", "2",
            outputPath
        };

        return RunAsync(args, ct);
    }

    private async Task<(bool Success, string? Error)> RunAsync(IEnumerable<string> args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList evita inyección de argumentos: cada valor es un argumento aislado.
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            _logger.LogError(ex, "No se pudo iniciar FFmpeg. Ruta configurada: {Path}", _ffmpegPath);
            return (false, "El servicio de conversión no está disponible en este momento.");
        }

        // Se leen ambos flujos en paralelo para evitar bloqueos por buffers llenos.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            if (ct.IsCancellationRequested)
                throw;

            _logger.LogError("FFmpeg superó el tiempo máximo de {Minutes} minutos.", Timeout.TotalMinutes);
            return (false, "La conversión tardó demasiado y fue cancelada.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            // El detalle técnico solo va al log del servidor, nunca al usuario.
            _logger.LogError("FFmpeg terminó con ExitCode {ExitCode}.\nSTDOUT: {Stdout}\nSTDERR: {Stderr}",
                process.ExitCode, stdout, stderr);

            return (false, "No se pudo convertir el archivo. Verifica que no esté dañado.");
        }

        return (true, null);
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo terminar el proceso de FFmpeg.");
        }
    }
}