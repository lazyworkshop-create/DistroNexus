from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw


def _gear_points(
    center_x: float,
    center_y: float,
    base_radius: float,
    outer_radius: float,
    teeth: int,
) -> list[tuple[float, float]]:
    points: list[tuple[float, float]] = []
    sector = (2.0 * math.pi) / teeth

    for tooth_index in range(teeth):
        start = tooth_index * sector
        tooth_start = start + sector * 0.25
        tooth_end = start + sector * 0.75
        next_start = (tooth_index + 1) * sector

        points.append((center_x + base_radius * math.cos(start), center_y + base_radius * math.sin(start)))
        points.append((center_x + base_radius * math.cos(tooth_start), center_y + base_radius * math.sin(tooth_start)))
        points.append((center_x + outer_radius * math.cos(tooth_start), center_y + outer_radius * math.sin(tooth_start)))
        points.append((center_x + outer_radius * math.cos(tooth_end), center_y + outer_radius * math.sin(tooth_end)))
        points.append((center_x + base_radius * math.cos(tooth_end), center_y + base_radius * math.sin(tooth_end)))
        points.append((center_x + base_radius * math.cos(next_start), center_y + base_radius * math.sin(next_start)))

    return points


def render_gear_icon(size: int, supersample: int = 6) -> Image.Image:
    render_size = size * supersample
    image = Image.new("RGBA", (render_size, render_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    center_x = render_size / 2.0
    center_y = render_size / 2.0

    hole_radius = render_size * (40.0 / 256.0)
    base_radius = render_size * (90.0 / 256.0)
    outer_radius = render_size * ((90.0 + 25.0) / 256.0)
    teeth = 8
    fill_color = (60, 60, 60, 255)

    points = _gear_points(center_x, center_y, base_radius, outer_radius, teeth)
    draw.polygon(points, fill=fill_color)

    draw.ellipse(
        (
            center_x - base_radius,
            center_y - base_radius,
            center_x + base_radius,
            center_y + base_radius,
        ),
        fill=fill_color,
    )

    draw.ellipse(
        (
            center_x - hole_radius,
            center_y - hole_radius,
            center_x + hole_radius,
            center_y + hole_radius,
        ),
        fill=(0, 0, 0, 0),
    )

    return image.resize((size, size), Image.Resampling.LANCZOS)


def write_icon(output: Path, size: int) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    icon = render_gear_icon(size=size)
    icon.save(output, format="PNG")


def main() -> None:
    targets: list[tuple[str, int]] = [
        ("tools/icon.png", 1024),
        ("src/DistroNexus.Package/Assets/StoreLogo.png", 50),
        ("src/DistroNexus.Package/Assets/Square44x44Logo.png", 44),
        ("src/DistroNexus.Package/Assets/Square150x150Logo.png", 150),
        ("src/DistroNexus.Package/Assets/StoreListing/AppTileIcon300x300.png", 300),
        ("src/DistroNexus.Package/Assets/StoreListing/StoreLogo150x150.png", 150),
        ("src/DistroNexus.Package/Assets/StoreListing/StoreLogo71x71.png", 71),
        ("src/DistroNexus.Package/Assets/StoreListing/Square44x44Logo.altform-unplated.png", 44),
        ("src/DistroNexus.Package/Assets/StoreListing/Square150x150Logo.altform-unplated.png", 150),
    ]

    for relative_path, size in targets:
        write_icon(Path(relative_path), size)
        print(f"Generated {relative_path} ({size}x{size})")


if __name__ == "__main__":
    main()
