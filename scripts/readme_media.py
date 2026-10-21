"""Turns the frames captured by New-ReadmeMedia.ps1 into README screenshots and an animated tour."""

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CAPTIONS = {
    "01-environments": "One tab per environment, every log folder checked with a short timeout",
    "02-files": "Expand a folder to see its newest log files",
    "03-log-viewer": "Open a log in a tab inside its environment",
    "04-search": "Search highlights every match as you type",
    "05-only-matches": "Show only the matching lines, with counts per level",
    "06-palette": "Every command is one search away (Ctrl+Shift+P)",
}

CORNER_RADIUS = 8
MARGIN = 24
CAPTION_HEIGHT = 64
TOUR_WIDTH = 1100
FRAME_MILLISECONDS = 2600
BACKGROUND = (232, 236, 242)
CAPTION_TEXT = (31, 41, 55)
ACCENT = (0, 103, 192)
MUTED = (148, 163, 184)


def without_black_edges(image: Image.Image) -> Image.Image:
    """PrintWindow leaves the one-pixel window border unpainted, which shows as a black line."""
    rgb = image.convert("RGB")
    left, top, right, bottom = 0, 0, rgb.width, rgb.height
    if rgb.getpixel((0, rgb.height // 2)) == (0, 0, 0):
        left = 1
    if rgb.getpixel((rgb.width - 1, rgb.height // 2)) == (0, 0, 0):
        right -= 1
    if rgb.getpixel((rgb.width // 2, rgb.height - 1)) == (0, 0, 0):
        bottom -= 1
    return rgb.crop((left, top, right, bottom))


def rounded(image: Image.Image) -> Image.Image:
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, image.width - 1, image.height - 1), CORNER_RADIUS, fill=255)
    result = image.convert("RGBA")
    result.putalpha(mask)
    return result


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    name = "segoeuib.ttf" if bold else "segoeui.ttf"
    try:
        return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size)
    except OSError:
        return ImageFont.load_default(size)


def tour_frame(shot: Image.Image, caption: str, step: int, steps: int) -> Image.Image:
    scale = TOUR_WIDTH / shot.width
    shot = shot.resize((TOUR_WIDTH, round(shot.height * scale)), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (TOUR_WIDTH + 2 * MARGIN, shot.height + 2 * MARGIN + CAPTION_HEIGHT), BACKGROUND)
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((MARGIN - 1, MARGIN - 1, MARGIN + shot.width, MARGIN + shot.height), CORNER_RADIUS + 1, fill=(203, 210, 220))
    canvas.paste(shot, (MARGIN, MARGIN), shot)

    baseline = MARGIN + shot.height + CAPTION_HEIGHT // 2
    badge = 28
    draw.ellipse((MARGIN, baseline - badge // 2, MARGIN + badge, baseline + badge // 2), fill=ACCENT)
    draw.text((MARGIN + badge // 2, baseline), str(step), font=font(15, bold=True), fill="white", anchor="mm")
    draw.text((MARGIN + badge + 12, baseline), caption, font=font(20, bold=True), fill=CAPTION_TEXT, anchor="lm")

    dot = 8
    right = MARGIN + TOUR_WIDTH
    for index in range(steps):
        x = right - (steps - index) * (dot + 8)
        draw.ellipse((x, baseline - dot // 2, x + dot, baseline + dot // 2), fill=ACCENT if index == step - 1 else MUTED)
    return canvas


def main(frames_folder: Path, output_folder: Path) -> None:
    names = [name for name in CAPTIONS if (frames_folder / f"{name}.png").exists()]
    if not names:
        raise SystemExit(f"No frames found in {frames_folder}")

    tour = []
    for step, name in enumerate(names, start=1):
        shot = rounded(without_black_edges(Image.open(frames_folder / f"{name}.png")))
        shot.save(output_folder / f"{name}.png", optimize=True)
        tour.append(tour_frame(shot, CAPTIONS[name], step, len(names)))

    palette_source = Image.new("RGB", (tour[0].width, tour[0].height * len(tour)))
    for index, frame in enumerate(tour):
        palette_source.paste(frame, (0, index * frame.height))
    palette = palette_source.quantize(colors=255, method=Image.Quantize.MEDIANCUT)
    frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in tour]
    frames[0].save(
        output_folder / "tour.gif",
        save_all=True,
        append_images=frames[1:],
        duration=FRAME_MILLISECONDS,
        loop=0,
        optimize=True,
        disposal=1,
    )


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
