using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wellcome.Dds.Repositories.Pdf;

/// <summary>
/// Runs an external rasteriser as a subprocess with a timeout. Arguments are passed as a list, not a
/// command line, so paths with spaces and quotes need no escaping.
/// </summary>
public abstract class ProcessPdfRasteriser : IPdfRasteriser
{
    /// <summary>
    /// The iiif-builder container points this at a minimal fontconfig for libvips' text rendering, which
    /// knows only the one font the placeholder canvases use. A subprocess would inherit it and then
    /// substitute that font for anything a PDF does not embed, and differently from the other containers,
    /// which do not set it. The rasteriser gets the system fontconfig in every container instead.
    /// </summary>
    private const string FontConfigVariable = "FONTCONFIG_FILE";

    private readonly TimeSpan timeout;
    private readonly ILogger logger;

    protected ProcessPdfRasteriser(string executablePath, int dpi, int targetWidth, TimeSpan timeout, ILogger logger)
    {
        ExecutablePath = executablePath;
        Dpi = dpi;
        TargetWidth = targetWidth;
        this.timeout = timeout;
        this.logger = logger;
    }

    public abstract string Name { get; }
    public string ExecutablePath { get; }
    protected int Dpi { get; }
    protected int TargetWidth { get; }

    protected abstract IEnumerable<string> BuildArguments(string pdfPath, string outputPngPath);

    /// <summary>
    /// Where the tool actually writes, if it insists on naming the file itself.
    /// </summary>
    protected virtual string ActualOutputPath(string outputPngPath) => outputPngPath;

    public async Task RasteriseFirstPage(string pdfPath, string outputPngPath, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment.Remove(FontConfigVariable);
        foreach (var arg in BuildArguments(pdfPath, outputPngPath))
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        logger.LogDebug("{Rasteriser}: {Executable} {Arguments}", Name, ExecutablePath, string.Join(" ", startInfo.ArgumentList));
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new PdfRasteriseException($"{Name} could not be started from '{ExecutablePath}': {ex.Message}", ex);
        }

        // Drain both pipes concurrently so a chatty tool cannot block on a full buffer.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // already gone
            }
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            throw new PdfRasteriseException($"{Name} timed out after {timeout.TotalSeconds:0}s rendering '{pdfPath}'");
        }

        var errorOutput = (await stderr).Trim();
        await stdout;
        var actualOutput = ActualOutputPath(outputPngPath);
        if (process.ExitCode != 0 || !File.Exists(actualOutput))
        {
            throw new PdfRasteriseException(
                $"{Name} exited with {process.ExitCode} and {(File.Exists(actualOutput) ? "" : "no ")}output for '{pdfPath}'. {errorOutput}".Trim());
        }
        if (errorOutput.Length > 0)
        {
            // Tools warn about mildly broken PDFs and render them anyway.
            logger.LogInformation("{Rasteriser} rendered '{PdfPath}' with warnings: {Warnings}", Name, pdfPath, errorOutput);
        }
        if (actualOutput != outputPngPath)
        {
            File.Move(actualOutput, outputPngPath, overwrite: true);
        }
    }
}

/// <summary>
/// poppler-utils' pdftoppm: small, fast, renders only the requested page, and straight to the
/// thumbnail width so nothing is scaled afterwards.
/// </summary>
public class PdftoppmRasteriser : ProcessPdfRasteriser
{
    public PdftoppmRasteriser(string executablePath, int dpi, int targetWidth, TimeSpan timeout, ILogger logger)
        : base(executablePath, dpi, targetWidth, timeout, logger)
    {
    }

    public override string Name => "pdftoppm";

    protected override IEnumerable<string> BuildArguments(string pdfPath, string outputPngPath)
    {
        // -scale-to-x with -scale-to-y -1 keeps the aspect ratio; -singlefile writes exactly "<prefix>.png"
        return new[]
        {
            "-f", "1", "-l", "1",
            "-scale-to-x", TargetWidth.ToString(), "-scale-to-y", "-1",
            "-png", "-singlefile",
            pdfPath,
            OutputPrefix(outputPngPath)
        };
    }

    protected override string ActualOutputPath(string outputPngPath) => OutputPrefix(outputPngPath) + ".png";

    private static string OutputPrefix(string outputPngPath)
    {
        return Path.Combine(Path.GetDirectoryName(outputPngPath) ?? "", Path.GetFileNameWithoutExtension(outputPngPath));
    }
}

/// <summary>
/// Ghostscript, as the PdfThumbGenerator tool always used; the Windows developer fallback. It has no
/// render-to-width, so it renders at <see cref="PdfThumbnailOptions.Dpi"/> and the service resizes.
/// </summary>
public class GhostscriptRasteriser : ProcessPdfRasteriser
{
    public GhostscriptRasteriser(string executablePath, int dpi, int targetWidth, TimeSpan timeout, ILogger logger)
        : base(executablePath, dpi, targetWidth, timeout, logger)
    {
    }

    public override string Name => "ghostscript";

    protected override IEnumerable<string> BuildArguments(string pdfPath, string outputPngPath)
    {
        return new[]
        {
            "-q", "-dNOPAUSE", "-dBATCH", "-dSAFER",
            "-dFirstPage=1", "-dLastPage=1",
            $"-r{Dpi}",
            "-sDEVICE=png16m",
            $"-sOutputFile={outputPngPath}",
            pdfPath
        };
    }
}

/// <summary>
/// Stands in when no tool was found, so the host still starts and the failure is reported per request
/// with an actionable message instead of at startup.
/// </summary>
public class UnavailablePdfRasteriser : IPdfRasteriser
{
    public const string Message =
        "No PDF rasteriser is available: install poppler-utils (pdftoppm) or Ghostscript, " +
        "or set PdfThumbnails:ExecutablePath.";

    public string Name => "none";

    public Task RasteriseFirstPage(string pdfPath, string outputPngPath, CancellationToken cancellationToken = default)
    {
        throw new PdfRasteriseException(Message);
    }
}
