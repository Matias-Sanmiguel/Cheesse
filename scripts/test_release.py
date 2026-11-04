"""Offline checks for pinned release downloads and package completeness."""
import hashlib
import io
from pathlib import Path
import tarfile
import tempfile
import unittest
import zipfile

import release


class ReleaseTests(unittest.TestCase):
    def test_digest_rejects_changed_download(self):
        release.verify(b"official", hashlib.sha256(b"official").hexdigest())
        with self.assertRaises(ValueError):
            release.verify(b"changed", hashlib.sha256(b"official").hexdigest())

    def test_linux_archive_only_reads_expected_regular_members(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "engine.tar.gz"
            with tarfile.open(archive, "w:gz") as package:
                for name, data in [("../../outside", b"bad"),
                                   ("stockfish/stockfish-linux-x86-64-universal", b"engine"),
                                   ("stockfish/Copying.txt", b"license")]:
                    info = tarfile.TarInfo(name)
                    info.size = len(data)
                    package.addfile(info, io.BytesIO(data))
            self.assertEqual(release.engine_bytes(archive, "linux"), (b"engine", b"license"))
            self.assertEqual(list(Path(directory).iterdir()), [archive])
            with tarfile.open(archive, "w:gz") as package:
                info = tarfile.TarInfo("stockfish/stockfish-linux-x86-64-universal")
                info.type = tarfile.SYMTYPE
                info.linkname = "/etc/passwd"
                package.addfile(info)
                info = tarfile.TarInfo("stockfish/Copying.txt")
                package.addfile(info, io.BytesIO())
            with self.assertRaises(ValueError):
                release.engine_bytes(archive, "linux")

    def test_windows_archive_reads_exact_members(self):
        with tempfile.TemporaryDirectory() as directory:
            archive = Path(directory) / "engine.zip"
            with zipfile.ZipFile(archive, "w") as package:
                package.writestr("../../outside", b"bad")
                package.writestr("stockfish/stockfish-windows-x86-64-universal.exe", b"engine")
                package.writestr("stockfish/Copying.txt", b"license")
            self.assertEqual(release.engine_bytes(archive, "windows"), (b"engine", b"license"))
            self.assertEqual(list(Path(directory).iterdir()), [archive])

    def test_package_rejects_missing_runtime(self):
        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            for name in ["Cheesse.x86_64", "Cheesse.pck", "engines/stockfish",
                         "third-party/Stockfish-COPYING.txt", f"third-party/{release.SOURCE_NAME}",
                         "third-party/dotnet-LICENSE.txt", "third-party/dotnet-THIRD-PARTY-NOTICES.txt",
                         "HOW-TO-PLAY.txt"]:
                path = folder / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"data")
            with self.assertRaisesRegex(ValueError, "runtime"):
                release.check_package(folder, "linux")
            (folder / "libcoreclr.so").write_bytes(b"runtime")
            (folder / "Chess.Game.dll").write_bytes(b"assembly")
            release.check_package(folder, "linux")


if __name__ == "__main__":
    unittest.main()
