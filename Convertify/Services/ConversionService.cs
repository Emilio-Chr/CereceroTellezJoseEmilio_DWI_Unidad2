using System.Diagnostics.CodeAnalysis;
using Convertify.Models;

namespace Convertify.Services;

public class ConversionService : IConversionService
{
    private readonly FFmpegService _ffmpeg;
    private readonly ILogger<ConversionService> _logger;
    private readonly string _uploadsDir;
    private readonly string _convertedDir;

    public ConversionService(FFmpegService ffmpeg, IWebHostEnvironment env, ILogger<ConversionService> logger)
    {
        _ffmpeg = ffmpeg;
        _logger = logger;

        _uploadsDir = Path.Combine(env.ContentRootPath, "Storage", "Uploads");
        _convertedDir = Path.Combine(env.ContentRootPath, "Storage", "Converted");

        Directory.CreateDirectory(_uploadsDir);
        Directory.CreateDirectory(_convertedDir);
    }

    public async Task<ConversionResult> ConvertAsync(IFormFile file, string outputFormat, CancellationToken cancellationToken = default)
    {
        var inputExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var outputExtension = outputFormat.Trim().ToLowerInvariant();

        if (!ConversionCatalog.TryGetInput(inputExtension, out var input) ||
            !ConversionCatalog.IsSupported(inputExtension, outputExtension))
        {
            return ConversionResult.Fail("La conversión solicitada no está soportada.");
        }

        // Nombres generados por el servidor: nunca se usa el nombre del usuario en rutas.
        var id = Guid.NewGuid().ToString("N");
        var inputPath = Path.Combine(_uploadsDir, id + inputExtension);
        var outputPath = Path.Combine(_convertedDir, id + "." + outputExtension);

        try
        {
            await using (var target = new FileStream(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(target, cancellationToken);
            }

            (bool Success, string? Error) outcome = input.Engine switch
            {
                ConversionEngine.FFmpeg => await ConvertWithFFmpegAsync(inputPath, outputPath, outputExtension, cancellationToken),
                _ => (false, "Este tipo de conversión aún no está disponible.")
            };

            if (!outcome.Success || !File.Exists(outputPath))
            {
                TryDelete(outputPath);
                return ConversionResult.Fail(outcome.Error ?? "No se pudo completar la conversión.");
            }

            return ConversionResult.Ok(id);
        }
        catch (OperationCanceledException)
        {
            TryDelete(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado durante la conversión.");
            TryDelete(outputPath);
            return ConversionResult.Fail("Ocurrió un error inesperado al convertir el archivo.");
        }
        finally
        {
            // El archivo original siempre se elimina al terminar.
            TryDelete(inputPath);
        }
    }

    public bool TryGetConvertedFile(string? id, [NotNullWhen(true)] out string? path)
    {
        path = null;

        // Solo se aceptan GUID en formato "N": evita Path Traversal y comodines.
        if (!Guid.TryParseExact(id, "N", out var guid))
            return false;

        path = Directory.EnumerateFiles(_convertedDir, guid.ToString("N") + ".*").FirstOrDefault();
        return path is not null;
    }

    private Task<(bool Success, string? Error)> ConvertWithFFmpegAsync(
        string inputPath, string outputPath, string outputExtension, CancellationToken ct)
    {
        return outputExtension switch
        {
            "mp3" => _ffmpeg.ConvertToMp3Async(inputPath, outputPath, ct),
            _ => Task.FromResult<(bool Success, string? Error)>((false, "Formato de salida no soportado."))
        };
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el archivo temporal {Path}.", path);
        }
    }
}