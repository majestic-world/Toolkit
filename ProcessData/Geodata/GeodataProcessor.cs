using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace L2Toolkit.ProcessData.Geodata;

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
            log("Nenhum arquivo geodata encontrado na pasta de entrada.");
            return results;
        }

        Directory.CreateDirectory(outputDir);
        log($"Encontrados {files.Count} arquivo(s) geodata.");

        int completed = 0;

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(filePath);
            var sourceFormat = GeoConstants.DetectFormat(filePath);

            if (sourceFormat == null)
            {
                log($"[SKIP] {fileName} - formato não reconhecido");
                results.Add(new FileResult(fileName, false, false, true, "Formato não reconhecido"));
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
                    log($"[COPY] {fileName} - já está no formato {targetFormat}");
                    results.Add(new FileResult(fileName, false, true, false));
                }
                catch (Exception ex)
                {
                    log($"[ERRO] {fileName} - falha ao copiar: {ex.Message}");
                    results.Add(new FileResult(fileName, false, false, true, ex.Message));
                }
            }
            else
            {
                try
                {
                    await Task.Run(() =>
                    {
                        log($"[READ] {fileName} ({sourceFormat})...");

                        var parser = GeodataParser.Create(sourceFormat.Value, filePath);
                        parser.Decrypt();

                        if (!parser.IsValid())
                        {
                            log($"[SKIP] {fileName} - arquivo inválido ou coordenadas fora do intervalo");
                            results.Add(new FileResult(fileName, false, false, true, "Arquivo inválido"));
                            return;
                        }

                        var region = parser.Parse();
                        var xy = parser.GetXY();
                        var outputFileName = GeoConstants.GetOutputFileName(xy[0], xy[1], targetFormat);
                        var outputPath = Path.Combine(outputDir, outputFileName);

                        log($"[CONV] {fileName} -> {outputFileName} ({targetFormat})...");

                        var writer = GeodataWriter.Create(region, targetFormat);
                        writer.WriteTo(outputPath);

                        log($"[OK]   {outputFileName} salvo com sucesso");
                        results.Add(new FileResult(fileName, true, false, false));
                    }, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log($"[ERRO] {fileName} - {ex.Message}");
                    results.Add(new FileResult(fileName, false, false, true, ex.Message));
                }
            }

            completed++;
            progress(completed, files.Count);
        }

        return results;
    }
}
