using System.Text;

namespace Convertify.Services;

public static class FileValidator
{
    private static readonly string[] Mp4Signatures = { "ftyp", "moov", "mdat", "free", "wide", "skip" };

    /// <summary>Devuelve null si todo es válido, o el mensaje de error.</summary>
    public static async Task<string?> ValidateAsync(IFormFile? file, string? outputFormat, long maxBytes)
    {
        if (file is null || file.Length == 0)
            return "Selecciona un archivo válido.";

        if (file.Length > maxBytes)
            return $"El archivo supera el tamaño máximo de {maxBytes / 1024 / 1024} MB.";

        var extension = Path.GetExtension(file.FileName);

        if (!ConversionCatalog.TryGetInput(extension, out var input))
            return "Tipo de archivo no soportado.";

        if (!input.MimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return $"El tipo de contenido del archivo no está permitido ({file.ContentType}).";

        if (!ConversionCatalog.IsSupported(extension, outputFormat))
            return "El formato de salida no es válido para este archivo.";

        if (!await HasValidSignatureAsync(file, extension))
            return "El contenido del archivo no coincide con su extensión.";

        return null;
    }

    private static async Task<bool> HasValidSignatureAsync(IFormFile file, string extension)
    {
        if (!extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            return true;

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

        if (read < 12)
            return false;

        var marker = Encoding.ASCII.GetString(header, 4, 4);
        return Mp4Signatures.Contains(marker);
    }
}