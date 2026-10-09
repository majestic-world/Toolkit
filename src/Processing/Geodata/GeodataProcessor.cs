using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using L2Toolkit.Localization;

namespace L2Toolkit.Processing.Geodata;

public class GeodataProcessor
{
    public record FileResult(string FileName, bool Converted, bool Copied, bool Failed, string? Error = null);

    private static readonly string[] SupportedExtensions =
        [".l2j", "_conv.dat", ".l2d", ".l2s", ".l2g", ".l2m", ".rp", "_path.txt"];

    public static List<string> FindGeodataFiles(string inputDir)
    {
        if (!Directory.Exists(inputDir)) return [];

        return Directory.GetFiles(inputDir)
            .Where(f => GeoConstants.DetectFormat(f) != null)
            .ToList();
    }

    public static async Task<List<FileResult>> ConvertAsync(
        string inputDir,
        string outputDir,
        GeodataFormat targetFormat,
        Action<string> log,
        Action<int, int> progress,
        CancellationToken ct = default)
    {
        var files = FindGeodataFiles(inputDir);
        var results = new List<FileResult>();

        if (files.Count == 0)
        {
            log(Loc.Geodata.NoFilesLog);
            return results;
        }

        Directory.CreateDirectory(outputDir);
        log(Loc.Geodata.FoundLog(files.Count));

        int completed = 0;

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            var sourceFormat = GeoConstants.DetectFormat(filePath);

            if (sourceFormat == null)
            {
                log(Loc.Geodata.SkipUnknownLog(fileName));
                results.Add(new FileResult(fileName, false, false, true, Loc.Geodata.UnknownFormatError));
                completed++;
                progress(completed, files.Count);
                continue;
            }

            if (sourceFormat == targetFormat)
            {
                var destPath = Path.Combine(outputDir, fileName);
                try
                {
                    await Task.Run(() => File.Copy(filePath, destPath, true), ct);
                    log(Loc.Geodata.CopyLog(fileName, targetFormat));
                    results.Add(new FileResult(fileName, false, true, false));
                }
                catch (Exception ex)
                {
                    log(Loc.Geodata.CopyFailedLog(fileName, ex.Message));
                    results.Add(new FileResult(fileName, false, false, true, ex.Message));
                }
            }
            else
            {
                try
                {
                    await Task.Run(() =>
                    {
                        log(Loc.Geodata.ReadLog(fileName, sourceFormat));

                        var parser = GeodataParser.Create(sourceFormat.Value, filePath);
                        parser.Decrypt();

                        if (!parser.IsValid())
                        {
                            log(Loc.Geodata.InvalidFileLog(fileName));
                            results.Add(new FileResult(fileName, false, false, true, Loc.Geodata.InvalidFileError));
                            return;
                        }

                        var region = parser.Parse();
                        var xy = parser.GetXY();
                        var outputFileName = GeoConstants.GetOutputFileName(xy[0], xy[1], targetFormat);
                        var outputPath = Path.Combine(outputDir, outputFileName);

                        log(Loc.Geodata.ConvLog(fileName, outputFileName, targetFormat));

                        var writer = GeodataWriter.Create(region, targetFormat);
                        writer.WriteTo(outputPath);

                        log(Loc.Geodata.SavedLog(outputFileName));
                        results.Add(new FileResult(fileName, true, false, false));
                    }, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log(Loc.Geodata.ErrorLog(fileName, ex.Message));
                    results.Add(new FileResult(fileName, false, false, true, ex.Message));
                }
            }

            completed++;
            progress(completed, files.Count);
        }

        return results;
    }
}
