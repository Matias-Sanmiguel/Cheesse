using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace Cheesse.Game.Adapters.Ai;

/// <summary>
/// Finds a player-installed or release-bundled engine, and if necessary
/// installs an official, checksum-verified Stockfish executable in user data.
/// No executable, archive, or generated file is committed to the repository.
/// </summary>
public static class StockfishEngineInstaller
{
    // Keep these pinned official Stockfish 19 release hashes in sync with
    // scripts/release.py. Do not trust an unverified downloaded executable.
    private const string BaseUrl =
        "https://github.com/official-stockfish/Stockfish/releases/download/sf_19/";
    private static readonly SemaphoreSlim InstallGate = new(1, 1);
    private static readonly HttpClient DownloadClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
    private const long MaximumArchiveBytes = 512L * 1024 * 1024;

    public sealed record Package(string Url, string Sha256, string EntryName, bool Zip);

    public static Package OfficialPackage()
    {
        if (OperatingSystem.IsWindows())
            return new Package(
                BaseUrl + "stockfish-windows-x86-64-universal.zip",
                "3c8bf1f9ea66a09350a40df4f632288285ac206d99f33ab5842c408fc30b48a7",
                "stockfish/stockfish-windows-x86-64-universal.exe", true);
        if (OperatingSystem.IsLinux())
            return new Package(
                BaseUrl + "stockfish-linux-x86-64-universal.tar.gz",
                "9defc0d4e55d49c65a6d042f3e571a39fcea499ade6dbe741b53b8c65e03611f",
                "stockfish/stockfish-linux-x86-64-universal", false);
        throw new NotSupportedException(
            "La instalación automática de Stockfish admite Windows y Linux x86-64. " +
            "Podés elegir un motor instalado mediante CHEESSE_STOCKFISH_PATH.");
    }

    /// <summary>
    /// Respect an explicit user override, then packaged/project/user binaries,
    /// then an already installed Stockfish in PATH. No network access here.
    /// </summary>
    public static string? FindExisting(
        string name, string? overridePath, IEnumerable<string> candidates, string? systemPath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var configured = Path.GetFullPath(overridePath.Trim().Trim('"'));
            if (!File.Exists(configured))
                throw new FileNotFoundException(
                    "CHEESSE_STOCKFISH_PATH apunta a un motor que no existe: " + configured,
                    configured);
            return configured;
        }
        foreach (var path in candidates)
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;

        foreach (var directory in (systemPath ?? "").Split(
                     Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Downloads only when needed, checks the SHA-256 of the *entire* official
    /// archive, extracts one exact entry and atomically installs it. Temp files
    /// are deleted on failure or cancellation. An injected client/package lets
    /// tests exercise the full install without downloading or running a binary.
    /// </summary>
    public static async Task<string> InstallAsync(
        string destination, Package package, CancellationToken cancellationToken = default,
        HttpClient? client = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(package);
        await InstallGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(destination)) return destination;
            string dir = Path.GetDirectoryName(destination)
                ?? throw new ArgumentException("La ruta del motor debe tener carpeta.", nameof(destination));
            Directory.CreateDirectory(dir);

            string archivePath = Path.Combine(dir, ".stockfish-" + Guid.NewGuid().ToString("N") + ".download");
            string executablePath = Path.Combine(dir, ".stockfish-" + Guid.NewGuid().ToString("N") + ".temporary");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, package.Url);
                using var response = await (client ?? DownloadClient).SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false))
                await using (var output = new FileStream(archivePath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, cancellationToken)
                               .ConfigureAwait(false)) != 0)
                    {
                        total += read;
                        if (total > MaximumArchiveBytes)
                            throw new InvalidDataException("El archivo de Stockfish supera el tamaño permitido.");
                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                string actual = Convert.ToHexString(hash.GetHashAndReset());
                if (!actual.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "La firma SHA-256 de la descarga de Stockfish no coincide con la oficial.");

                await ExtractBinaryAsync(archivePath, executablePath, package, cancellationToken)
                    .ConfigureAwait(false);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(executablePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(destination)) return destination;
                File.Move(executablePath, destination);
                return destination;
            }
            finally
            {
                if (File.Exists(archivePath)) File.Delete(archivePath);
                if (File.Exists(executablePath)) File.Delete(executablePath);
            }
        }
        finally
        {
            InstallGate.Release();
        }
    }

    private static async Task ExtractBinaryAsync(
        string archivePath, string outputPath, Package package, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(outputPath, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 81920, useAsync: true);
        if (package.Zip)
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.GetEntry(package.EntryName);
            if (entry is null || entry.Length == 0)
                throw new InvalidDataException("El ZIP oficial no contiene el ejecutable esperado.");
            await using var input = entry.Open();
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var file = File.OpenRead(archivePath);
        await using var compressed = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(compressed);
        TarEntry? entryFromTar;
        while ((entryFromTar = tar.GetNextEntry()) is not null)
        {
            if (entryFromTar.Name != package.EntryName) continue;
            if (entryFromTar.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) ||
                entryFromTar.DataStream is null)
                throw new InvalidDataException("El TAR oficial contiene una entrada inválida.");
            await entryFromTar.DataStream.CopyToAsync(output, cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        throw new InvalidDataException("El TAR oficial no contiene el ejecutable esperado.");
    }
}
