"""Chuẩn bị các bộ nền parallax cho game: cắt/lật/ghép ảnh, viết .meta, viết asset BackgroundTheme + DanhSach.txt."""
import json, os, re, sys, uuid
from PIL import Image, ImageChops

PACKS = sys.argv[1]      # thư mục đã giải nén
REPO = sys.argv[2]       # repo Unity
OUT_JSON = sys.argv[3]   # ghi thông số lớp ra để vẽ thử

ART = os.path.join(REPO, 'Assets/Art/Backgrounds')
RES = os.path.join(REPO, 'Assets/Resources/NenManChoi')
SCRIPT_GUID = open(os.path.join(REPO, 'Assets/Scripts/BackgroundTheme.cs.meta')).read().split('guid: ')[1].split()[0]

def new_guid():
    return uuid.uuid4().hex

def write(path, text):
    # luôn UTF-8 + xuống dòng LF (Windows mặc định cp1252 làm hỏng chữ Việt)
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)

def old_value(meta_path, key):
    """Đọc lại guid / spriteID của .meta cũ để chạy lại công cụ không đổi guid (giữ mọi tham chiếu)."""
    if not os.path.exists(meta_path):
        return None
    m = re.search(r'^\s*%s: ([0-9a-f]{32})\s*$' % key, open(meta_path, encoding='utf-8').read(), re.M)
    return m.group(1) if m else None

def is2x(im):
    if im.width % 2 or im.height % 2:
        return False
    # so đủ mọi kênh màu (getbbox của ảnh RGBA chỉ xét kênh trong suốt -> lần trước thu nhỏ nhầm cả trời có trăng)
    s = im.resize((im.width // 2, im.height // 2), Image.NEAREST)
    return s.resize(im.size, Image.NEAREST).tobytes() == im.tobytes()

def frac(a, b):
    px = list(ImageChops.difference(a, b).get_flattened_data())
    return sum(1 for p in px if max(p) > 24) / max(1, len(px))

def seam_bad(im):
    bb = im.getchannel('A').getbbox()
    if bb is None:
        return False
    W = im.width
    seam = frac(im.crop((W - 1, bb[1], W, bb[3])), im.crop((0, bb[1], 1, bb[3])))
    base = frac(im.crop((0, bb[1], 1, bb[3])), im.crop((1, bb[1], 2, bb[3])))
    return seam - base > 0.1, seam, base

def folder_meta(path):
    meta = path + '.meta'
    if os.path.exists(meta):
        return
    write(meta, 
        'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % new_guid())

def text_meta(path):
    write(path + '.meta',
          'fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n'
          '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % (old_value(path + '.meta', 'guid') or new_guid()))

def texture_meta(path, ppu, filter_mode, max_size):
    guid = old_value(path + '.meta', 'guid') or new_guid()
    sprite_id = old_value(path + '.meta', 'spriteID') or new_guid()
    platform = ''
    for target in ('DefaultTexturePlatform', 'Standalone'):
        platform += (
            '  - serializedVersion: 4\n    buildTarget: %s\n    maxTextureSize: %d\n    resizeAlgorithm: 0\n'
            '    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 50\n    crunchedCompression: 0\n'
            '    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n'
            '    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n' % (target, max_size))
    text = (
        'fileFormatVersion: 2\nguid: %(guid)s\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n'
        '  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n'
        '    linearTexture: 0\n    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n'
        '    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n'
        '  bumpmap:\n    convertToNormalMap: 0\n    externalNormalMap: 0\n    heightScale: 0.25\n    normalMapFilter: 0\n'
        '    flipGreenChannel: 0\n  isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0\n  vTOnly: 0\n'
        '  ignoreMipmapLimit: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  cubemapConvolution: 0\n'
        '  seamlessCubemap: 0\n  textureFormat: 1\n  maxTextureSize: %(max)d\n  textureSettings:\n    serializedVersion: 2\n'
        '    filterMode: %(filter)d\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n'
        '  nPOTScale: 0\n  lightmap: 0\n  compressionQuality: 50\n  spriteMode: 1\n  spriteExtrude: 1\n'
        '  spriteMeshType: 0\n  alignment: 7\n  spritePivot: {x: 0.5, y: 0}\n  spritePixelsToUnits: %(ppu)s\n'
        '  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 0\n  alphaUsage: 1\n'
        '  alphaIsTransparency: 1\n  spriteTessellationDetail: -1\n  textureType: 8\n  textureShape: 1\n'
        '  singleChannelComponent: 0\n  flipbookRows: 1\n  flipbookColumns: 1\n  maxTextureSizeSet: 0\n'
        '  compressionQualitySet: 0\n  textureFormatSet: 0\n  ignorePngGamma: 0\n  applyGammaDecoding: 0\n'
        '  swizzle: 50462976\n  cookieLightType: 0\n  platformSettings:\n%(platform)s'
        '  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n    customData: \n'
        '    physicsShape: []\n    bones: []\n    spriteID: %(sid)s\n    internalID: 0\n    vertices: []\n'
        '    indices: \n    edges: []\n    weights: []\n    secondaryTextures: []\n    spriteCustomMetadata:\n'
        '      entries: []\n    nameFileIdTable: {}\n  mipmapLimitGroupName: \n  pSDRemoveMatte: 0\n  userData: \n'
        '  assetBundleName: \n  assetBundleVariant: \n'
    ) % dict(guid=guid, max=max_size, filter=filter_mode, ppu=fmt(ppu), platform=platform, sid=sprite_id)
    write(path + '.meta', text)
    return guid

def fmt(v):
    s = ('%.6f' % v).rstrip('0').rstrip('.')
    return s if s not in ('', '-0') else '0'

def trim_faint_edges(im, limit=16):
    """Bỏ các cột ở 2 mép mờ hơn hẳn phần giữa (trong suốt toàn phần hoặc một phần)."""
    alpha = im.getchannel('A')
    H = im.height
    def coverage(x):
        return sum(alpha.getpixel((x, y)) for y in range(H)) / H
    mid = sorted(coverage(x) for x in range(0, im.width, max(1, im.width // 64)))
    ref = mid[len(mid) // 2]
    left, right = 0, im.width
    while left < limit and coverage(left) < 0.9 * ref:
        left += 1
    while im.width - right < limit and coverage(right - 1) < 0.9 * ref:
        right -= 1
    return im.crop((left, 0, right, H))

def extend_down(im, rows):
    """Kéo dài đáy lớp xuống thêm 'rows' hàng bằng màu điểm đặc thấp nhất mỗi cột (đáy phẳng của núi / rừng xa),
    để leo cao các lớp trước tụt xuống nhanh hơn thì không hở dải trời."""
    W, H = im.size
    out = Image.new('RGBA', (W, H + rows))
    out.paste(im, (0, 0))
    src, dst = im.load(), out.load()
    for x in range(W):
        color = None
        for y in range(H - 1, max(-1, H - 9), -1):
            if src[x, y][3] == 255:
                color = src[x, y]
                break
        if color is None:
            continue
        for y in range(H, H + rows):
            dst[x, y] = color
    return out

def extend_up(im, rows):
    """Kéo dài mép trên lên thêm 'rows' hàng bằng hàng trên cùng (dải mây chạm mép trên khung chuẩn)."""
    W, H = im.size
    out = Image.new('RGBA', (W, H + rows))
    out.paste(im, (0, rows))
    top = im.crop((0, 0, W, 1))
    for y in range(rows):
        out.paste(top, (0, y))
    return out

def haze(im, color, amount):
    """Pha màu lớp về phía màu trời (sương xa) để rừng tối không trùng màu ô đất trong của màn chơi."""
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (round(r + (color[0] - r) * amount), round(g + (color[1] - g) * amount),
                            round(b + (color[2] - b) * amount), a)
    return im

def extend_sky(im):
    """Lớp trời: tô kín phần trong suốt bên dưới bằng màu điểm đặc thấp nhất của từng cột (đường chân trời)."""
    W, H = im.size
    px = im.load()
    for x in range(W):
        last = None
        for y in range(H):
            if px[x, y][3] == 255:
                last = px[x, y]
            elif last is not None:
                px[x, y] = last
    return im

THEMES = [
    dict(asset='NenRung', folder='Rung', scenes=['GameplayScene'],
         src='forestH/parallax/forest', canvas=(1900, 1200), ppu=64, filter=1, scale=1.0, bottom=2.25, vy=0.25,
         credit='Bongseng (rừng thông)',
         layers=[
             dict(file='forest_sky.png', kind='sky', fx=0.97),
             dict(file='forest_moon.png', kind='single', fx=1.0, fy=1.0, x=7.5),  # lọt cả khung 4:3
             dict(file='forest_mountain.png', fx=0.9, extend_down=192),
             dict(file='forest_back.png', fx=0.75, extend_down=192),
             dict(file='forest_long.png', fx=0.6),
             dict(file='forest_mid.png', fx=0.45),
             dict(file='forest_short.png', fx=0.25),
         ]),
    dict(asset='NenChangVang', folder='ChangVang', scenes=['MainMenuScene', 'Man2', 'Man5'],
         src='dusk/Super Mountain Dusk Files/Assets/version A/Layers', canvas=(320, 240), ppu=16, filter=0, scale=1.45,
         bottom=5.5, vy=0.25, credit='ansimuz (núi chạng vạng)',
         layers=[
             dict(file='sky.png', kind='sky', fx=1.0),
             dict(file='far-clouds.png', fx=0.92, scroll=0.12),
             dict(file='near-clouds.png', fx=0.85, scroll=0.25),
             dict(file='far-mountains.png', fx=0.75),
             dict(file='mountains.png', fx=0.55),
             dict(file='trees.png', fx=0.3),
         ]),
    dict(asset='NenHoangHon', folder='HoangHon', scenes=['Man3'],
         src='sunset/Sunset', canvas=(320, 180), ppu=16, filter=0, scale=1.8, bottom=2.0, vy=0.25,
         credit='kayillustrations (hoàng hôn)',
         layers=[
             dict(file='Background.png', kind='sky', fx=1.0),
             dict(file='1.png', fx=0.95),
             dict(file='2.png', fx=0.9),
             dict(file='3.png', fx=0.93, scroll=0.2, y=8.0, extend_up=24),  # hạ dải mây vào khung
             dict(file='4.png', fx=0.8),
             dict(file='5.png', fx=0.7, haze=0.12),
             dict(file='6.png', fx=0.6, haze=0.22),
             dict(file='7.png', fx=0.45, haze=0.25),
             dict(file='8.png', fx=0.35, haze=0.25),
             dict(file='Foreground.png', fx=0.25, haze=0.25),
         ]),
    dict(asset='NenNuiTuyet', folder='NuiTuyet', scenes=['Man4'],
         src='glacial/Glacial-mountains-parallax-background_vnitti/Layers', canvas=(384, 216), ppu=16, filter=0, scale=1.5,
         bottom=2.0, vy=0.25, credit='Vicente Nitti - vnitti (núi tuyết, CC BY 4.0)',
         layers=[
             dict(file='sky_lightened.png', kind='sky', fx=1.0),
             dict(file='cloud_lonely.png', fx=0.95, scroll=0.3),
             dict(file='clouds_bg.png', fx=0.9, scroll=0.06),
             dict(file='glacial_mountains.png', fx=0.82),
             dict(file='clouds_mg_3.png', fx=0.6, scroll=0.12),
             dict(file='clouds_mg_2.png', fx=0.45, scroll=0.2),
             dict(file='clouds_mg_1.png', fx=0.3, scroll=0.3),
         ]),
]

CLAMP = {'Auto': 0, 'TopAndBottom': 1, 'BottomOnly': 2, 'None': 3}

summary = []
os.makedirs(ART, exist_ok=True)
os.makedirs(RES, exist_ok=True)
folder_meta(ART)
folder_meta(RES)
for theme in THEMES:
    folder = os.path.join(ART, theme['folder'])
    os.makedirs(folder, exist_ok=True)
    folder_meta(folder)
    cw, ch = theme['canvas']
    cppu = theme['ppu']
    out_layers = []
    sky_color = None
    for index, spec in enumerate(theme['layers']):
        im = Image.open(os.path.join(PACKS, theme['src'], spec['file'])).convert('RGBA')
        fs = im.height / ch  # điểm ảnh file trên 1 điểm ảnh khung chuẩn
        while is2x(im):
            im = im.resize((im.width // 2, im.height // 2), Image.NEAREST)
            fs /= 2
        tppu = cppu * fs
        kind = spec.get('kind', 'layer')
        x_offset = 0.0
        tiled = True
        mirrored = False
        if kind == 'sky':
            im = extend_sky(im)
            sky_color = im.getpixel((0, 0))
            opaque = [p for p in im.get_flattened_data() if p[3] == 255]
            haze_color = tuple(sum(p[i] for p in opaque) // len(opaque) for i in range(3))
            y1 = im.height
            clamp = 'TopAndBottom'
        elif kind == 'single':
            bb = im.getchannel('A').getbbox()
            x_offset = ((bb[0] + bb[2]) / 2 - im.width / 2) / tppu
            y1 = bb[3]
            im = im.crop(bb)
            tiled = False
            clamp = 'None'
        else:
            bb = im.getchannel('A').getbbox()
            y1 = bb[3]
            ground = bb[3] == im.height
            im = im.crop((0, bb[1], im.width, bb[3]))
            if seam_bad(im)[0]:
                # bỏ các cột mờ ở 2 mép trước: nhiều khi chỉ 1 cột trống làm lộ đường nối, cắt đi là liền, khỏi phải lật
                im = trim_faint_edges(im)
                if seam_bad(im)[0]:
                    flipped = im.transpose(Image.FLIP_LEFT_RIGHT)
                    both = Image.new('RGBA', (im.width * 2, im.height))
                    both.paste(im, (0, 0))
                    both.paste(flipped, (im.width, 0))
                    im = both
                    mirrored = True
                    # dời 1/4 ô lặp để trục gương không nằm đúng giữa khung lúc vào màn (trông như soi gương)
                    x_offset = im.width / tppu / 4
            if spec.get('haze'):
                im = haze(im, haze_color, spec['haze'])
            if spec.get('extend_down'):
                im = extend_down(im, spec['extend_down'])
                y1 += spec['extend_down']
            if spec.get('extend_up'):
                im = extend_up(im, spec['extend_up'])
            clamp = 'BottomOnly' if ground else 'None'
        full_h = ch * fs
        y_offset = spec['y'] if 'y' in spec else (full_h - y1) / tppu
        if 'x' in spec:
            x_offset = spec['x']
        fx = spec['fx']
        fy = spec.get('fy', 1.0 if kind == 'sky' else 1.0 - (1.0 - fx) * theme['vy'])
        name = '%02d_%s' % (index, os.path.splitext(spec['file'])[0].replace(' ', '_'))
        png = os.path.join(folder, name + '.png')
        im.save(png, optimize=True)
        max_size = 2048
        while max_size < max(im.size):
            max_size *= 2
        guid = texture_meta(png, tppu, spec.get('filter', theme['filter']), max_size)
        out_layers.append(dict(name=name, png=png, guid=guid, fx=fx, fy=round(fy, 4), scroll=spec.get('scroll', 0.0),
                               yOffset=round(y_offset, 5), xOffset=round(x_offset, 5), tiled=tiled, clamp=clamp,
                               ppu=tppu, size=im.size, mirrored=mirrored))
    color = [c / 255 for c in sky_color[:3]]
    # asset BackgroundTheme
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
             '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
             '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
             '  m_Script: {fileID: 11500000, guid: %s, type: 3}' % SCRIPT_GUID, '  m_Name: %s' % theme['asset'],
             '  m_EditorClassIdentifier: Assembly-CSharp::BackgroundTheme']
    lines += ['  scale: %s' % fmt(theme['scale']), '  bottomBelowView: %s' % fmt(theme['bottom']), '  width: 480',
              '  cameraColor: {r: %s, g: %s, b: %s, a: 1}' % tuple(fmt(c) for c in color), '  layers:']
    for layer in out_layers:
        lines += ['  - sprite: {fileID: 21300000, guid: %s, type: 3}' % layer['guid'],
                  '    followX: %s' % fmt(layer['fx']), '    followY: %s' % fmt(layer['fy']),
                  '    autoScrollSpeed: %s' % fmt(layer['scroll']), '    yOffset: %s' % fmt(layer['yOffset']),
                  '    xOffset: %s' % fmt(layer['xOffset']), '    tiled: %d' % (1 if layer['tiled'] else 0),
                  '    edgeClamp: %d' % CLAMP[layer['clamp']]]
    asset = os.path.join(RES, theme['asset'] + '.asset')
    write(asset, '\n'.join(lines) + '\n')
    write(asset + '.meta',
          'fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
          '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % (old_value(asset + '.meta', 'guid') or new_guid()))
    summary.append(dict(theme=theme['asset'], scenes=theme['scenes'], scale=theme['scale'], bottom=theme['bottom'],
                        canvas=theme['canvas'], ppu=cppu, color=color, layers=out_layers))

# màn nào dùng bộ nền nào + ghi công: file chữ người dùng tự sửa được, LevelBackground đọc lúc vào màn
listing = ['// Màn nào dùng bộ nền nào: "tên scene | tên file bộ nền" (file .asset cùng thư mục, không ghi đuôi).',
           '// MainMenuScene = menu chính (nền trôi theo chuột).',
           '// Màn không có dòng nào thì giữ nền cũ trong scene. Dòng "ghicong | ..." hiện ở bảng kết thúc game.',
           '']
for theme in THEMES:
    listing += ['%s | %s' % (scene, theme['asset']) for scene in theme['scenes']]
listing.append('')
listing += ['ghicong | %s' % theme['credit'] for theme in THEMES]
list_path = os.path.join(RES, 'DanhSach.txt')
write(list_path, '\n'.join(listing) + '\n')
text_meta(list_path)

with open(OUT_JSON, 'w', encoding='utf-8') as f:
    json.dump(summary, f, indent=1, default=str)
for t in summary:
    print(t['theme'], t['scenes'])
    for l in t['layers']:
        print('   %-26s %-10s fx %.2f fy %.3f scroll %.2f y %.3f x %.3f tiled %s %s ppu %s%s' % (
            l['name'], str(l['size']), l['fx'], l['fy'], l['scroll'], l['yOffset'], l['xOffset'], l['tiled'],
            l['clamp'], fmt(l['ppu']), ' MIRRORED' if l['mirrored'] else ''))
