using System.Diagnostics.CodeAnalysis;
using Convertify.Models;

namespace Convertify.Services;

public interface IConversionService
{
    Task<ConversionResult> ConvertAsync(IFormFile file, string outputFormat, CancellationToken cancellationToken = default);

    bool TryGetConvertedFile(string? id, [NotNullWhen(true)] out string? path);
}