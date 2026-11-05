using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using Cheesse.Game.Adapters.Ai;
using NUnit.Framework;

namespace Chess.Tests;

[TestFixture]
public sealed class StockfishEngineInstallerTests
{
    private string _directory = null!;
    private string Destination => Path.Combine(_directory,
        OperatingSystem.IsWindows() ? "stockfish.exe" : "stockfish");

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "cheesse-stockfish-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public async Task TearDown()
    {
        // Windows can temporarily pin a freshly executed .exe (AV/indexing)
        // even after Stockfish closes. This is a runner cleanup race, not a
        // failed UCI response. Retry cleanup rather than failing the test.
        for (int attempt = 0; attempt < 25; attempt++)
        {
            try
            {
                if (Directory.Exists(_directory))
                    Directory.Delete(_directory, recursive: true);
                return;
            }
            catch (IOException) when (OperatingSystem.IsWindows() && attempt < 24) { }
            catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && attempt < 24) { }
            await Task.Delay(150);
        }
    }

    [Test]
    public void Locator_respects_override_bundled_user_cache_and_PATH()
    {
        var packaged = Path.Combine(_directory, "packaged");
        var overrideFile = Path.Combine(_directory, "override");
        var installed = Path.Combine(_directory, "stockfish.exe");
        File.WriteAllText(packaged, "packaged");
        File.WriteAllText(overrideFile, "override");
        File.WriteAllText(installed, "installed");

        Assert.That(StockfishEngineInstaller.FindExisting("stockfish.exe", null,
            [packaged, installed], null), Is.EqualTo(packaged));
        Assert.That(StockfishEngineInstaller.FindExisting("stockfish.exe", overrideFile,
            [packaged, installed], null), Is.EqualTo(overrideFile));
        Assert.That(StockfishEngineInstaller.FindExisting("stockfish.exe", null,
            [], _directory), Is.EqualTo(installed));
        Assert.That(StockfishEngineInstaller.FindExisting("stockfish.exe", null,
            [], null), Is.Null);
        Assert.Throws<FileNotFoundException>(() =>
            StockfishEngineInstaller.FindExisting("stockfish.exe",
                Path.Combine(_directory, "missing"), [packaged], null));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Verified_package_installs_only_the_expected_binary(bool zip)
    {
        var expected = "verified Stockfish binary"u8.ToArray();
        const string member = "stockfish/official-engine";
        var archive = zip ? MakeZip(member, expected) : MakeTarGz(member, expected);
        using var client = new HttpClient(new ResponseHandler(archive));
        var pkg = Package(archive, member, zip);

        var installed = await StockfishEngineInstaller.InstallAsync(Destination, pkg, client: client);
        Assert.That(installed, Is.EqualTo(Destination));
        Assert.That(File.ReadAllBytes(installed), Is.EqualTo(expected));
        Assert.That(Directory.GetFiles(_directory), Is.EquivalentTo(new[] { Destination }),
            "No ZIP/TAR archive or temporary binary can be left in user data.");

        // The cached engine is reused offline, without making an HTTP request.
        using var offline = new HttpClient(new ResponseHandler(null));
        var cached = await StockfishEngineInstaller.InstallAsync(Destination, pkg, client: offline);
        Assert.That(cached, Is.EqualTo(Destination));
    }

    [Test]
    public void Incorrect_checksum_is_rejected_and_no_executable_is_left()
    {
        var archive = MakeZip("stockfish/official-engine", "fake binary"u8.ToArray());
        using var client = new HttpClient(new ResponseHandler(archive));
        var wrong = Package(archive, "stockfish/official-engine", true)
            with { Sha256 = new string('0', 64) };
        Assert.ThrowsAsync<InvalidDataException>(async () =>
            await StockfishEngineInstaller.InstallAsync(Destination, wrong, client: client));
        Assert.That(Directory.GetFiles(_directory), Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Missing_binary_entry_is_rejected_and_cleans_up(bool zip)
    {
        var archive = zip
            ? MakeZip("stockfish/some-other-file", "not-the-engine"u8.ToArray())
            : MakeTarGz("stockfish/some-other-file", "not-the-engine"u8.ToArray());
        using var client = new HttpClient(new ResponseHandler(archive));
        Assert.ThrowsAsync<InvalidDataException>(async () =>
            await StockfishEngineInstaller.InstallAsync(Destination,
                Package(archive, "stockfish/official-engine", zip), client: client));
        Assert.That(Directory.GetFiles(_directory), Is.Empty);
    }

    [Test]
    public void Cancellation_before_download_does_not_create_engine_or_temp_files()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var archive = MakeZip("stockfish/official-engine", "engine"u8.ToArray());
        using var client = new HttpClient(new ResponseHandler(archive));
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await StockfishEngineInstaller.InstallAsync(Destination,
                Package(archive, "stockfish/official-engine", true),
                canceled.Token, client));
        Assert.That(Directory.GetFiles(_directory), Is.Empty);
    }

    [Test]
    public async Task Official_windows_archive_installs_and_plays_an_actual_move()
    {
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("CHEESSE_RUN_REAL_INSTALLER") != "1")
            Assert.Ignore("Windows release smoke test runs only on its dedicated CI job.");

        var official = StockfishEngineInstaller.OfficialPackage();
        var installed = await StockfishEngineInstaller.InstallAsync(Destination, official);
        Assert.That(File.Exists(installed), Is.True, "The official binary was installed.");
        var game = GameTestSupport.Create();
        Assert.That(game.TryMove(GameTestSupport.Move("e2", "e4")), Is.True);
        var move = await new StockfishAI(installed, 1400, 100).ChooseMoveAsync(game);
        Assert.That(game.LegalMoves(), Does.Contain(move),
            "The freshly downloaded official Windows Stockfish must reply legally.");
    }

    private static StockfishEngineInstaller.Package Package(byte[] bytes, string entry, bool zip) =>
        new("https://example.invalid/official-stockfish", Convert.ToHexString(SHA256.HashData(bytes)), entry, zip);

    private static byte[] MakeZip(string name, byte[] bytes)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(name);
            using var member = entry.Open();
            member.Write(bytes);
        }
        return stream.ToArray();
    }

    private static byte[] MakeTarGz(string name, byte[] bytes)
    {
        using var stream = new MemoryStream();
        using (var zipped = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true))
        using (var tar = new TarWriter(zipped, TarEntryFormat.Pax, leaveOpen: true))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
            {
                DataStream = new MemoryStream(bytes)
            };
            tar.WriteEntry(entry);
        }
        return stream.ToArray();
    }

    private sealed class ResponseHandler(byte[]? content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (content is null)
                throw new AssertionException("Cached engine must not start a download.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}
