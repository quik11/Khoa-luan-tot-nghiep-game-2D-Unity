// Lõi mô phỏng quỹ đạo nhân vật (bản C của levelsim.fly) - gọi từ Python qua ctypes.
#include <math.h>
#include <stdlib.h>
#include <string.h>

#define G_ 9.81
#define GS 2.25
#define FALL 2.5
#define LOW 2.0
#define AIR 7.0
#define WALK 9.5
#define DT 0.02
#define COYOTE 0.1
#define Q_RANGE 8.0
#define Q_SPEED 22.0
#define Q_MAX 0.6
#define DODGE_V 16.0
#define HW 0.497
#define HH 1.2457
#define GROUND_CAST 0.05
#define WALL_CAST 0.2

static int W = 0, H = 0;
static unsigned char *solid = 0, *killc = 0;
static double kill_y = -1e9;
static double sample_dt = 0.1;

void set_level(int w, int h, const unsigned char *s, const unsigned char *k, double ky, double sdt) {
    W = w; H = h;
    free(solid); free(killc);
    solid = malloc(w * h); killc = malloc(w * h);
    memcpy(solid, s, w * h); memcpy(killc, k, w * h);
    kill_y = ky;
    sample_dt = sdt;
}

static inline int is_solid(int x, int y) {
    if (x < 0 || x >= W) return 0;  // ngoài bản đồ là khoảng trống (rơi ra ngoài thì chết)
    if (y < 0 || y >= H) return 0;
    return solid[y * W + x];
}

static inline int box_hits(double x0, double y0, double x1, double y1) {
    int ax = (int)floor(x0 + 1e-6), bx = (int)floor(x1 - 1e-6);
    int ay = (int)floor(y0 + 1e-6), by = (int)floor(y1 - 1e-6);
    for (int cx = ax; cx <= bx; cx++) {
        if (cx < 0 || cx >= W) continue;
        for (int cy = ay; cy <= by; cy++) {
            if (cy < 0 || cy >= H) continue;
            if (solid[cy * W + cx]) return 1;
        }
    }
    return 0;
}

static inline int box_kill(double x0, double y0, double x1, double y1) {
    int ax = (int)floor(x0 + 1e-6), bx = (int)floor(x1 - 1e-6);
    int ay = (int)floor(y0 + 1e-6), by = (int)floor(y1 - 1e-6);
    for (int cx = ax; cx <= bx; cx++) {
        if (cx < 0 || cx >= W) continue;
        for (int cy = ay; cy <= by; cy++) {
            if (cy < 0 || cy >= H) continue;
            if (killc[cy * W + cx]) return 1;
        }
    }
    return 0;
}

typedef struct {
    double x, y, vx, vy, gs, coyote, wjlock, pull, tx, ty, dodge, qcool;
    int face;
} Body;

static inline int grounded(Body *b) {
    double feet = b->y - HH;
    return box_hits(b->x - HW + 0.02, feet - GROUND_CAST, b->x + HW - 0.02, feet - 1e-4);
}

static inline int on_wall(Body *b, int face) {
    if (face > 0) return box_hits(b->x + HW + 1e-4, b->y - HH + 0.05, b->x + HW + WALL_CAST, b->y + HH - 0.05);
    return box_hits(b->x - HW - WALL_CAST, b->y - HH + 0.05, b->x - HW - 1e-4, b->y + HH - 0.05);
}

static void step_physics(Body *b) {
    b->vy += -G_ * b->gs * DT;
    double nx = b->x + b->vx * DT;
    if (b->vx != 0 && box_hits(nx - HW, b->y - HH, nx + HW, b->y + HH)) {
        // chân nhân vật là capsule tròn: vướng mép thấp hơn ~0.15 ô thì trượt lên được, không bị chặn
        int lifted = 0;
        for (double lift = 0.05; lift <= 0.151; lift += 0.05) {
            if (!box_hits(nx - HW, b->y - HH + lift, nx + HW, b->y + HH + lift)) { b->y += lift; lifted = 1; break; }
        }
        if (!lifted) {
            if (b->vx > 0) nx = floor(nx + HW - 1e-6) - HW - 1e-4;
            else nx = floor(nx - HW + 1e-6) + 1 + HW + 1e-4;
            if (box_hits(nx - HW, b->y - HH, nx + HW, b->y + HH)) nx = b->x;
            b->vx = 0;
        }
    }
    b->x = nx;
    double ny = b->y + b->vy * DT;
    if (b->vy != 0 && box_hits(b->x - HW, ny - HH, b->x + HW, ny + HH)) {
        if (b->vy > 0) ny = floor(ny + HH - 1e-6) - HH - 1e-4;
        else ny = floor(ny - HH + 1e-6) + 1 + HH + 1e-4;
        if (box_hits(b->x - HW, ny - HH, b->x + HW, ny + HH)) ny = b->y;
        b->vy = 0;
    }
    b->y = ny;
}

static inline int is_dead(Body *b) {
    return b->y < kill_y || box_kill(b->x - HW, b->y - HH, b->x + HW, b->y + HH);
}

// tia tơ kéo ngang từ tâm; trả 1 nếu trúng, ghi điểm trúng
int q_target(double x, double y, int face, double *tx, double *ty) {
    int cy = (int)floor(y), cx = (int)floor(x);
    for (;;) {
        cx += face;
        double edge = face > 0 ? cx : cx + 1;
        if (fabs(edge - x) > Q_RANGE) return 0;
        if (is_solid(cx, cy)) { *tx = edge; *ty = y; return 1; }
    }
}

// bay theo tơ; trả 1 nếu còn sống, vị trí dừng ghi vào b
static int q_flight(Body *b) {
    while (b->pull > 0) {
        b->pull -= DT;
        double dx = b->tx - b->x, dy = b->ty - b->y;
        double dist = sqrt(dx * dx + dy * dy);
        if (dist < 0.3 || b->pull <= 0) break;
        b->vx = dx / dist * Q_SPEED; b->vy = dy / dist * Q_SPEED;
        b->gs = GS;
        step_physics(b);
        if (is_dead(b)) return 0;
    }
    b->pull = 0; b->vx = b->vy = 0;
    return 1;
}

int q_pull_from(double x, double y, int face, double *ox, double *oy) {
    double tx, ty;
    if (!q_target(x, y, face, &tx, &ty)) return 0;
    Body b; memset(&b, 0, sizeof b);
    b.x = x; b.y = y; b.face = face; b.gs = GS; b.pull = Q_MAX; b.tx = tx; b.ty = ty;
    if (!q_flight(&b)) return 0;
    *ox = b.x; *oy = b.y;
    return 1;
}

static inline int qk(double v) { return (int)lround(v * 4); }

// ev: mỗi sự kiện 5 int: kind(0 G,1 W,2 A,3 H), a, b, c, d
// prog: n cặp (thời điểm, phím)
int fly(double x, double y, double vx, double vy, int face, double wjlock, double dodge, double qcool,
        const double *prog_t, const int *prog_d, int nprog, double hold, double tmax,
        int allow_q, int allow_hop, int hopn, double jump_at, int *ev, int maxev) {
    // jump_at >= 0: bấm nhảy (giữ trong bộ đệm) từ thời điểm này; nhảy thật khi hết lộn né và còn chạm đất/coyote.
    // Dùng cho: đi khỏi mép rồi nhảy trễ (coyote) và lộn né khỏi mép rồi nhảy ngay khung hết né.
    int jumped = 0; double jt = 0;
    Body b; memset(&b, 0, sizeof b);
    b.x = x; b.y = y; b.vx = vx; b.vy = vy; b.face = face; b.gs = GS; b.wjlock = wjlock; b.dodge = dodge; b.qcool = qcool;
    int n = 0;
    double t = 0, next_sample = 0;
    int pi = 0, inp = prog_d[0];
#define EMIT(K, A, B, C, D) do { if (n < maxev) { int *e = ev + 5 * n; e[0] = K; e[1] = A; e[2] = B; e[3] = C; e[4] = D; n++; } } while (0)
    while (t < tmax) {
        while (pi + 1 < nprog && t >= prog_t[pi + 1] - 1e-9) { pi++; inp = prog_d[pi]; }
        int held = jump_at < 0 ? (t < hold) : (jumped && (t - jt) < hold);
        int gr = grounded(&b);
        if (gr) b.coyote = COYOTE; else b.coyote -= DT;
        if (b.wjlock > 0) b.wjlock -= DT;
        if (b.qcool > 0) b.qcool -= DT;
        int freec = b.wjlock <= 0 && b.dodge <= 0;
        if (inp != 0 && freec) b.face = inp > 0 ? 1 : -1;
        if (freec) b.vx = inp * (gr ? WALK : AIR);
        if (b.dodge > 0) { b.dodge -= DT; b.vx = b.face * DODGE_V; b.vy = 0; }
        if (b.vy < 0) b.gs = GS * FALL;
        else if (b.vy > 0 && !held) b.gs = GS * LOW;
        else b.gs = GS;
        if (jump_at >= 0 && !jumped && t >= jump_at - 1e-9 && b.dodge <= 0 && (gr || b.coyote > 0)) {
            b.vy = 16.0; jumped = 1; jt = t; b.gs = GS;
        }
        step_physics(&b);
        t += DT;
        if (is_dead(&b)) return n;
        if (b.y > H + 12 || b.x < -3 || b.x > W + 3) return n;  // bay khỏi bản đồ
        gr = grounded(&b);
        if (gr && b.vy <= 0 && b.dodge <= 0 && t > 0.03 && (jump_at < 0 || jumped || t > jump_at + 0.12)) {
            EMIT(0, (int)floor(b.x), (int)lround(b.y - HH), 0, 0);
            return n;
        }
        if (b.dodge > 0 || t < next_sample - 1e-9) continue;
        next_sample = t + sample_dt;
        if (gr) continue;
        if (b.wjlock <= 0 && on_wall(&b, b.face)) EMIT(1, qk(b.x), qk(b.y), b.face, 0);
        if (allow_q && b.qcool <= 0) {
            for (int k = 0; k < 2; k++) {
                int f = k == 0 ? b.face : -b.face;
                double ox, oy;
                if (q_pull_from(b.x, b.y, f, &ox, &oy)) EMIT(2, qk(ox), qk(oy), f, 0);
            }
        }
        if (allow_hop && hopn < 1 && b.vy < -2) EMIT(3, qk(b.x), qk(b.y), inp, hopn + 1);
    }
    return n;
}
