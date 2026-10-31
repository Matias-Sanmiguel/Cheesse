"""Run production Godot UI scenes with isolated saves (Python 3 + Godot .NET)."""
import argparse
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", default="godot-mono")
    parser.add_argument("--scene", choices=["MenuTests", "SidePanelTests", "MvpTests"], default="MvpTests")
    args = parser.parse_args()
    tests = Path(__file__).resolve().parent
    game = tests.parent / "chess-game"
    subprocess.run(["dotnet", "build", str(game / "Chess.Game.csproj"), "--nologo"], check=True)
    sandbox = tests / ".godot" / (args.scene + "-sandbox")
    sandbox.mkdir(parents=True, exist_ok=True)
    for directory in ("adapters", "scenes", "tests", "assets", ".godot/mono/temp/bin/Debug"):
        shutil.copytree(game / directory, sandbox / directory, dirs_exist_ok=True)
    config = (game / "project.godot").read_text()
    project_name = "Cheesse " + args.scene.removesuffix("Tests") + " Tests"
    config = config.replace('config/name="Cheesse"', f'config/name="{project_name}"')
    config = config.replace('run/main_scene="res://scenes/Main.tscn"', f'run/main_scene="res://tests/{args.scene}.tscn"')
    config = config.replace('config/icon="res://icon.svg"', '')
    (sandbox / "project.godot").write_text(config)
    subprocess.run([args.godot, "--headless", "--path", str(sandbox), "--import"], check=True, timeout=120)
    result = subprocess.run([args.godot, "--headless", "--path", str(sandbox), "--quit-after", "600"],
                            capture_output=True, text=True, timeout=120)
    print(result.stdout, end="")
    print(result.stderr, end="")
    result.check_returncode()
    expected = {"MenuTests": "menu", "SidePanelTests": "sidepanel", "MvpTests": "mvp"}[args.scene]
    if f"PASS: {expected}," not in result.stdout:
        raise RuntimeError("UI test scene did not finish")


if __name__ == "__main__":
    main()
