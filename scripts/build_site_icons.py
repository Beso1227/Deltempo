"""
Rebuild the site's icon assets from one master.

The master is docs/app_icon.png (512x512, the current Cyber Mint shield-and-brush
mark - the same artwork the Windows app ships in app.ico and in the Store logo
set). Every icon on the site is derived from it here, so the artwork cannot
drift between the favicon, the PWA manifest, the social card and the logo the
search engines read.

WHAT IS PRODUCED AND WHY
------------------------
favicon.ico
    The multi-frame icon browsers and crawlers ask for. The old file carried
    only 16/32/48/64 frames, and its 16px frame was an unreadable blob: a
    downscale of an already-downscaled 64px frame. Google renders a favicon at
    16px in search results, so the smallest frame is the one most people ever
    see. The frames are rendered straight from the master, so each one is a
    single clean downscale.

favicon-32.png
    A 32px PNG for the icon set. Browsers pick the closest declared size, and
    there was nothing between 16 and 48.

favicon-48.png, favicon-64.png
    The sizes a bookmark list and a high-density desktop ask for.

app_icon-64.png, app_icon-192.png
    The sizes the pages use for the in-page logo and a PWA installer.

app_icon-64.webp
    The WebP variant the pages use in <picture> sources, with
    app_icon-64.png as the fallback.

apple-touch-icon.png
    iOS requires PNG for apple-touch-icon. The pages pointed it at
    app_icon.webp, which iOS does not accept, so iOS fell back to a
    screenshot of the page.

app_icon-maskable-192.png, app_icon-maskable-512.png
    A maskable icon is masked to an arbitrary shape by the launcher, so its
    content must fit inside the inner circle of diameter 80% of the canvas. The
    master is a squircle that reaches the canvas edge, so it cannot be a
    maskable icon as it stands. These place the artwork at the safe size on an
    opaque brand background, so the launcher's mask never cuts the mark. The
    old manifest claimed "any maskable" for the full-bleed icons, which was
    not true of them.

app_icon.png
    The master itself, re-encoded at maximum compression. It is 482 KB as a
    32-bit PNG, and this is the URL the search engines read for the site's
    logo and the URL the sitemap lists as the site's image, so it is worth
    making a fraction of that size. The pixels are unchanged.

HOW THE CHECK WORKS
-------------------
The check compares the committed files with what this script renders, and it
compares PIXELS rather than bytes. That distinction matters: the PNG and ICO
encoders change between Pillow releases, so the same image encodes to
different bytes on different machines. Comparing pixels answers the question
that actually matters - "is the published icon the icon this script renders?"
- and it answers it the same way on every machine. It is why the check pins
nothing: it passes with whatever Pillow the runner has, as long as the
committed icons are the right picture.

USAGE
-----
    python scripts/build_site_icons.py           rebuild the icons
    python scripts/build_site_icons.py --check   fail if the committed icons
                                                 are not what this produces
"""
import io
import os
import sys

from PIL import Image, ImageChops

DOCS = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "docs"))
MASTER = os.path.join(DOCS, "app_icon.png")

# The colour the squircle's outer edge fades to. It is the colour the artwork
# itself uses for its shadowed border, so the maskable icons and the iOS icon
# sit on the brand's own background rather than an invented one.
BACKGROUND = (3, 18, 31, 255)

# A maskable icon must keep its content inside the inner circle of diameter
# 80% of the canvas. A square of side s has its corners at s/sqrt(2) from the
# centre, so the artwork's side is 0.8 * sqrt(2) * canvas / 2 to fit.
MASKABLE_SIDE = 0.8 * (2 ** 0.5) / 2

# In --check mode nothing is written: the icons are rendered into memory and
# compared with the committed files, so CI can prove the committed set is what
# this script produces without touching the working tree.
CHECK = "--check" in sys.argv

master = Image.open(MASTER).convert("RGBA")
if master.size != (512, 512):
    raise SystemExit("The master icon is expected to be 512x512, found %s" % (master.size,))

# What this script produced, per file: the encoded bytes (to write) and the
# image it decodes to (to compare).
produced = {}


def render(image, name, **kwargs):
    """Encode an icon and remember it, writing it unless this is a check."""
    fmt = kwargs.pop("format")
    buffer = io.BytesIO()
    image.save(buffer, format=fmt, **kwargs)
    data = buffer.getvalue()
    produced[name] = (data, Image.open(io.BytesIO(data)).convert("RGBA"))

    path = os.path.join(DOCS, name)
    if not CHECK:
        with open(path, "wb") as handle:
            handle.write(data)
    print("  %-32s %7.1f KB" % (name, len(data) / 1024.0))


print("Rebuilding the icon set from docs/app_icon.png")
print()

# --- favicon.ico -----------------------------------------------------------
# Every frame is a direct downscale of the master. PIL's ICO writer resizes
# the master itself with LANCZOS, so a frame is never a downscale of a
# downscale.
ico_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64)]
buffer = io.BytesIO()
master.save(buffer, format="ICO", sizes=ico_sizes)
data = buffer.getvalue()
produced["favicon.ico"] = (data, Image.open(io.BytesIO(data)).convert("RGBA"))
if not CHECK:
    with open(os.path.join(DOCS, "favicon.ico"), "wb") as handle:
        handle.write(data)
print("  %-32s %7.1f KB  frames %s" % (
    "favicon.ico",
    len(data) / 1024.0,
    ", ".join("%d" % s[0] for s in ico_sizes),
))
print()

# --- the per-size PNGs -----------------------------------------------------
print("Per-size PNGs (direct downscales of the master):")
for name, size in (
    ("favicon-32.png", 32),
    ("favicon-48.png", 48),
    ("favicon-64.png", 64),
    ("app_icon-64.png", 64),
    ("app_icon-192.png", 192),
):
    render(master.resize((size, size), Image.LANCZOS), name, format="PNG", optimize=True)
print()

# --- the WebP variant the pages use in <picture> ---------------------------
print("WebP variant:")
render(master.resize((64, 64), Image.LANCZOS), "app_icon-64.webp", format="WEBP", quality=90, method=6)
print()

# --- apple-touch-icon (iOS wants PNG) --------------------------------------
print("iOS and Android:")
apple = master.resize((180, 180), Image.LANCZOS)
flat = Image.new("RGBA", (180, 180), BACKGROUND)
flat.alpha_composite(apple)
render(flat.convert("RGB"), "apple-touch-icon.png", format="PNG", optimize=True)

for name, size in (("app_icon-maskable-192.png", 192), ("app_icon-maskable-512.png", 512)):
    inner = int(round(size * MASKABLE_SIDE))
    art = master.resize((inner, inner), Image.LANCZOS)
    canvas = Image.new("RGBA", (size, size), BACKGROUND)
    canvas.alpha_composite(art, ((size - inner) // 2, (size - inner) // 2))
    render(canvas.convert("RGB"), name, format="PNG", optimize=True)
print()

# --- the master, re-encoded ------------------------------------------------
# 32-bit PNG at 512x512 is 482 KB for an image whose content is a smooth
# gradient. Re-encoding at maximum compression keeps the pixels identical and
# makes the logo a file a crawler can fetch without thinking about it.
print("Master re-encoded (identical pixels, a fraction of the bytes):")
render(master, "app_icon.png", format="PNG", optimize=True, compress_level=9)

# --- the check --------------------------------------------------------------
if CHECK:
    stale = []
    for name, (_, rendered) in sorted(produced.items()):
        path = os.path.join(DOCS, name)
        if not os.path.exists(path):
            stale.append(f"{name} (missing)")
            continue
        committed = Image.open(path).convert("RGBA")
        if committed.size != rendered.size:
            stale.append(f"{name} ({committed.size[0]}x{committed.size[1]}, expected {rendered.size[0]}x{rendered.size[1]})")
            continue
        # A pixel is the picture; the encoding is only the container. The
        # committed icon is current when it shows the same picture.
        #
        # The comparison is made on RGB, not RGBA: ImageChops.getbbox() on an
        # RGBA image looks at the alpha channel alone, so a difference in the
        # colours alone - a darkened icon, a changed hue - would be invisible
        # to it. Comparing the RGB bands catches every visible change.
        difference = ImageChops.difference(committed.convert("RGB"), rendered.convert("RGB")).getbbox()
        if difference is not None:
            stale.append(f"{name} (the pixels differ)")
    if stale:
        print()
        print("The committed icons are not what this script produces:")
        for name in stale:
            print(f"  {name}")
        print("Run: python scripts/build_site_icons.py")
        raise SystemExit(1)
    print()
    print(f"The committed icons are current ({len(produced)} files).")
