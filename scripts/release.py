#!/usr/bin/env python3
"""Fetch pinned official engines/templates and export self-contained desktop releases."""
import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tarfile
import urllib.request
import zipfile
import zlib

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / ".release-cache"
PROJECT = ROOT / "chess-game"
SF_COMMIT = "edb0d9db6731067ec50ce619ff372b463bc4dd5d"
SF_BASE = "https://github.com/official-stockfish/Stockfish/releases/download/sf_19/"
ENGINES = {
    "linux": ("stockfish-linux-x86-64-universal.tar.gz", "9defc0d4e55d49c65a6d042f3e571a39fcea499ade6dbe741b53b8c65e03611f", "stockfish"),
    "windows": ("stockfish-windows-x86-64-universal.zip", "3c8bf1f9ea66a09350a40df4f632288285ac206d99f33ab5842c408fc30b48a7", "stockfish.exe"),
}
SOURCE_NAME = f"Stockfish-{SF_COMMIT}.tar.gz"
SOURCE_URL = f"https://codeload.github.com/official-stockfish/Stockfish/tar.gz/{SF_COMMIT}"
SOURCE_SHA = "4fc20699a5ecf7883b036ce180020f5e57979623ad719fd2a52839209521581b"
TEMPLATE_URL = "https://github.com/godotengine/godot-builds/releases/download/4.7.2-stable/Godot_v4.7.2-stable_mono_export_templates.tpz"
# ZIP member header offsets, compressed lengths, and SHA256 of uncompressed official templates.
# Only these members are transferred, rather than the entire 1.2 GB template archive.
TEMPLATES = {
    "linux_release.x86_64": (843081558, 28367625, "d776f83ae09d82cfa1295fb3d70ad7ab8a77fe66a4cc4f9d42244fae3665d8cc"),
    "windows_release_x86_64.exe": (1164599829, 37995718, "16e0ec3cfd398b938f514de95ff2474b532b5061ada78f92226dba3f7584d55d"),
    "windows_release_x86_64_console.exe": (1164523268, 76459, "7895a7873947deaff6c8c859c1f77be051023413035048e2db74ceba07a65017"),
}
TEMPLATE_DIR = CACHE / "godot-mono-4.7.2"


def verify(data, expected):
    actual = hashlib.sha256(data).hexdigest()
    if actual != expected:
        raise ValueError(f"SHA256 mismatch: expected {expected}, got {actual}")


def cached(path, expected):
    if not path.exists():
        return False
    verify(path.read_bytes(), expected)
    return True


def download(url, name, expected):
    path = CACHE / name
    if cached(path, expected):
        return path
    print(f"Downloading {name}", flush=True)
    partial = path.with_suffix(path.suffix + ".part")
    try:
        with urllib.request.urlopen(url, timeout=90) as response, partial.open("wb") as output:
            received = 0
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
                received += len(chunk)
                if received % (16 * 1024 * 1024) == 0:
                    print(f"  {received // (1024 * 1024)} MiB", flush=True)
        verify(partial.read_bytes(), expected)
        partial.replace(path)
    finally:
        partial.unlink(missing_ok=True)
    return path


def byte_range(url, start, end):
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"Range": f"bytes={start}-{end}"})
            with urllib.request.urlopen(request, timeout=90) as response:
                if response.status != 206 or response.headers.get("Content-Range", "").split("/")[0] != f"bytes {start}-{end}":
                    raise ValueError("Server did not honor the requested byte range")
                data = response.read(end - start + 2)
                if len(data) != end - start + 1:
                    raise ValueError("Incomplete byte range")
                return data
        except (OSError, ValueError):
            if attempt == 2:
                raise


def fetch_template(name):
    offset, compressed_size, expected = TEMPLATES[name]
    path = TEMPLATE_DIR / name
    if cached(path, expected):
        return path
    print(f"Downloading Godot .NET template: {name}", flush=True)
    header = byte_range(TEMPLATE_URL, offset, offset + 29)
    if header[:4] != b"PK\x03\x04" or struct.unpack_from("<H", header, 8)[0] != 8:
        raise ValueError("Unexpected template ZIP header")
    name_length, extra_length = struct.unpack_from("<HH", header, 26)
    if name_length + extra_length > 4096:
        raise ValueError("Unexpected template ZIP metadata length")
    start = offset + 30 + name_length + extra_length
    ranges = [(i, min(i + 1024 * 1024 - 1, start + compressed_size - 1))
              for i in range(start, start + compressed_size, 1024 * 1024)]
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
        packed = b"".join(executor.map(lambda bounds: byte_range(TEMPLATE_URL, *bounds), ranges))
    data = zlib.decompress(packed, -15)
    verify(data, expected)
    path.write_bytes(data)
    path.chmod(0o755)
    return path


def engine_bytes(archive, platform):
    """Read two exact members, never extract archive paths into the filesystem."""
    name = ENGINES[platform][0]
    binary = "stockfish/" + name.removesuffix(".tar.gz").removesuffix(".zip")
    if platform == "windows":
        binary += ".exe"
        with zipfile.ZipFile(archive) as package:
            return package.read(binary), package.read("stockfish/Copying.txt")
    with tarfile.open(archive) as package:
        members = [package.getmember(p) for p in (binary, "stockfish/Copying.txt")]
        if not all(member.isfile() for member in members):
            raise ValueError("Expected regular engine and license archive members")
        return tuple(package.extractfile(member).read() for member in members)


def install_engine(platform):
    name, digest, executable = ENGINES[platform]
    archive = download(SF_BASE + name, name, digest)
    binary, license_text = engine_bytes(archive, platform)
    engine_dir = PROJECT / "engines"
    engine_dir.mkdir(exist_ok=True)
    path = engine_dir / executable
    path.write_bytes(binary)
    path.chmod(0o755)
    expected_license = (ROOT / "third-party/Stockfish-COPYING.txt").read_bytes()
    if license_text != expected_license:
        raise ValueError("Engine license differs from the checked-in upstream license")
    return path


def write_presets():
    presets = []
    for i, platform in enumerate(ENGINES):
        template = "linux_release.x86_64" if platform == "linux" else "windows_release_x86_64.exe"
        presets.append(f'''[preset.{i}]
name="{platform}"
platform="{'Linux' if platform == 'linux' else 'Windows Desktop'}"
runnable=true
advanced_options=true
export_filter="all_resources"
include_filter=""
exclude_filter="engines/*"
export_path=""
encryption_include_filters=""
encryption_exclude_filters=""
encrypt_pck=false
encrypt_directory=false

[preset.{i}.options]
custom_template/release={json.dumps(str(TEMPLATE_DIR / template))}
binary_format/architecture="x86_64"
binary_format/embed_pck=false
texture_format/s3tc_bptc=true
texture_format/etc2_astc=false
debug/export_console_wrapper=0
application/modify_resources=false
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
dotnet/embed_build_outputs=false
''')
    (PROJECT / "export_presets.cfg").write_text("\n".join(presets))


def check_package(folder, platform):
    executable = folder / ("Cheesse.x86_64" if platform == "linux" else "Cheesse.exe")
    required = [executable, folder / "Cheesse.pck", folder / "engines" / ENGINES[platform][2],
                folder / "third-party/Stockfish-COPYING.txt", folder / "third-party" / SOURCE_NAME,
                folder / "third-party/dotnet-LICENSE.txt", folder / "third-party/dotnet-THIRD-PARTY-NOTICES.txt",
                folder / "HOW-TO-PLAY.txt"]
    for path in required:
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError(f"Missing or empty release file: {path}")
    runtime = "libcoreclr.so" if platform == "linux" else "coreclr.dll"
    if not list(folder.rglob(runtime)) or not list(folder.rglob("Chess.Game.dll")):
        raise ValueError(f"Missing bundled .NET runtime or game assembly in {folder}")


def build(platform, godot, source):
    folder = ROOT / "dist" / f"Cheesse-{platform}-x64"
    # Clean only this generated platform directory, preventing stale runtime files in a release.
    if folder.exists():
        shutil.rmtree(folder)
    folder.mkdir(parents=True)
    executable = folder / ("Cheesse.x86_64" if platform == "linux" else "Cheesse.exe")
    subprocess.run([godot, "--headless", "--path", str(PROJECT), "--export-release", platform, str(executable)], check=True)
    (folder / "engines").mkdir()
    shutil.copy2(PROJECT / "engines" / ENGINES[platform][2], folder / "engines")
    shutil.copytree(ROOT / "third-party", folder / "third-party")
    shutil.copy2(source, folder / "third-party")
    deps = json.loads(next(folder.rglob("Chess.Game.deps.json")).read_text())
    runtime_pack = next(name for name in deps["libraries"] if name.startswith("runtimepack.Microsoft.NETCore.App.Runtime."))
    package_name, version = runtime_pack.removeprefix("runtimepack.").split("/")
    nuget = Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages")))
    package = nuget / package_name.lower() / version
    for source_name, target_name in [("LICENSE.TXT", "dotnet-LICENSE.txt"), ("THIRD-PARTY-NOTICES.TXT", "dotnet-THIRD-PARTY-NOTICES.txt")]:
        shutil.copy2(package / source_name, folder / "third-party" / target_name)
    launch = "Ejecutá ./Cheesse.x86_64 en esta carpeta." if platform == "linux" else "Abrí Cheesse.exe con doble clic."
    (folder / "HOW-TO-PLAY.txt").write_text(
        f"Cheesse — {platform} x86-64\n\n{launch}\nConservá toda la carpeta. No necesitás instalar Godot ni .NET.\n\n"
        "Elegí humano contra humano o Stockfish (jugás con blancas), nivel y reloj.\n"
        "Para mover, hacé clic en la pieza y después en su destino. Ctrl+Z deshace.\n"
        "El panel permite guardar/cargar con relojes, abandonar, acordar o reclamar tablas, e importar/exportar FEN y PGN.\n"
        "Los archivos importados abren en modo humano, sin reloj. El historial muestra las jugadas en SAN.\n"
        "Los relojes se pausan en el menú, los diálogos y al revisar el historial.\n"
        "Stockfish 19 y su código fuente/licencias están incluidos. No requiere conexión para jugar.\n"
        "Las partidas se guardan en la carpeta de datos del usuario de Godot.\n", encoding="utf-8")
    check_package(folder, platform)
    print(f"Package verified: {folder}", flush=True)
    archive_format = "gztar" if platform == "linux" else "zip"
    archive = shutil.make_archive(str(folder), archive_format, root_dir=folder.parent, base_dir=folder.name)
    print(f"Archive ready: {archive}", flush=True)
    return folder


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=["all", *ENGINES], default="all")
    parser.add_argument("--godot", default=os.environ.get("GODOT", "godot-mono"))
    parser.add_argument("--engines-only", action="store_true", help="install Stockfish for editor use; no Godot required")
    parser.add_argument("--prepare-only", action="store_true", help="prepare downloads and generated presets without exporting")
    args = parser.parse_args()
    CACHE.mkdir(exist_ok=True)
    platforms = list(ENGINES) if args.platform == "all" else [args.platform]
    for platform in platforms:
        install_engine(platform)
    if args.engines_only:
        return
    source = download(SOURCE_URL, SOURCE_NAME, SOURCE_SHA)
    TEMPLATE_DIR.mkdir(exist_ok=True)
    for name in TEMPLATES:
        if name.startswith(tuple(platforms)):
            fetch_template(name)
    write_presets()
    if not args.prepare_only:
        for platform in platforms:
            build(platform, args.godot, source)


if __name__ == "__main__":
    main()
