using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace L2Toolkit.Utilities;

/// <summary>Release mais nova do GitHub com o instalador do Windows e o que mostrar ao usuário antes de atualizar.</summary>
public sealed record AppRelease(
    string Tag, Version Version, string Name, string Notes, DateTimeOffset? PublishedAt,
    string PageUrl, string InstallerUrl, long InstallerSize, string? Sha256);

public enum UpdateStatus
{
    UpToDate,
    Available,
    /// <summary>Anti-flood: a última consulta foi há menos de <see cref="AppUpdater.MinInterval"/>.</summary>
    Throttled,
    Failed,
}

public sealed record UpdateCheck(UpdateStatus Status, AppRelease? Release = null, TimeSpan Wait = default, string? Error = null);

/// <summary>
/// Atualização pelo GitHub Releases de majestic-world/Toolkit: compara a tag da release mais
/// nova com a versão do app (APP_VERSION), baixa o instalador do Inno Setup, confere o
/// SHA-256 publicado pelo GitHub e abre o assistente do instalador.
/// </summary>
public static class AppUpdater
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(10);

    private const string LatestReleaseUrl = "https://api.github.com/repos/majestic-world/Toolkit/releases/latest";

    private static readonly HttpClient Http = CreateClient();
    private static readonly Lock Gate = new();
    private static long _lastCheckTicks = long.MinValue;
    private static Task<UpdateCheck>? _running;

    public static Version CurrentVersion { get; } = ParseVersion(
        typeof(AppUpdater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion) ?? new Version(0, 0);

    /// <summary>
    /// Consulta a release mais nova. No máximo uma consulta a cada <see cref="MinInterval"/>;
    /// chamadas durante uma consulta em andamento recebem o mesmo resultado.
    /// </summary>
    public static Task<UpdateCheck> CheckAsync()
    {
        lock (Gate)
        {
            if (_running is { IsCompleted: false }) return _running;
            var elapsed = Stopwatch.GetElapsedTime(_lastCheckTicks);
            if (_lastCheckTicks != long.MinValue && elapsed < MinInterval)
                return Task.FromResult(new UpdateCheck(UpdateStatus.Throttled, Wait: MinInterval - elapsed));
            _lastCheckTicks = Stopwatch.GetTimestamp();
            return _running = FetchAsync();
        }
    }

    /// <summary>
    /// Apaga instaladores e downloads parciais de atualizações anteriores. Melhor esforço: o
    /// instalador que acabou de reabrir o app pode ainda estar fechando e segurar o arquivo.
    /// </summary>
    public static void CleanupDownloads()
    {
        if (!Directory.Exists(DownloadFolder)) return;
        foreach (var file in Directory.EnumerateFiles(DownloadFolder, "L2 Toolkit Installer *"))
        {
            try { File.Delete(file); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string DownloadFolder => Path.Combine(Path.GetTempPath(), "L2Toolkit");

    /// <summary>Baixa o instalador para %TEMP%\L2Toolkit e confere tamanho e SHA-256.</summary>
    public static async Task<string> DownloadAsync(AppRelease release, IProgress<(long Done, long Total)> progress, CancellationToken token)
    {
        Directory.CreateDirectory(DownloadFolder);
        var path = Path.Combine(DownloadFolder, $"L2 Toolkit Installer {release.Tag}.exe");
        var partial = path + ".part";

        using (var response = await Http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.InstallerSize;
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var target = File.Create(partial);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), token);
                hash.AppendData(buffer, 0, read);
                done += read;
                progress.Report((done, total));
            }

            if (done != release.InstallerSize)
                throw new InvalidDataException($"Download incompleto: {done} de {release.InstallerSize} bytes.");
            var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
            if (release.Sha256 != null && !string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O instalador baixado não confere com o SHA-256 da release.");
        }

        File.Move(partial, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// Abre o assistente do instalador, igual à instalação manual: termina na página final com
    /// "Abrir L2 Toolkit" (entrada postinstall do [Run] do Setup.iss). /CLOSEAPPLICATIONS fecha
    /// o que ainda segurar os arquivos; o chamador fecha o app logo em seguida.
    /// </summary>
    public static void LaunchInstaller(string installerPath)
        => Process.Start(new ProcessStartInfo(installerPath, "/SP- /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true,
        });

    public static void OpenReleasePage(AppRelease release)
        => Process.Start(new ProcessStartInfo(release.PageUrl) { UseShellExecute = true });

    private static async Task<UpdateCheck> FetchAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response = await Http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var root = json.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var version = ParseVersion(tag) ?? throw new InvalidDataException($"Tag da release fora do formato de versão: \"{tag}\".");
            if (version <= CurrentVersion) return new UpdateCheck(UpdateStatus.UpToDate);

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
                var sha256 = digest?.StartsWith("sha256:", StringComparison.Ordinal) == true ? digest["sha256:".Length..] : null;
                var published = root.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetDateTimeOffset()
                    : (DateTimeOffset?)null;
                var release = new AppRelease(tag, version,
                    root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
                    published,
                    root.GetProperty("html_url").GetString() ?? "",
                    asset.GetProperty("browser_download_url").GetString() ?? "",
                    asset.GetProperty("size").GetInt64(),
                    sha256);
                return new UpdateCheck(UpdateStatus.Available, release);
            }
            throw new InvalidDataException($"A release {tag} não tem instalador (.exe).");
        }
        catch (Exception ex)
        {
            return new UpdateCheck(UpdateStatus.Failed, Error: ex.Message);
        }
    }

    /// <summary>"3.8", "v3.8.1" → versão com as partes ausentes zeradas (3.8 == 3.8.0).</summary>
    private static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !Version.TryParse(text.Trim().TrimStart('v', 'V'), out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }

    private static HttpClient CreateClient()
    {
        // Sem Timeout global: o download do instalador pode demorar; a consulta à API tem o seu.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        // A API do GitHub recusa requisições sem User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"L2Toolkit/{typeof(AppUpdater).Assembly.GetName().Version}");
        return client;
    }
}
