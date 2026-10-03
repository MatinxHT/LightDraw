#!/usr/bin/env python3
"""Package a self-contained desktop publish directory for GitHub Releases."""

import argparse
import hashlib
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import tarfile
import tempfile
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('rid', choices=[
        'win-x64', 'win-arm64', 'osx-arm64', 'linux-x64', 'linux-arm64',
    ])
    parser.add_argument('version')
    parser.add_argument('--publish-dir', type=Path, default=Path('publish'))
    parser.add_argument('--output-dir', type=Path, default=Path('dist'))
    args = parser.parse_args()
    if not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', args.version):
        parser.error('version must have the form 0.7.3')

    root = Path(__file__).resolve().parents[1]
    executable = 'LightDraw.exe' if args.rid.startswith('win-') else 'LightDraw'
    if not (args.publish_dir / executable).is_file():
        parser.error(f'{args.publish_dir / executable} does not exist; run dotnet publish first')
    args.output_dir.mkdir(parents=True, exist_ok=True)
    name = f'LightDraw-{args.version}-{args.rid}'

    with tempfile.TemporaryDirectory(prefix='lightdraw-release-') as temporary:
        package = Path(temporary) / name
        if args.rid == 'osx-arm64':
            app = package / 'LightDraw.app'
            contents = app / 'Contents'
            payload = contents / 'MacOS'
            shutil.copytree(args.publish_dir, payload)
            resources = contents / 'Resources'
            resources.mkdir()
            iconset = Path(temporary) / 'LightDraw.iconset'
            iconset.mkdir()
            icon = root / 'src/LightDraw.Desktop/Assets/Icons/LightDraw-256.png'
            for size in (16, 32, 128, 256, 512):
                for scale in (1, 2):
                    pixels = size * scale
                    suffix = '@2x' if scale == 2 else ''
                    subprocess.run([
                        'sips', '-z', str(pixels), str(pixels), str(icon),
                        '--out', str(iconset / f'icon_{size}x{size}{suffix}.png'),
                    ], check=True, stdout=subprocess.DEVNULL)
            subprocess.run([
                'iconutil', '-c', 'icns', str(iconset),
                '-o', str(resources / 'LightDraw.icns'),
            ], check=True)
            with (contents / 'Info.plist').open('wb') as output:
                plistlib.dump({
                    'CFBundleExecutable': 'LightDraw',
                    'CFBundleIdentifier': 'club.martinphysics.lightdraw',
                    'CFBundleName': 'LightDraw',
                    'CFBundleDisplayName': 'LightDraw',
                    'CFBundlePackageType': 'APPL',
                    'CFBundleIconFile': 'LightDraw.icns',
                    'CFBundleShortVersionString': args.version,
                    'CFBundleVersion': args.version,
                    'NSHighResolutionCapable': True,
                }, output)
        else:
            payload = package
            shutil.copytree(args.publish_dir, payload)

        for document in ('LICENSE', 'README.md', 'README.en.md'):
            shutil.copy2(root / document, package / document)
        if not args.rid.startswith('win-'):
            (payload / executable).chmod(0o755)
        if args.rid == 'osx-arm64':
            # Ad-hoc signing requires no Apple account; this is not notarization.
            subprocess.run(['codesign', '--force', '--deep', '--sign', '-', str(app)], check=True)
            subprocess.run(['codesign', '--verify', '--deep', '--strict', str(app)], check=True)

        if args.rid.startswith('win-'):
            archive = args.output_dir / f'{name}.zip'
            with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED) as output:
                for item in sorted(package.rglob('*')):
                    if item.is_file():
                        output.write(item, item.relative_to(package.parent))
        else:
            archive = args.output_dir / f'{name}.tar.gz'
            with tarfile.open(archive, 'w:gz') as output:
                output.add(package, arcname=name)

    digest = hashlib.sha256()
    with archive.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(block)
    archive.with_name(archive.name + '.sha256').write_text(
        f'{digest.hexdigest()}  {archive.name}\n', encoding='utf-8',
    )
    print(f'Created {archive}')


if __name__ == '__main__':
    main()
