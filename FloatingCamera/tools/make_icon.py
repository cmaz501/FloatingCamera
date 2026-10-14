"""生成 Assets/app.ico —— 紫底白色相机图标，多尺寸（BMP 条目，兼容 System.Drawing.Icon）。"""

import os
from PIL import Image, ImageDraw

ACCENT = (139, 63, 190, 255)
WHITE = (255, 255, 255, 255)
SIZES = [16, 24, 32, 48, 64, 128]
SUPERSAMPLE = 8


def render(size: int) -> Image.Image:
    s = size * SUPERSAMPLE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.235), fill=ACCENT)

    def u(x, y, w, h, radius, fill):
        d.rounded_rectangle(
            [x * s, y * s, (x + w) * s, (y + h) * s],
            radius=int(radius * s),
            fill=fill,
        )

    def circle(cx, cy, r, fill):
        d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=fill)

    # 取景器凸起 + 机身
    u(0.335, 0.235, 0.205, 0.105, 0.030, WHITE)
    u(0.175, 0.315, 0.650, 0.415, 0.070, WHITE)

    # 镜头
    circle(0.500, 0.523, 0.130, ACCENT)
    circle(0.500, 0.523, 0.052, WHITE)

    # 闪光灯
    circle(0.735, 0.415, 0.032, ACCENT)

    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    here = os.path.dirname(os.path.abspath(__file__))
    assets = os.path.join(here, "..", "Assets")
    os.makedirs(assets, exist_ok=True)
    target = os.path.join(assets, "app.ico")

    frames = [render(n) for n in SIZES]
    frames[0].save(
        target,
        format="ICO",
        sizes=[(n, n) for n in SIZES],
        append_images=frames[1:],
    )
    print("written:", os.path.normpath(target))


if __name__ == "__main__":
    main()
