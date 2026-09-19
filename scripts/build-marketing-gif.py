from __future__ import annotations

import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path("assets/marketing").resolve()
ROOT.mkdir(parents=True, exist_ok=True)

SCENES = [
    ("widget.png", "Compact media control"),
    ("widget-album.png", "Album artwork theme"),
    ("widget-lyrics.png", "Synchronized lyrics"),
    ("widget-micro-hover.png", "One-click Micro mode"),
    ("settings-appearance.png", "Focused appearance controls"),
    ("settings-elements.png", "Simple layout controls"),
]

CANVAS = (960, 720)
BACKGROUND = (8, 11, 17, 255)
TEXT = (244, 247, 252, 255)
MUTED = (158, 168, 184, 255)


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = [
        Path(r"C:\Windows\Fonts\segoeuib.ttf" if bold else r"C:\Windows\Fonts\segoeui.ttf"),
        Path(r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def contain(image: Image.Image, box: tuple[int, int]) -> Image.Image:
    copy = image.convert("RGBA")
    copy.thumbnail(box, Image.Resampling.LANCZOS)
    return copy


def scene_frame(filename: str, caption: str) -> Image.Image:
    canvas = Image.new("RGBA", CANVAS, BACKGROUND)
    glow = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    glow_draw.ellipse((120, 160, 840, 760), fill=(44, 123, 229, 42))
    canvas = Image.alpha_composite(canvas, glow.filter(ImageFilter.GaussianBlur(80)))

    shot = contain(Image.open(ROOT / filename), (820, 500))
    x = (CANVAS[0] - shot.width) // 2
    y = 142 + (500 - shot.height) // 2

    shadow = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 150), (x + 8, y + 16, x + shot.width + 8, y + shot.height + 16))
    canvas = Image.alpha_composite(canvas, shadow.filter(ImageFilter.GaussianBlur(18)))
    canvas.alpha_composite(shot, (x, y))

    draw = ImageDraw.Draw(canvas)
    draw.text((54, 40), "MEDIANCE", font=font(18, True), fill=MUTED)
    draw.text((54, 68), caption, font=font(36, True), fill=TEXT)
    draw.text((54, 664), "Windows media, lyrics and per-app audio routing", font=font(17), fill=MUTED)
    return canvas.convert("RGB")


def build_gif(frames: list[Image.Image]) -> None:
    sequence: list[Image.Image] = []
    durations: list[int] = []
    for index, current in enumerate(frames):
        sequence.extend([current] * 15)
        durations.extend([80] * 15)
        following = frames[(index + 1) % len(frames)]
        for step in range(1, 9):
            sequence.append(Image.blend(current, following, step / 9))
            durations.append(65)
    output = ROOT / "Mediance-feature-tour.gif"
    sequence[0].save(
        output,
        save_all=True,
        append_images=sequence[1:],
        duration=durations,
        loop=0,
        disposal=2,
        optimize=True,
    )


def build_sheet(frames: list[Image.Image]) -> None:
    sheet = Image.new("RGB", (1600, 1080), (8, 11, 17))
    thumb_box = (740, 450)
    for index, frame in enumerate(frames):
        thumb = contain(frame, thumb_box).convert("RGB")
        column = index % 2
        row = index // 2
        x = 40 + column * 780 + (740 - thumb.width) // 2
        y = 30 + row * 350 + (310 - thumb.height) // 2
        sheet.paste(thumb, (x, y))
    sheet.save(ROOT / "Mediance-screenshot-sheet.png", quality=95)


missing = [name for name, _ in SCENES if not (ROOT / name).exists()]
if missing:
    raise SystemExit("Missing preview images: " + ", ".join(missing))

rendered = [scene_frame(name, caption) for name, caption in SCENES]
build_gif(rendered)
build_sheet(rendered)
print(f"Marketing animation and contact sheet written to {ROOT}")
