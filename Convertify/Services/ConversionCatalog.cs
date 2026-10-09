using System.Diagnostics.CodeAnalysis;

namespace Convertify.Services;

public enum ConversionEngine
{
    FFmpeg,
    LibreOffice,
    Image
}

public record InputFormat(ConversionEngine Engine, string[] MimeTypes, string[] Outputs);

public static class ConversionCatalog
{
    private static readonly Dictionary<string, InputFormat> Inputs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".mp4"] = new(ConversionEngine.FFmpeg, new[] { "video/mp4" }, new[] { "mp3" }),
        };

    public static IReadOnlyDictionary<string, string[]> OutputsByExtension =>
        Inputs.ToDictionary(kv => kv.Key, kv => kv.Value.Outputs);

    public static bool TryGetInput(string? extension, [NotNullWhen(true)] out InputFormat? input)
    {
        input = null;
        return !string.IsNullOrWhiteSpace(extension) && Inputs.TryGetValue(extension, out input);
    }

    public static bool IsSupported(string? extension, string? outputFormat)
    {
        return TryGetInput(extension, out var input)
            && !string.IsNullOrWhiteSpace(outputFormat)
            && input.Outputs.Contains(outputFormat, StringComparer.OrdinalIgnoreCase);
    }
}