using System.ComponentModel.DataAnnotations;

namespace Convertify.Models;

public class ConversionViewModel
{
    [Required(ErrorMessage = "Selecciona un archivo.")]
    public IFormFile? File { get; set; }

    [Required(ErrorMessage = "Selecciona un formato de salida.")]
    public string? OutputFormat { get; set; }

    // Usadas por la vista de resultado.
    public string? DownloadId { get; set; }
    public string? DownloadName { get; set; }
}