"""Genera `focusbar.ico` de AuTaskBar desde el SVG maestro.

Por qué así: el `.ico` se generó una vez de forma ad hoc y quedó con el anillo
del hexágono negro. Ahora el maestro es `AuTaskBar/Assets/focusbar.svg` y el
`.ico` se rasteriza desde él: el SVG es la única fuente de la forma y del
color; el binario no se edita a mano, se regenera.

Cómo se rasteriza, y por qué no es un simple `get_pixmap`:
  - PyMuPDF traza el SVG con antialiasing, pero su parser SVG **no soporta
    gradientes**: cualquier `url(#…)` se pinta negro (comprobado con 1.28.2).
    El anillo del hexágono salía negro en vez del degradado violeta→menta.
  - Por eso el script renderiza con PyMuPDF las capas sólidas (fondo redondeado,
    barra, punto) y la máscara del anillo, y compone con Pillow el gradiente
    declarado en el propio maestro (sus stops y su vector se leen del SVG).
    Técnica de `glpi-companion` (W56-B) y `aufocus-ui` (W57).
  - Se rasteriza a 4× y se reduce con Lanczos: AA limpio también a 16 px.

El `.ico` conserva los 7 tamaños del anterior: 16, 24, 32, 48, 64, 128 y 256.

Uso:
    python scripts/generate_focusbar_ico.py

Requiere Pillow y PyMuPDF.
"""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from PIL import Image

try:
    import pymupdf
except ImportError:  # pragma: no cover
    sys.exit(
        "Falta PyMuPDF. Instálalo con: pip install pymupdf\n"
        "(Se usa para rasterizar el SVG maestro.)"
    )

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / 'AuTaskBar' / 'Assets'
SVG_MASTER = ASSETS / 'focusbar.svg'
ICO_TARGET = ASSETS / 'focusbar.ico'

# Lado del viewBox del maestro.
VIEWBOX = 64.0
# Escala de render: se rasteriza a 4× y se reduce con Lanczos.
SUPERSAMPLE = 4
# Resolución con la que se mide la caja de geometría del trazo (ver geometry_bbox).
BBOX_RENDER = 1024

# Tamaños que embebe el .ico (los mismos del binario anterior).
ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)


def render(svg_text, size):
    """Rasteriza un SVG en memoria a un PNG RGBA del lado pedido."""
    with pymupdf.open(stream=svg_text.encode('utf-8'), filetype='svg') as document:
        page = document[0]
        matrix = pymupdf.Matrix(size / VIEWBOX, size / VIEWBOX)
        pixmap = page.get_pixmap(matrix=matrix, alpha=True)
    return Image.frombytes('RGBA', (pixmap.width, pixmap.height), pixmap.samples)


def find_gradient_stroke(svg_text):
    """Lee del maestro el elemento con trazo `url(#…)` y su gradiente.

    Devuelve `(elemento, stops, vector, unidades)` o `None` si el maestro no
    declara ningún trazo con gradiente. Este script no inventa colores: los
    stops y el vector salen del linearGradient del propio SVG.
    """
    root = ET.fromstring(svg_text)
    namespace = root.tag[: root.tag.index('}') + 1] if root.tag.startswith('{') else ''

    stroke_element = None
    reference = None
    for node in root.iter():
        match = re.fullmatch(r'url\(#([^)]+)\)', node.get('stroke', ''))
        if match:
            if stroke_element is not None:
                raise SystemExit(
                    'el maestro declara más de un trazo con gradiente; '
                    'este script asume uno solo'
                )
            stroke_element, reference = node, match.group(1)
    if stroke_element is None:
        return None

    gradient = next(
        (node for node in root.iter(f'{namespace}linearGradient')
         if node.get('id') == reference),
        None,
    )
    if gradient is None:
        raise SystemExit(
            f'el trazo usa url(#{reference}) pero el maestro no declara ese linearGradient'
        )

    stops = []
    for stop in gradient.findall(f'{namespace}stop'):
        if float(stop.get('stop-opacity', '1')) < 1.0:
            raise SystemExit('stop-opacity aún no soportado; el gradiente debe ser opaco')
        offset = stop.get('offset', '0')
        position = float(offset[:-1]) / 100.0 if offset.endswith('%') else float(offset)
        color = stop.get('stop-color', '#000000').strip().lstrip('#')
        if len(color) == 3:
            color = ''.join(channel * 2 for channel in color)
        stops.append((position, tuple(int(color[i:i + 2], 16) for i in (0, 2, 4))))
    stops.sort()

    vector = tuple(
        float(gradient.get(key, default))
        for key, default in (('x1', '0'), ('y1', '0'), ('x2', '1'), ('y2', '0'))
    )
    units = gradient.get('gradientUnits', 'objectBoundingBox')
    return stroke_element, stops, vector, units


def shape_svg(element, paint):
    """SVG mínimo con la geometría del elemento del maestro, pintada en negro.

    `paint='stroke'` conserva el trazo (máscara del anillo); `paint='fill'`
    rellena la silueta (caja de geometría). Los atributos de forma (`d`,
    `points`, `transform`, …) se copian tal cual del maestro: el SVG sigue
    siendo la única fuente de la geometría.
    """
    tag = element.tag.split('}')[-1]
    attributes = dict(element.attrib)
    attributes['fill'] = '#000000' if paint == 'fill' else 'none'
    attributes['stroke'] = '#000000' if paint == 'stroke' else 'none'
    attributes = ' '.join(f'{key}="{value}"' for key, value in attributes.items())
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" '
        f'viewBox="0 0 {VIEWBOX:g} {VIEWBOX:g}"><{tag} {attributes}/></svg>'
    )


def geometry_bbox(element):
    """Caja de geometría del elemento (unidades del viewBox), sin trazo.

    El `objectBoundingBox` que usa el gradiente abarca la geometría del
    elemento, no el trazo: se mide la silueta rellena a alta resolución. Así no
    hay que interpretar el atributo `d` del path ni asumir que la forma sigue
    siendo un polígono.
    """
    silhouette = render(shape_svg(element, 'fill'), BBOX_RENDER).getchannel('A')
    x0, y0, x1, y1 = silhouette.getbbox()
    scale = VIEWBOX / BBOX_RENDER
    return (x0 * scale, y0 * scale, x1 * scale, y1 * scale)


def gradient_lut(stops):
    """Tabla de 256 colores interpolando los stops del maestro."""
    if len(stops) == 1:
        return [stops[0][1]] * 256
    table = []
    for i in range(256):
        t = i / 255.0
        color = stops[-1][1]
        for (p0, c0), (p1, c1) in zip(stops, stops[1:]):
            if t <= p1:
                factor = 0.0 if p1 == p0 else (t - p0) / (p1 - p0)
                color = tuple(round(a + (b - a) * factor) for a, b in zip(c0, c1))
                break
        table.append(color)
    return table


def gradient_image(size, bbox, units, vector, stops):
    """Gradiente lineal del maestro (violeta→menta en diagonal)."""
    bx0, by0, bx1, by1 = bbox
    width, height = bx1 - bx0, by1 - by0

    x1, y1, x2, y2 = vector
    if units == 'userSpaceOnUse':
        start, end = (x1, y1), (x2, y2)
    else:  # objectBoundingBox: fracciones de la caja de geometría
        start = (bx0 + x1 * width, by0 + y1 * height)
        end = (bx0 + x2 * width, by0 + y2 * height)
    dx, dy = end[0] - start[0], end[1] - start[1]
    denominator = dx * dx + dy * dy or 1.0

    table = gradient_lut(stops)
    scale = VIEWBOX / size
    pixels = []
    for iy in range(size):
        y = (iy + 0.5) * scale
        offset = (y - start[1]) * dy
        for ix in range(size):
            x = (ix + 0.5) * scale
            t = ((x - start[0]) * dx + offset) / denominator
            index = 0 if t <= 0.0 else (255 if t >= 1.0 else int(t * 255.0 + 0.5))
            pixels.append(table[index])

    image = Image.new('RGB', (size, size))
    image.putdata(pixels)
    return image


def build_icon(svg_text, gradient, size):
    """Compone el icono de `size` px desde el maestro SVG.

    Sin gradiente declarado se rasteriza directo. Con él: la capa base sale de
    PyMuPDF con el trazo anulado y el anillo degradado se compone con Pillow
    usando los stops y el vector leídos del maestro.
    """
    upscale = size * SUPERSAMPLE
    if gradient is None:
        return render(svg_text, upscale).resize((size, size), Image.LANCZOS)

    stroke_element, stops, vector, units = gradient
    bbox = geometry_bbox(stroke_element)

    # Capa base: el maestro sin el trazo con gradiente (MuPDF lo pintaría negro).
    plain_text, replacements = re.subn(
        r'stroke="url\(#[^)]+\)"', 'stroke="none"', svg_text
    )
    if replacements != 1:
        raise SystemExit(
            f'esperaba 1 trazo con gradiente en el maestro; hubo {replacements}'
        )

    base = render(plain_text, upscale).resize((size, size), Image.LANCZOS)
    mask = render(shape_svg(stroke_element, 'stroke'), upscale).getchannel('A')
    mask = mask.resize((size, size), Image.LANCZOS)
    ring = gradient_image(size, bbox, units, vector, stops).convert('RGBA')
    ring.putalpha(mask)
    base.alpha_composite(ring)
    return base


def main():
    if not SVG_MASTER.exists():
        sys.exit(f'No existe el SVG maestro: {SVG_MASTER}')

    svg_text = SVG_MASTER.read_text(encoding='utf-8')
    gradient = find_gradient_stroke(svg_text)
    if gradient is None:
        print(
            f'AVISO: {SVG_MASTER.name} no declara trazo con gradiente; '
            'se rasteriza directo.'
        )

    base = build_icon(svg_text, gradient, max(ICO_SIZES))
    base.save(ICO_TARGET, format='ICO', sizes=[(size, size) for size in ICO_SIZES])
    print(
        f'{ICO_TARGET.relative_to(ROOT)} regenerado desde {SVG_MASTER.name} '
        f'({len(ICO_SIZES)} tamaños: {", ".join(map(str, ICO_SIZES))})'
    )


if __name__ == '__main__':
    main()
