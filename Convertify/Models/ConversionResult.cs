namespace Convertify.Models;

public record ConversionResult(bool Success, string? FileId = null, string? ErrorMessage = null)
{
    public static ConversionResult Ok(string fileId) => new(true, fileId);

    public static ConversionResult Fail(string message) => new(false, null, message);
}