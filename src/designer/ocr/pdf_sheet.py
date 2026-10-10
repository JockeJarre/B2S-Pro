import json
import math
import struct
import sys

import pypdfium2 as pdfium

# Application-local PDF rendering; no Pillow or system Python installation.
with pdfium.PdfDocument(sys.argv[2]) as document:
    if sys.argv[1] == "count":
        print(len(document))
    elif sys.argv[1] == "render":
        page = document[int(sys.argv[3]) - 1]
        try:
            width, height = page.get_size()
            scale = min(300 / 72, 6000 / max(width, height))
            bitmap = page.render(scale=scale, force_bitmap_format=pdfium.raw.FPDFBitmap_BGRx)
            try:
                pixels = bytes(bitmap.buffer)
                header = struct.pack('<2sIHHI', b'BM', 54 + len(pixels), 0, 0, 54)
                # Negative height denotes the top-down PDFium row order.
                header += struct.pack('<IiiHHIIiiII', 40, bitmap.width, -bitmap.height,
                                      1, 32, 0, len(pixels), 11811, 11811, 0, 0)
                with open(sys.argv[4], 'wb') as output:
                    output.write(header)
                    output.write(pixels)
            finally:
                bitmap.close()
        finally:
            page.close()
    else:
        raise ValueError('Unknown PDF sheet operation')
