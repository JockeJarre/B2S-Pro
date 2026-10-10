"""Build application-local OCR from an explicitly provisioned dependency tree."""
import json
import shutil
import sys
import zipfile
from pathlib import Path

root, helpers, output, model = map(Path, sys.argv[1:5])
output.mkdir(parents=True, exist_ok=True)
for name in ('recognize.cjs', 'pdf_digits.py', 'pdf_sheet.py'):
    shutil.copy2(helpers / name, output / name)
shutil.copy2(model, output / 'eng.traineddata')
shutil.copytree(helpers / 'Licenses', output / 'Licenses', dirs_exist_ok=True)
shutil.copy2(helpers / 'README.md', output / 'README.md')

python = output / 'python'
python.mkdir(exist_ok=True)
for name in ('python.exe', 'python3.dll', 'python312.dll', 'vcruntime140.dll', 'vcruntime140_1.dll', 'LICENSE.txt'):
    shutil.copy2(root / 'python' / name, python / name)
for source in (root / 'python' / 'DLLs').iterdir():
    if source.suffix.lower() in ('.pyd', '.dll'):
        shutil.copy2(source, python / source.name)
excluded = {'site-packages', '__pycache__', 'test', 'tests', 'idlelib', 'ensurepip', 'turtledemo'}
with zipfile.ZipFile(python / 'python312.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for source in (root / 'python' / 'Lib').rglob('*.py'):
        relative = source.relative_to(root / 'python' / 'Lib')
        if not excluded.intersection(relative.parts):
            archive.write(source, relative.as_posix())
site = python / 'site-packages'
site.mkdir(exist_ok=True)
for name in ('pypdfium2', 'pypdfium2_raw', 'pypdfium2_cfg'):
    shutil.copytree(root / 'python' / 'Lib' / 'site-packages' / name, site / name,
                    ignore=shutil.ignore_patterns('__pycache__', '*.pyc'), dirs_exist_ok=True)
metadata = root / 'python' / 'Lib' / 'site-packages' / 'pypdfium2-5.13.0.dist-info'
pdf_licenses = output / 'Licenses' / 'PDFium'
pdf_licenses.mkdir(parents=True, exist_ok=True)
for source in (metadata / 'licenses').rglob('*'):
    if source.is_file():
        destination = pdf_licenses / source.name
        if destination.exists() and destination.read_bytes() != source.read_bytes():
            raise ValueError('Conflicting PDFium license filename: ' + source.name)
        shutil.copy2(source, destination)
(python / 'python312._pth').write_text('python312.zip\n.\nsite-packages\n', encoding='ascii')

node = output / 'node'
node.mkdir(parents=True, exist_ok=True)
shutil.copy2(root / 'node' / 'bin' / 'node.exe', node / 'node.exe')
modules = root / 'node' / 'node_modules'
target = node / 'node_modules'
seen = set()
def package(name):
    if name in seen:
        return
    seen.add(name)
    source = modules / name
    metadata = json.loads((source / 'package.json').read_text(encoding='utf-8'))
    shutil.copytree(source, target / name, dirs_exist_ok=True,
        ignore=shutil.ignore_patterns('test', 'tests', '__tests__', 'docs', 'examples', '.github',
                                     'bin', 'dist', 'install', 'scripts', 'src', 'coverage', '*.map'))
    # Tesseract's worker runs directly from its JavaScript source.
    if name == 'tesseract.js':
        shutil.copytree(source / 'src', target / name / 'src', dirs_exist_ok=True)
    if name in ('sharp', 'wasm-feature-detect', 'idb-keyval'):
        shutil.copytree(source / 'dist', target / name / 'runtime', dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns('*.map'))
        # These are third-party runtime modules, not local build staging.
        manifest = (target / name / 'package.json').read_text(encoding='utf-8')
        (target / name / 'package.json').write_text(manifest.replace('dist/', 'runtime/'), encoding='utf-8')
    if name == 'zlibjs':
        shutil.copytree(source / 'bin', target / name / 'runtime', dirs_exist_ok=True)
        manifest = (target / name / 'package.json').read_text(encoding='utf-8')
        (target / name / 'package.json').write_text(manifest.replace('bin/', 'runtime/'), encoding='utf-8')
    for dependency in metadata.get('dependencies', {}):
        package(dependency)
    for dependency in metadata.get('optionalDependencies', {}):
        if (modules / dependency / 'package.json').exists():
            package(dependency)
for name in ('tesseract.js', 'sharp'):
    package(name)
print(json.dumps({'packages': sorted(seen), 'files': len(list(output.rglob('*')))}, indent=2))
