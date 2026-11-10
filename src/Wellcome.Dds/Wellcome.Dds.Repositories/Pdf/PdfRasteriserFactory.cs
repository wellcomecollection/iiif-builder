using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Utils;

namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// Chooses and locates the rasteriser. In the Linux containers that is pdftoppm from poppler-utils.
/// On a Windows developer machine it is pdftoppm.exe if it is on the PATH, otherwise the Ghostscript
/// install the PdfThumbGenerator tool has always relied on.
/// </summary>
public static class PdfRasteriserFactory
{
    public static IPdfRasteriser Create(PdfThumbnailOptions options, ILogger logger)
    {
        var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        var choice = (options.Rasteriser ?? "auto").Trim().ToLowerInvariant();
        switch (choice)
        {
            case "pdftoppm":
            {
                var exe = options.ExecutablePath.HasText() ? options.ExecutablePath : FindOnPath("pdftoppm");
                return exe == null ? Unavailable(logger, "pdftoppm was configured but not found")
                    : new PdftoppmRasteriser(exe, options.Dpi, options.ThumbnailWidth, timeout, logger);
            }
            case "ghostscript":
            {
                var exe = options.ExecutablePath.HasText() ? options.ExecutablePath : FindGhostscript();
                return exe == null ? Unavailable(logger, "Ghostscript was configured but not found")
                    : new GhostscriptRasteriser(exe, options.Dpi, options.ThumbnailWidth, timeout, logger);
            }
            default:
            {
                var pdftoppm = FindOnPath("pdftoppm");
                if (pdftoppm != null)
                {
                    logger.LogInformation("PDF thumbnails will be rendered by pdftoppm at {Path}", pdftoppm);
                    return new PdftoppmRasteriser(pdftoppm, options.Dpi, options.ThumbnailWidth, timeout, logger);
                }
                var gs = FindGhostscript();
                if (gs != null)
                {
                    logger.LogInformation("PDF thumbnails will be rendered by Ghostscript at {Path}", gs);
                    return new GhostscriptRasteriser(gs, options.Dpi, options.ThumbnailWidth, timeout, logger);
                }
                return Unavailable(logger, "neither pdftoppm nor Ghostscript was found");
            }
        }
    }

    private static IPdfRasteriser Unavailable(ILogger logger, string why)
    {
        logger.LogWarning("PDF thumbnails cannot be generated: {Why}. {Advice}", why, UnavailablePdfRasteriser.Message);
        return new UnavailablePdfRasteriser();
    }

    /// <summary>
    /// The first directory on the PATH containing the executable (with .exe on Windows).
    /// </summary>
    public static string? FindOnPath(string name)
    {
        var fileNames = OperatingSystem.IsWindows() ? new[] { name + ".exe", name } : new[] { name };
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var fileName in fileNames)
            {
                var candidate = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// gs on the PATH, or on Windows the console binary of the newest install under Program Files.
    /// </summary>
    public static string? FindGhostscript()
    {
        var onPath = FindOnPath("gs") ?? (OperatingSystem.IsWindows() ? FindOnPath("gswin64c") : null);
        if (onPath != null)
        {
            return onPath;
        }
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var gsRoot = Path.Combine(programFiles, "gs");
        if (!Directory.Exists(gsRoot))
        {
            return null;
        }
        return Directory.GetDirectories(gsRoot)
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
            .Select(d => Path.Combine(d, "bin", "gswin64c.exe"))
            .FirstOrDefault(File.Exists);
    }
}
