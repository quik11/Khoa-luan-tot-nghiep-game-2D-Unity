"""Vẽ thử nền theo đúng công thức LevelBackground + ParallaxEffect ở vài vị trí camera."""
import json, sys, math
from PIL import Image, ImageDraw

summary = json.load(open(sys.argv[1]))
out = sys.argv[2]
PX = 30           # điểm ảnh xem thử trên 1 ô
HALF_H = 8.0
ASPECT = 16 / 9
MARGIN = 0.1
WIDTH = 480

cache = {}
def tex(path):
    if path not in cache:
        cache[path] = Image.open(path).convert('RGBA')
    return cache[path]

def render(theme, dx, dy, t=0.0, aspect=ASPECT):
    hw = HALF_H * aspect
    W, H = int(2 * hw * PX), int(2 * HALF_H * PX)
    canvas = Image.new('RGBA', (W, H), tuple(int(c * 255) for c in theme['color']) + (255,))
    s = theme['scale']
    cam0 = (0.0, 0.0)
    cam = (dx, dy)
    bottom0 = cam0[1] - HALF_H - theme['bottom']
    for L in theme['layers']:
        im = tex(L['png'])
        tw, th = im.size[0] / L['ppu'] * s, im.size[1] / L['ppu'] * s   # 1 ô lặp, theo đơn vị thế giới
        start = (cam0[0] + L['xOffset'] * s, bottom0 + L['yOffset'] * s)
        scroll = 0.0
        if L['scroll'] and L['tiled']:
            scroll = math.fmod(L['scroll'] * t, tw)
        x = start[0] + dx * L['fx'] + scroll
        y = start[1] + dy * L['fy']
        if L['tiled']:
            tiles = max(1, math.ceil(WIDTH / tw))
            if tiles % 2 == 0:
                tiles += 1
            width = tiles * tw
        else:
            width = tw
        # giữ mép như ParallaxEffect.KeepCoveringView (pivot đáy giữa)
        cx, cy = x, y + th / 2
        vhh, vhw = HALF_H + MARGIN, hw + MARGIN
        if L['clamp'] != 'None':
            if width / 2 > vhw:
                cx = min(max(cx, cam[0] + vhw - width / 2), cam[0] - vhw + width / 2)
            # như ParallaxEffect: mép trên chỉ giữ khi ảnh cao hơn khung, mép dưới luôn giữ
            if L['clamp'] == 'TopAndBottom' and th / 2 > vhh:
                cy = max(cy, cam[1] + vhh - th / 2)
            if L['clamp'] in ('TopAndBottom', 'BottomOnly'):
                cy = min(cy, cam[1] - vhh + th / 2)
        x, y = cx, cy - th / 2
        scaled = im.resize((max(1, round(tw * PX)), max(1, round(th * PX))), Image.NEAREST)
        top_px = int(round((cam[1] + HALF_H - (y + th)) * PX))
        left_edge = x - width / 2
        k0 = math.floor((cam[0] - hw - left_edge) / tw) - 1
        k1 = math.ceil((cam[0] + hw - left_edge) / tw) + 1
        for k in range(max(0, k0), min(math.ceil(width / tw), k1)):
            tx = left_edge + k * tw
            left_px = int(round((tx - (cam[0] - hw)) * PX))
            canvas.paste(scaled, (left_px, top_px), scaled)
    return canvas

if __name__ == '__main__':
    shots = [(0, 0, 'đầu màn'), (60, 0, 'đi sang phải 60 ô'), (20, 15, 'leo cao 15 ô'), (40, 29, 'leo cao 29 ô'),
             (10, -10, 'rơi xuống 10 ô')]
    for theme in summary:
        frames = [render(theme, dx, dy, t=20) for dx, dy, _ in shots]
        W, H = frames[0].size
        sheet = Image.new('RGBA', (W * 2 + 10, (H + 30) * 3), (40, 40, 40, 255))
        d = ImageDraw.Draw(sheet)
        for i, (f, (dx, dy, label)) in enumerate(zip(frames, shots)):
            x, y = (i % 2) * (W + 10), (i // 2) * (H + 30)
            sheet.paste(f, (x, y + 25))
            d.text((x + 5, y + 5), '%s - %s' % (theme['theme'], label.encode('ascii', 'ignore').decode()), fill=(255, 255, 255, 255))
        sheet.save('%s/preview_%s.png' % (out, theme['theme']))
        print('saved', theme['theme'], sheet.size)
