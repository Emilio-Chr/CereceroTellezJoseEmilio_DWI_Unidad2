using Convertify.Models;
using Convertify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Convertify.Controllers;

public class ConversionController : Controller
{
    private const long MaxFileSizeBytes = 100L * 1024 * 1024; // 100 MB
    private const long RequestLimitBytes = MaxFileSizeBytes + 1024 * 1024; // margen para el multipart

    private readonly IConversionService _conversionService;

    public ConversionController(IConversionService conversionService)
    {
        _conversionService = conversionService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    public async Task<IActionResult> Convert(ConversionViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return InvalidRequest(model);

        var error = await FileValidator.ValidateAsync(model.File, model.OutputFormat, MaxFileSizeBytes);
        if (error is not null)
        {
            ModelState.AddModelError(string.Empty, error);
            return InvalidRequest(model);
        }

        var result = await _conversionService.ConvertAsync(model.File!, model.OutputFormat!, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No se pudo completar la conversión.");
            return InvalidRequest(model);
        }

        var baseName = SanitizeBaseName(Path.GetFileNameWithoutExtension(model.File!.FileName));
        return RedirectToAction(nameof(Result), new { id = result.FileId, name = baseName });
    }

    [HttpGet]
    public IActionResult Result(string? id, string? name)
    {
        if (!_conversionService.TryGetConvertedFile(id, out var path))
        {
            TempData["Error"] = "El archivo ya no está disponible. Vuelve a convertirlo.";
            return RedirectToAction("Index", "Home");
        }

        return View(new ConversionViewModel
        {
            DownloadId = id,
            DownloadName = BuildDownloadName(name, path)
        });
    }

    [HttpGet]
    public IActionResult Download(string? id, string? name)
    {
        if (!_conversionService.TryGetConvertedFile(id, out var path))
        {
            TempData["Error"] = "El archivo ya fue descargado o ya no está disponible.";
            return RedirectToAction("Index", "Home");
        }

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        if (!contentTypeProvider.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";

        // DeleteOnClose: el archivo se elimina automáticamente cuando termina la descarga.
        var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        return File(stream, contentType, BuildDownloadName(name, path));
    }

    private ViewResult InvalidRequest(ConversionViewModel model)
    {
        model.File = null;
        return View("~/Views/Home/Index.cshtml", model);
    }

    private static string BuildDownloadName(string? name, string path)
    {
        // La extensión sale del archivo real, nunca de lo que envía el usuario.
        var baseName = SanitizeBaseName(Path.GetFileNameWithoutExtension(name ?? string.Empty));
        return baseName + Path.GetExtension(path);
    }

    private static string SanitizeBaseName(string? name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((name ?? string.Empty)
            .Where(c => !invalid.Contains(c) && !char.IsControl(c))
            .ToArray()).Trim();

        if (cleaned.Length > 80)
            cleaned = cleaned[..80];

        return string.IsNullOrWhiteSpace(cleaned) ? "convertify" : cleaned;
    }
}