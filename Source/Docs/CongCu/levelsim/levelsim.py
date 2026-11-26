#!/usr/bin/env python3
"""Mô phỏng + kiểm tra màn chơi viết bằng file chữ (Assets/Levels/ManN.txt) cho Brand NEU day.

Bám theo PlayerController.FixedUpdate (50 Hz) với thông số đang chỉnh trong GameplayScene:
  đi 9.5, trên không 7, nhảy 16, gravityScale 2.25, rơi x2.5, nhả nhảy sớm x2,
  coyote 0.1, bật tường (8,12) khóa 0.2s, tơ kéo tầm 8 bay 22 ô/s tối đa 0.6s (tia ngang từ tâm),
  lộn né 16 ô/s x 0.25s (chỉ dưới đất), mẹo đòn trên không (bấm U lúc rơi) đặt vy=7.
  Có tính cả: nhảy trễ trong 0.1s coyote sau khi bước khỏi mép, lộn né khỏi mép rồi nhảy ngay khung hết né
  (bấm nhảy sẵn trong lúc né, không mất độ cao), chân capsule tròn trượt lên được mép vướng thấp ~0.15 ô.
Thân người chơi: hộp 0.994 x 2.491 quanh tâm (pivot). Chết khi chạm ô X hoặc tâm thấp hơn đáy đất thấp nhất 3 ô.

Đồ thị: nút đứng G(cột, hàng chân), nút chạm tường W, nút vừa tới nơi bằng tơ kéo A, nút mẹo U H.
Mỗi cạnh ghi cơ chế đã dùng {q, wall, dodge, hop} -> bỏ từng cơ chế thì biết mất những chỗ nào.

Dùng:  python3 levelsim.py Man3.txt [--png out.png] [--json out.json] [--quiet]
"""
import sys, os, math, json, argparse, ctypes
from collections import deque

_here = os.path.dirname(os.path.abspath(__file__))
_so = os.path.join(_here, 'flycore.so')
if not os.path.exists(_so) or os.path.getmtime(_so) < os.path.getmtime(os.path.join(_here, 'flycore.c')):
    # lần đầu chạy (hoặc vừa sửa flycore.c) thì tự biên dịch lõi C; cần gcc
    import subprocess
    subprocess.check_call(['gcc', '-O2', '-shared', '-fPIC', '-o', _so, os.path.join(_here, 'flycore.c'), '-lm'])
_core = ctypes.CDLL(_so)
_core.set_level.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_char_p, ctypes.c_char_p, ctypes.c_double, ctypes.c_double]
_core.q_pull_from.argtypes = [ctypes.c_double, ctypes.c_double, ctypes.c_int, ctypes.POINTER(ctypes.c_double), ctypes.POINTER(ctypes.c_double)]
_core.fly.argtypes = [ctypes.c_double] * 4 + [ctypes.c_int] + [ctypes.c_double] * 3 + [
    ctypes.POINTER(ctypes.c_double), ctypes.POINTER(ctypes.c_int), ctypes.c_int, ctypes.c_double, ctypes.c_double,
    ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_double, ctypes.POINTER(ctypes.c_int), ctypes.c_int]
_EVBUF = (ctypes.c_int * (5 * 4096))()
_PT = (ctypes.c_double * 8)()
_PD = (ctypes.c_int * 8)()

G = 9.81
GS = 2.25
FALL = 2.5
LOW = 2.0
AIR = 7.0
WALK = 9.5
JUMP = 16.0
DT = 0.02
COYOTE = 0.1
WJ_X, WJ_Y, WJ_LOCK = 8.0, 12.0, 0.2
Q_RANGE, Q_SPEED, Q_MAX, Q_COOL = 8.0, 22.0, 0.6, 0.4
DODGE_V, DODGE_T = 16.0, 0.25
HOP = 7.0
HW, HH = 0.497, 1.2457
GROUND_CAST, WALL_CAST = 0.05, 0.2
KILL_BELOW = 3.0
USE_HOP = False  # bật bằng --meo: thử cả mẹo U (chậm hơn nhiều)
SAMPLE = 0.1   # khoảng thời gian giữa 2 lần thử rẽ nhánh (bấm Q / bật tường / U)

STAND_CH = set('PESNDC')


class Level:
    def __init__(self, path):
        self.settings = {}
        rows = []
        for raw in open(path, encoding='utf-8'):
            line = raw.rstrip('\r\n').rstrip(' \t')
            if not line or line.startswith('//'):
                continue
            if line.startswith('@'):
                k, _, v = line[1:].partition(' ')
                self.settings[k] = v.strip()
                continue
            rows.append(line)
        self.rows = rows
        self.H = len(rows)
        self.W = max(len(r) for r in rows)
        W, H = self.W, self.H
        # lưới đặc có viền: chỉ số [x+1][y+1]; ngoài trái/phải = đặc, trên/dưới = trống
        self.g = [[False] * (H + 40) for _ in range(W + 2)]
        for xx in (0, W + 1):
            self.g[xx] = [True] * (H + 40)
        self.solid = set()
        self.kill = set()
        self.ents = []
        for r, row in enumerate(rows):
            y = H - 1 - r
            for x, c in enumerate(row):
                if c == '#':
                    self.solid.add((x, y))
                    self.g[x + 1][y + 1] = True
                elif c == 'X':
                    self.kill.add((x, y))
                elif c != '.':
                    self.ents.append((c, x, y))
        self.min_ground_y = min(y for _, y in self.solid) if self.solid else 0
        self.kill_y = self.min_ground_y - KILL_BELOW
        sb = bytearray(W * H); kb = bytearray(W * H)
        for (x, y) in self.solid:
            sb[y * W + x] = 1
        for (x, y) in self.kill:
            kb[y * W + x] = 1
        _core.set_level(W, H, bytes(sb), bytes(kb), self.kill_y, SAMPLE)

    def is_solid(self, x, y):
        if x < 0 or x >= self.W:
            return False
        if y < -1 or y >= self.H + 30:
            return False
        return self.g[x + 1][y + 1]

    def box_hits(self, x0, y0, x1, y1):
        g = self.g
        W = self.W
        ax = int(math.floor(x0 + 1e-6)); bx = int(math.floor(x1 - 1e-6))
        ay = int(math.floor(y0 + 1e-6)); by = int(math.floor(y1 - 1e-6))
        if ay < -1:
            ay = -1
        if by > self.H + 28:
            by = self.H + 28
        for cx in range(ax, bx + 1):
            if cx < 0 or cx >= W:
                return True
            col = g[cx + 1]
            for cy in range(ay, by + 1):
                if col[cy + 1]:
                    return True
        return False

    def box_kill(self, x0, y0, x1, y1):
        if not self.kill:
            return False
        for cx in range(int(math.floor(x0 + 1e-6)), int(math.floor(x1 - 1e-6)) + 1):
            for cy in range(int(math.floor(y0 + 1e-6)), int(math.floor(y1 - 1e-6)) + 1):
                if (cx, cy) in self.kill:
                    return True
        return False


class Body:
    __slots__ = ('x', 'y', 'vx', 'vy', 'face', 'gs', 'coyote', 'wjlock', 'pull', 'tx', 'ty', 'dodge', 'qcool')

    def __init__(self, x, y, face=1):
        self.x, self.y = x, y
        self.vx = self.vy = 0.0
        self.face = face
        self.gs = GS
        self.coyote = 0.0
        self.wjlock = 0.0
        self.pull = 0.0
        self.tx = self.ty = 0.0
        self.dodge = 0.0
        self.qcool = 0.0

    def copy(self):
        b = Body.__new__(Body)
        b.x, b.y, b.vx, b.vy, b.face, b.gs = self.x, self.y, self.vx, self.vy, self.face, self.gs
        b.coyote, b.wjlock, b.pull, b.tx, b.ty, b.dodge, b.qcool = (self.coyote, self.wjlock, self.pull, self.tx,
                                                                    self.ty, self.dodge, self.qcool)
        return b


def grounded(lv, b):
    feet = b.y - HH
    return lv.box_hits(b.x - HW + 0.02, feet - GROUND_CAST, b.x + HW - 0.02, feet - 1e-4)


def on_wall(lv, b, face):
    if face > 0:
        return lv.box_hits(b.x + HW + 1e-4, b.y - HH + 0.05, b.x + HW + WALL_CAST, b.y + HH - 0.05)
    return lv.box_hits(b.x - HW - WALL_CAST, b.y - HH + 0.05, b.x - HW - 1e-4, b.y + HH - 0.05)


def step_physics(lv, b):
    b.vy += -G * b.gs * DT
    nx = b.x + b.vx * DT
    if b.vx != 0 and lv.box_hits(nx - HW, b.y - HH, nx + HW, b.y + HH):
        if b.vx > 0:
            nx = math.floor(nx + HW - 1e-6) - HW - 1e-4
        else:
            nx = math.floor(nx - HW + 1e-6) + 1 + HW + 1e-4
        if lv.box_hits(nx - HW, b.y - HH, nx + HW, b.y + HH):
            nx = b.x
        b.vx = 0.0
    b.x = nx
    ny = b.y + b.vy * DT
    if b.vy != 0 and lv.box_hits(b.x - HW, ny - HH, b.x + HW, ny + HH):
        if b.vy > 0:
            ny = math.floor(ny + HH - 1e-6) - HH - 1e-4
        else:
            ny = math.floor(ny - HH + 1e-6) + 1 + HH + 1e-4
        if lv.box_hits(b.x - HW, ny - HH, b.x + HW, ny + HH):
            ny = b.y
        b.vy = 0.0
    b.y = ny


def dead(lv, b):
    return b.y < lv.kill_y or lv.box_kill(b.x - HW, b.y - HH, b.x + HW, b.y + HH)


def q_target(lv, x, y, face):
    cy = math.floor(y)
    cx = math.floor(x)
    while True:
        cx += face
        edge = cx if face > 0 else cx + 1
        if abs(edge - x) > Q_RANGE:
            return None
        if lv.is_solid(cx, cy):
            return (edge, y)


def q_flight(lv, b):
    """Bay theo tơ kéo tới khi tới nơi / hết 0.6s. Trả về Body lúc dừng (vận tốc 0) hoặc None nếu chết."""
    while b.pull > 0:
        b.pull -= DT
        dx, dy = b.tx - b.x, b.ty - b.y
        dist = math.hypot(dx, dy)
        if dist < 0.3 or b.pull <= 0:
            break
        b.vx, b.vy = dx / dist * Q_SPEED, dy / dist * Q_SPEED
        b.gs = GS
        step_physics(lv, b)
        if dead(lv, b):
            return None
    b.pull = 0.0
    b.vx = b.vy = 0.0
    return b


def qk(v):
    return int(round(v * 4))


def cfly(b, prog, hold, tmax, allow_q=True, allow_hop=True, hopn=0, jump_at=-1.0):
    allow_hop = allow_hop and USE_HOP
    n = len(prog)
    for i, (tt, dd) in enumerate(prog):
        _PT[i] = tt
        _PD[i] = dd
    k = _core.fly(b.x, b.y, b.vx, b.vy, b.face, b.wjlock, b.dodge, b.qcool, _PT, _PD, n, hold, tmax,
                  1 if allow_q else 0, 1 if allow_hop else 0, hopn, jump_at, _EVBUF, 4096)
    ev = []
    for i in range(k):
        kind, a, bb, c, d = _EVBUF[5 * i:5 * i + 5]
        if kind == 0:
            ev.append(('G', (a, bb)))
        elif kind == 1:
            ev.append(('W', (a, bb, c)))
        elif kind == 2:
            ev.append(('A', (a, bb, c)))
        else:
            ev.append(('H', (a, bb, c, d)))
    return ev


def cqpull(x, y, face):
    ox, oy = ctypes.c_double(), ctypes.c_double()
    if _core.q_pull_from(x, y, face, ctypes.byref(ox), ctypes.byref(oy)):
        return ox.value, oy.value
    return None


HOLDS = (0.0, 0.1, 0.2, 0.32, 99.0)
SWITCH_T = (0.15, 0.35, 0.6)


def jump_programs():
    progs = []
    for d in (-1, 0, 1):
        progs.append([(0.0, d)])
    for d in (-1, 1):
        for ts in SWITCH_T:
            progs.append([(0.0, d), (ts, 0)])
            progs.append([(0.0, d), (ts, -d)])
            progs.append([(0.0, 0), (ts, d)])
    return progs


PROGS = jump_programs()


class Graph:
    def __init__(self, lv):
        self.lv = lv
        self.edges = {}   # node -> list of (node2, set of frozenset tags)

    def standable(self, x, fy):
        lv = self.lv
        return (lv.is_solid(x, fy - 1) and not lv.is_solid(x, fy) and not lv.is_solid(x, fy + 1)
                and not lv.is_solid(x, fy + 2) and (x, fy) not in lv.kill and (x, fy + 1) not in lv.kill)

    def _add(self, out, node, tags):
        s = out.get(node)
        if s is None:
            out[node] = {tags}
        else:
            s.add(tags)

    def _add_events(self, out, ev, tags):
        for e in ev:
            kind = e[0]
            if kind == 'G':
                n = e[1]
                for dx in (0, -1, 1):
                    if self.standable(n[0] + dx, n[1]):
                        self._add(out, ('G', n[0] + dx, n[1]), tags)
                        break
            elif kind == 'W':
                self._add(out, ('W',) + e[1], tags)
            elif kind == 'A':
                self._add(out, ('A',) + e[1], tags | {'q'})
            elif kind == 'H':
                self._add(out, ('H',) + e[1], tags | {'hop'})

    def succ(self, node):
        if node in self.edges:
            return self.edges[node]
        lv = self.lv
        out = {}
        kind = node[0]
        E = frozenset()
        if kind == 'G':
            _, x, fy = node
            for dx in (-1, 1):
                if self.standable(x + dx, fy):
                    self._add(out, ('G', x + dx, fy), E)
            starts = [x + 0.5]
            if not lv.is_solid(x - 1, fy - 1):
                starts.append(x + HW - 0.45)
            if not lv.is_solid(x + 1, fy - 1):
                starts.append(x + 1 - HW + 0.45)
            for sx in starts:
                base = Body(sx, fy + HH + 1e-4, 1)
                base.coyote = COYOTE
                if lv.box_hits(base.x - HW, base.y - HH, base.x + HW, base.y + HH):
                    continue
                for prog in PROGS:
                    d0 = prog[0][1]
                    faces = (1, -1) if d0 == 0 else ((1 if d0 > 0 else -1),)
                    for face in faces:
                        for hold in HOLDS:
                            b = base.copy()
                            b.face = face
                            b.vy = JUMP
                            self._add_events(out, cfly(b, prog, hold, 3.5), E)
                for d in (-1, 1):
                    b = base.copy()
                    b.face = d
                    b.x += d * 0.5
                    if lv.box_hits(b.x - HW, b.y - HH, b.x + HW, b.y + HH):
                        continue
                    for prog in ([(0.0, d)], [(0.0, d), (0.15, 0)], [(0.0, d), (0.3, -d)]):
                        self._add_events(out, cfly(b.copy(), prog, 0.0, 3.5), E)
                    # bước khỏi mép rồi mới nhảy (trong 0.1s coyote)
                    for ja in (0.04, 0.08):
                        for hold in (99.0, 0.2):
                            self._add_events(out, cfly(b.copy(), [(0.0, d)], hold, 3.5, jump_at=ja), E)
                for face in (-1, 1):
                    r = cqpull(sx, base.y, face)
                    if r is not None:
                        self._add(out, ('A', qk(r[0]), qk(r[1]), face), frozenset({'q'}))
                    b = base.copy()
                    b.face = face
                    b.dodge = DODGE_T
                    for after in (0, face, -face):
                        self._add_events(out, cfly(b.copy(), [(0.0, after)], 0.0, 3.0), frozenset({'dodge'}))
                    # lộn né khỏi mép, bấm nhảy sẵn -> nhảy ngay khung hết né nếu còn coyote (không mất độ cao)
                    for after in (face, 0):
                        for hold in (99.0, 0.15):
                            self._add_events(out, cfly(b.copy(), [(0.0, after)], hold, 3.0, jump_at=0.0), frozenset({'dodge'}))
        elif kind == 'W':
            _, xq, yq, face = node
            away = -face
            for after in (away, 0, -away):
                for hold in (99.0, 0.1):
                    b = Body(xq / 4, yq / 4, away)
                    b.vx, b.vy = away * WJ_X, WJ_Y
                    b.wjlock = WJ_LOCK
                    self._add_events(out, cfly(b, [(0.0, away), (WJ_LOCK, after)], hold, 3.0), frozenset({'wall'}))
        elif kind == 'A':
            _, xq, yq, face = node
            for after in (0, face, -face):
                b = Body(xq / 4, yq / 4, face)
                b.qcool = Q_COOL - 0.3
                self._add_events(out, cfly(b, [(0.0, after)], 0.0, 3.0), E)
        elif kind == 'H':
            _, xq, yq, inp, n = node
            for after in sorted({inp, 0, -inp}):
                b = Body(xq / 4, yq / 4, 1 if inp >= 0 else -1)
                b.vx, b.vy = inp * AIR, HOP
                self._add_events(out, cfly(b, [(0.0, inp), (0.25, after)], 0.0, 3.0, hopn=n), E)
        out.pop(node, None)
        res = list(out.items())
        self.edges[node] = res
        return res

    def explore(self, start):
        seen = {start}
        dq = deque([start])
        while dq:
            n = dq.popleft()
            for m, _ in self.succ(n):
                if m not in seen:
                    seen.add(m)
                    dq.append(m)
        return seen

    def reach(self, start, allowed):
        allowed = frozenset(allowed)
        seen = {start}
        dq = deque([start])
        while dq:
            n = dq.popleft()
            for m, tagsets in self.succ(n):
                if m in seen:
                    continue
                if any(ts <= allowed for ts in tagsets):
                    seen.add(m)
                    dq.append(m)
        return seen


def static_checks(lv):
    errs, warns = [], []
    for c, x, y in lv.ents:
        if c in STAND_CH:
            if not lv.is_solid(x, y - 1):
                errs.append(f"{c} ở cột {x} (dòng {lv.H - y}) không có đất ngay bên dưới")
            if any(lv.is_solid(x, y + k) for k in (0, 1, 2)):
                errs.append(f"{c} ở cột {x} (dòng {lv.H - y}): thiếu 3 ô trống phía trên chân")
    for (x, y) in lv.solid:
        if (x + 1, y) not in lv.solid and (x + 2, y) in lv.solid and x + 1 < lv.W and not lv.is_solid(x + 1, y + 1):
            if (x + 1, y) not in lv.kill:
                warns.append(f"lỗ rộng 1 cột ở cột {x + 1} (dòng {lv.H - y})")
    for r, row in enumerate(lv.rows):
        if len(row) != lv.W:
            warns.append(f"dòng bản đồ {r + 1} dài {len(row)} khác bề rộng {lv.W}")
    patrol = int(lv.settings.get('tuanTra', '3') or 3)
    for c, x, y in lv.ents:
        if c in '+aAB' and lv.is_solid(x, y):
            errs.append(f"vật phẩm {c} ở cột {x} (dòng {lv.H - y}) nằm trong đất")
        if c in 'FV':
            # quái bay cao ~1.5 ô, rộng ~1.5 ô: đường bay (±tuanTra) không được xuyên tường
            pts = [(x + k, y) for k in range(-patrol, patrol + 1)] if c == 'F' else [(x, y + k) for k in range(-patrol, patrol + 1)]
            for (px, py) in pts:
                if lv.is_solid(px, py) or lv.is_solid(px, py + 1):
                    errs.append(f"quái bay {c} ở cột {x} (dòng {lv.H - y}): đường tuần tra đâm vào tường ở cột {px} dòng {lv.H - py}")
                    break
            xs = range(x - patrol - 1, x + patrol + 2) if c == 'F' else range(x - 1, x + 2)
            if any((px, py) in lv.kill for px in xs for py in range(y - 12, y)):
                warns.append(f"quái bay {c} ở cột {x} lượn ngay trên vực chết (dễ hất người chơi xuống vực)")
        if c in 'ESN':
            # đoạn sàn Quỷ gối (cả 2 biến thể bọc thép S, mất ngủ N) đi tuần: chạy sang 2 bên tới khi gặp tường (ô ngang người) hoặc mép vực
            def run(d):
                k = 0
                while True:
                    nx = x + d * (k + 1)
                    if lv.is_solid(nx, y) or lv.is_solid(nx, y + 1) or not lv.is_solid(nx, y - 1):
                        return k
                    k += 1
                    if k > 80:
                        return k
            seg = run(-1) + run(1) + 1
            if seg < 4:
                warns.append(f"Quỷ gối ở cột {x} (dòng {lv.H - y}) chỉ có {seg} ô sàn để tuần tra (nên >= 6)")
    nE = sum(1 for c, _, _ in lv.ents if c in 'ESNFV')
    return errs, warns, nE


def ent_ok(R, c, x, y):
    Gs = [n for n in R if n[0] == 'G']
    if c in 'PDC':
        return any(abs(n[1] - x) <= 1 and n[2] == y for n in Gs)
    if c in 'ESN':
        return any(abs(n[1] - x) <= 3 and n[2] == y for n in Gs)
    if c in 'FV':
        return any(abs(n[1] - x) <= 4 and 0 <= y - n[2] <= 6 for n in Gs)
    return any(abs(n[1] - x) <= 1 and 0 <= y - n[2] <= 4 for n in Gs)


def analyse(path, png=None, quiet=False):
    lv = Level(path)
    errs, warns, nE = static_checks(lv)
    res = {'file': path, 'W': lv.W, 'H': lv.H, 'errors': errs, 'warnings': warns, 'enemies': nE}
    starts = [(x, y) for c, x, y in lv.ents if c == 'P']
    if len(starts) != 1:
        errs.append(f"cần đúng 1 chữ P, đang có {len(starts)}")
        if not quiet:
            report(res)
        return res, lv, None
    start = ('G',) + starts[0]
    g = Graph(lv)
    g.explore(start)  # mọi cơ chế kể cả mẹo U
    normal = {'q', 'wall', 'dodge'}
    Rn = g.reach(start, normal)
    ents = [(c, x, y) for c, x, y in lv.ents if c != 'P']
    doors = [(x, y) for c, x, y in lv.ents if c == 'D']
    res['nodes'] = len([n for n in Rn if n[0] == 'G'])
    res['door_reachable'] = any(ent_ok(Rn, 'D', x, y) for x, y in doors)
    res['missing'] = [f"{c}@cột{x},dòng{lv.H - y}" for c, x, y in ents if not ent_ok(Rn, c, x, y)]
    rev = {}
    fn = frozenset(normal)
    for n in Rn:
        for m, tagsets in g.succ(n):
            if m in Rn and any(ts <= fn for ts in tagsets):
                rev.setdefault(m, set()).add(n)
    goal = [n for n in Rn if n[0] == 'G' and any(abs(n[1] - x) <= 1 and n[2] == y for x, y in doors)]
    back = set(goal)
    dq = deque(goal)
    while dq:
        n = dq.popleft()
        for m in rev.get(n, ()):
            if m not in back:
                back.add(m)
                dq.append(m)
    stuck = sorted(n for n in Rn - back if n[0] == 'G')
    res['stuck'] = [(n[1], lv.H - n[2]) for n in stuck]
    variants = {
        'khong_to_keo': normal - {'q'},
        'khong_bat_tuong': normal - {'wall'},
        'khong_lon_ne': normal - {'dodge'},
        'chi_di_nhay': set(),
    }
    if USE_HOP:
        variants['co_meo_U'] = normal | {'hop'}
        variants['khong_to_keo_co_meo_U'] = (normal - {'q'}) | {'hop'}
    variants['khong_to_keo_khong_lon_ne'] = normal - {'q', 'dodge'}
    nB = sum(1 for c, _, _ in lv.ents if c == 'B')
    res['kien_thuc'] = nE + 4 * nB
    res['so_sach'] = nB
    # chỗ kéo Quỷ gối (đang bị trói) xuống vực: người chơi đứng cùng tầm (±1 hàng), cách <= 7.5 cột,
    # giữa 2 bên là hố rộng >= 5 cột (hẹp hơn thì quái bay qua hố, đáp sang bờ bên này)
    Gs = [n for n in Rn if n[0] == 'G']
    pulls = []
    for c, x, y in lv.ents:
        if c not in 'ESN':
            continue
        best = None
        for n in Gs:
            _, px, py = n
            if abs(py - y) > 1 or abs(px - x) > 8 or px == x:
                continue
            lo, hi = (px, x) if px < x else (x, px)
            gap = sum(1 for k in range(lo + 1, hi) if not lv.is_solid(k, y - 1) and not lv.is_solid(k, y))
            wall = any(lv.is_solid(k, y) or lv.is_solid(k, y + 1) for k in range(lo + 1, hi))
            if gap >= 5 and not wall:
                best = (px, lv.H - py, gap)
                break
        pulls.append({'E': (x, lv.H - y), 'keo_xuong_vuc': best})
    res['keo_quai'] = pulls
    res['variants'] = {}
    for name, allowed in variants.items():
        Rv = g.reach(start, allowed)
        lost = [f"{c}@cột{x}" for c, x, y in ents if ent_ok(Rn, c, x, y) and not ent_ok(Rv, c, x, y)]
        gained = [f"{c}@cột{x}" for c, x, y in ents if not ent_ok(Rn, c, x, y) and ent_ok(Rv, c, x, y)]
        gcols = [n[1] for n in Rv if n[0] == 'G']
        res['variants'][name] = {'toi_cua': any(ent_ok(Rv, 'D', x, y) for x, y in doors), 'mat': lost,
                                 'them': gained, 'cot_xa_nhat': max(gcols) if gcols else None}
    if png:
        render(lv, Rn, png, stuck=set(stuck))
    if not quiet:
        report(res)
    return res, lv, (g, Rn)


def report(res):
    print(f"== {res['file']}  ({res['W']} x {res['H']}), quái: {res.get('enemies')}")
    for e in res['errors']:
        print('  LỖI:', e)
    for w in res['warnings'][:30]:
        print('  chú ý:', w)
    if 'nodes' not in res:
        return
    print(f"  chỗ đứng tới được: {res['nodes']}; tới cửa: {res['door_reachable']}; kiến thức của màn: {res['kien_thuc']} ({res['enemies']} quái + {res['so_sach']} sách x4)")
    for p in res.get('keo_quai', []):
        if p['keo_xuong_vuc']:
            print(f"  Quỷ gối cột {p['E'][0]} dòng {p['E'][1]}: kéo xuống vực được khi đứng ở cột {p['keo_xuong_vuc'][0]} dòng {p['keo_xuong_vuc'][1]} (hố rộng {p['keo_xuong_vuc'][2]})")
    if res['missing']:
        print('  KHÔNG tới được:', ', '.join(res['missing']))
    if res['stuck']:
        s = res['stuck']
        print(f"  KẸT (vào được mà không về được cửa): {len(s)} chỗ (cột, dòng): {s[:15]}")
    for k, v in res['variants'].items():
        print(f"  [{k}] tới cửa: {v['toi_cua']}; cột xa nhất: {v['cot_xa_nhat']}; mất: {', '.join(v['mat']) or '-'}; thêm: {', '.join(v['them']) or '-'}")


def render(lv, R, out, stuck=()):
    from PIL import Image, ImageDraw
    S = 8
    img = Image.new('RGB', (lv.W * S, lv.H * S), (235, 240, 250))
    d = ImageDraw.Draw(img)
    for (x, y) in lv.solid:
        r = lv.H - 1 - y
        d.rectangle([x * S, r * S, x * S + S - 1, r * S + S - 1], fill=(70, 60, 55))
    for (x, y) in lv.kill:
        r = lv.H - 1 - y
        d.rectangle([x * S, r * S, x * S + S - 1, r * S + S - 1], fill=(230, 60, 60))
    for n in R:
        if n[0] != 'G':
            continue
        _, x, fy = n
        r = lv.H - 1 - fy
        col = (240, 150, 0) if n in stuck else (60, 190, 110)
        d.rectangle([x * S + 1, r * S + S - 3, x * S + S - 2, r * S + S - 1], fill=col)
    colors = {'P': (0, 90, 255), 'E': (200, 0, 160), 'S': (90, 120, 200), 'N': (230, 80, 200), 'F': (150, 0, 220), 'V': (150, 0, 220), 'D': (0, 160, 160),
              'C': (230, 200, 0), 'A': (0, 180, 255), 'a': (0, 180, 255), '+': (255, 120, 120), 'B': (255, 255, 255)}
    for c, x, y in lv.ents:
        r = lv.H - 1 - y
        d.ellipse([x * S, r * S, x * S + S - 1, r * S + S - 1], fill=colors.get(c, (0, 0, 0)), outline=(0, 0, 0))
    for x in range(0, lv.W, 10):
        d.line([x * S, 0, x * S, 6], fill=(0, 0, 0))
    img.save(out)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('file')
    ap.add_argument('--png')
    ap.add_argument('--json')
    ap.add_argument('--quiet', action='store_true')
    ap.add_argument('--meo', action='store_true', help='thử cả mẹo bấm U lúc rơi (chậm)')
    a = ap.parse_args()
    USE_HOP = a.meo
    res, lv, _ = analyse(a.file, a.png, a.quiet)
    if a.json:
        json.dump(res, open(a.json, 'w'), ensure_ascii=False, indent=1)
