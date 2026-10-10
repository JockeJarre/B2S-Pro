# Bundled digit-sheet recognition

The release build includes the OCR engine, English digit model, PDF reader,
and their application-local runtimes in each Designer architecture folder.
Setup installs these files automatically. Importing a sheet does not download
tools, install Python or Node globally, change PATH, or send artwork online.
The new sheet importer requires 64-bit Windows 10 or later; both the x86 and
x64 Designer can use its separate 64-bit helper processes. Existing individual
image imports and legacy backglasses retain their previous requirements.

Recognition locates digit artwork; the Designer crops the original rendered
pixels, shows the previews, and saves only after the user confirms them.
Credit sheets generate values zero through the selected maximum (1–99).
Zero is retained as the credit-reset image.

## Release build inputs

`tools/Build-CurrentRelease.ps1` accepts `OcrDependencyRoot` and `OcrModelPath`.
Provision dependencies outside the repository. The dependency root contains
`python/python.exe`, its `Lib` and `DLLs`, and `node/bin/node.exe` with
`node/node_modules`. `Build-OcrRuntime.py` copies the required packages and
standard library into external release staging; it excludes development tests
and does not copy the complete dependency environment.

The verified inputs are CPython 3.12.14, Node 24.19.0, pypdfium2 5.13.0,
Tesseract.js/core 7.0.0, Sharp 0.35.5 (libvips 8.18.7), and the English
`tessdata_fast` model. Python uses an isolated `_pth` file and does not search
user-installed packages. Helpers use paths relative to the Designer.

## Third-party licenses and source

These components retain their own licenses, separate from the B2S license.
Their notices accompany the runtime files. Node's complete license notices,
the English model's Apache license, and GPL/LGPL texts are in `Licenses`.
The Python license, PDFium's component notices, and npm package notices are
also copied into the runtime package.

- [CPython source and PSF license](https://github.com/python/cpython/tree/v3.12.14)
- [Node source and license notices](https://github.com/nodejs/node/tree/v24.19.0)
- [pypdfium2 source and licensing](https://github.com/pypdfium2-team/pypdfium2)
- [PDFium source and BSD notices](https://pdfium.googlesource.com/pdfium/)
- [Tesseract.js source, Apache 2.0](https://github.com/naptha/tesseract.js)
- [Tesseract.js core source](https://github.com/naptha/tesseract.js-core)
- [English model source, Apache 2.0](https://github.com/tesseract-ocr/tessdata_fast)
- [Sharp 0.35.5 source, Apache 2.0](https://github.com/lovell/sharp/tree/v0.35.5)
- [libvips 8.18.7 source, LGPL 2.1 or later](https://github.com/libvips/libvips/tree/v8.18.7)
- [Prebuilt libvips build recipes and dependency sources](https://github.com/lovell/sharp-libvips)

The libvips DLLs remain separate and replaceable. Runtime npm manifests and
the Sharp native package's `versions.json` identify their bundled dependencies.
