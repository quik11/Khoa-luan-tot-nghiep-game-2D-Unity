# Gắn animation bắn tơ, lộn né, bay bằng tơ kéo và ảnh sợi tơ (Unity 6)

> **Cập nhật 18/10/2026:** lộn né, bắn tơ và bay tơ kéo đã được công cụ Editor tự gắn (menu **Brand NEU day → Gắn animation lộn né…** và **→ Gắn animation bắn tơ, bay tơ, Quỷ gối bị trói**). Hướng dẫn tay bên dưới chỉ còn để tham khảo, hoặc khi vẽ thêm dáng bắn tơ trên không riêng (`player_webcast_air`).

Làm theo thứ tự. Mọi bước làm khi **không** bấm Play. Làm xong: Ctrl+S, File → Save Project, rồi commit + push các file `.png` (kèm `.meta`), `.anim` mới và `AC_Player.controller`.

## 0. Code đang làm gì
- Bấm **R** (tơ đạn) hoặc **Q** (tơ kéo): code gửi trigger `webCast` (cả dưới đất lẫn trên không). Dưới đất nhân vật đứng khựng 0.2 s (ô *Web Cast Lock Duration*), trong lúc đó vẫn nhảy được.
- Bấm **Shift** (chỉ dưới đất): trigger `dodge`, lướt 0.25 s (ô *Dodge Duration*), lộn luôn về phía mặt đang quay → chỉ cần vẽ cú lộn về phía trước.
- **Q** trúng tường / quái thường: bool `isWebPullFlying` bật suốt lúc bay (thường 0.1–0.35 s, tối đa 0.6 s), tới nơi tự tắt.
- **Sợi tơ** tự vẽ bằng code (không cần animation): hiện từ tay tới điểm bám lúc bay, lóe lên khi giật quái hoặc bắn hụt.
- Animator thiếu tham số nào thì code bỏ qua, Console chỉ nhắc 1 dòng trắng lúc vào màn → **tên tham số phải gõ đúng từng chữ hoa/thường**.

## 1. Vẽ sprite sheet (Piskel)
- Khung **90×60 px** giống bộ đấm (`Assets/Art/NEUNEU/auto/neuattack1v2.png`), các khung xếp **1 hàng ngang**, không chừa khoảng cách.
- Mở `neuattack1v2.png` trong Piskel làm lớp nháp rồi vẽ đè: giữa thân ở khoảng cột 54 (đếm từ mép trái), chân chạm hàng pixel dưới cùng → dùng chung điểm neo (0.6, 0.3333) với bộ đấm, đổi animation không bị giật.
- Dáng lộn: người cuộn tròn, phần thấp nhất chạm đáy khung. Dáng bay: thân nằm ngang, giữa thân cao ~20 px tính từ đáy.
- Gợi ý số khung: bắn tơ 3–4, lộn 3–5, bay 1–3.

## 2. Nhập sprite sheet vào Unity (giống các sheet trong `Assets/Art/NEUNEU`)
1. Tạo thư mục, ví dụ `Assets/Art/NEUNEU/webcast`, `dodge`, `webfly`, kéo file PNG vào.
2. Click file PNG → Inspector:
   - Texture Type: **Sprite (2D and UI)**, Sprite Mode: **Multiple**, Pixels Per Unit: **16**, Mesh Type: Full Rect
   - Advanced: tick Alpha Is Transparency, bỏ tick Generate Mipmap
   - Wrap Mode: Clamp, Filter Mode: **Point (no filter)**
   - Tab Default phía dưới: Compression: **None** (ảnh rộng hơn 2048 px thì Max Size chọn 4096)
   - Bấm **Apply**
3. **Open Sprite Editor** → **Slice** (góc trên trái):
   - Type: **Grid By Cell Size**, Pixel Size X **90**, Y **60**, Offset 0/0, Padding 0/0
   - Pivot: **Custom**, Custom Pivot X **0.6**, Y **0.3333**
   - Method: Delete Existing → bấm **Slice** → bấm **Apply** (góc trên phải) → đóng cửa sổ.
   - Khung cỡ khác 90×60: X = cột giữa thân ÷ chiều rộng khung; Y = 20 ÷ chiều cao khung (khung 60 → 0.3333; khung 40 → 0.5).
4. Bấm mũi tên cạnh file PNG trong Project: phải thấy các hình `ten_0`, `ten_1`, …
5. ⚠️ Đã làm clip rồi mà muốn sửa điểm neo: click từng khung trong Sprite Editor sửa ô Custom Pivot rồi Apply. **Đừng Slice lại bằng Delete Existing** (clip sẽ mất hình).

## 3. Tạo Animation Clip
Cách dễ nhất: **đặt Samples = số khung ÷ số giây mong muốn, TRƯỚC khi kéo hình vào**.

| Clip | Độ dài | Samples | Loop Time |
|---|---|---|---|
| `player_webcast` (bắn dưới đất) | 0.2 s | 3 khung → 15; 4 khung → 20 | tắt |
| `player_webcast_air` (bắn trên không, không bắt buộc) | 0.2–0.25 s | 3 khung → 12–15; 4 khung → 16–20 | tắt |
| `player_dodge` | 0.25 s | 3 → 12; 4 → 16; 5 → 20 | tắt |
| `player_webpull_fly` | lặp | 1–3 khung → 10–12 | **bật** |

1. Mở GameplayScene, click **Player** (object gốc) trong Hierarchy.
2. Window → Animation → **Animation** (Ctrl+6). Bấm ⋮ góc trên phải cửa sổ → tick **Show Sample Rate**.
3. Bấm ô tên clip → **Create New Clip...** → vào `Assets/Characters/Player` → đặt tên (vd `player_webcast`) → Save.
4. Gõ số **Samples** theo bảng → Enter.
5. Trong Project mở mũi tên của PNG, click hình `_0`, giữ Shift click hình cuối, **kéo cả nhóm** thả vào vùng dòng thời gian của cửa sổ Animation.
6. Kiểm tra thước thời gian (vd 3 khung, Samples 15 → kết thúc ở 0:20 = 0.2 s), bấm ▶ xem thử.
7. Click file `.anim` trong Project → tick/bỏ tick **Loop Time** theo bảng.
8. Không bấm nút tròn đỏ (Record). Lặp lại cho các clip còn lại. State không tự hiện trong Animator thì kéo file `.anim` thả vào chỗ trống của Animator (Base Layer).

## 4. Tạo tham số
Window → Animation → **Animator** (đang chọn Player) → tab **Parameters** → nút **+**:
- Trigger `webCast`
- Trigger `dodge`
- Bool `isWebPullFlying` (không tick)

## 5. Nối mũi tên trong Animator
Mọi state mới đặt ở **Base Layer** (cùng chỗ với player_hit, player_death, Any State). Thanh đường dẫn phía trên phải ghi "Base Layer".

Click từng state mới, kiểm tra: Motion đúng clip, Speed 1, **Write Defaults có tick** (giống state cũ).

Tạo mũi tên: chuột phải ô gốc → **Make Transition** → click ô đích. Click mũi tên → Inspector:
- Has Exit Time theo bảng (bật thì Exit Time = 1)
- Mở **Settings**: tick Fixed Duration, **Transition Duration 0**, Transition Offset 0
- Conditions: bấm **+**, chọn tham số

Mũi tên vào **cả nhóm** GroundStates/AirStates: click ô nhóm → menu nhỏ hiện ra → chọn dòng **StateMachine → GroundStates** (hoặc AirStates), giống 2 mũi tên có sẵn từ player_hit. Riêng mũi tên nhảy chọn **States → player_jump**.

### A. Từ Any State
| # | Any State → | Has Exit Time | Can Transition To Self | Conditions |
|---|---|---|---|---|
| 1 | player_webcast | tắt | để tick | `webCast`; `isGrounded` = true; `isAlive` = true |
| 2 | player_webcast_air | tắt | để tick | `webCast`; `isGrounded` = false; `isAlive` = true |
| 3 | player_dodge | tắt | để tick | `dodge`; `isAlive` = true |
| 4 | player_webpull_fly | tắt | **BỎ TICK (bắt buộc)** | `isWebPullFlying` = true; `isAlive` = true |

- **Chỉ vẽ 1 dáng bắn tơ dùng chung**: bỏ mũi tên 2, và mũi tên 1 **không** đặt điều kiện `isGrounded`.
- Thiếu `isAlive = true` thì chết giữa lúc bay sẽ nhảy qua lại giữa dáng chết và dáng bay.
- Mũi tên 4 phải bỏ tick Can Transition To Self, nếu không dáng bay khởi động lại mỗi khung hình (đứng hình ở khung đầu).
- **Thứ tự**: click Any State, trong danh sách Transitions để AnyState → player_death và → player_hit nằm **trên cùng**, 4 mũi tên mới bên dưới (kéo ký hiệu ≡ để đổi chỗ).

### B. Lối ra
| Từ | → Tới | Has Exit Time | Conditions |
|---|---|---|---|
| player_webcast | AirStates → **player_jump** (để dòng này đầu danh sách) | tắt | `jump` |
| player_webcast | GroundStates (cả nhóm) | bật, Exit Time 1 | `isGrounded` = true |
| player_webcast | AirStates (cả nhóm) | bật, Exit Time 1 | `isGrounded` = false |
| player_webcast_air | GroundStates | tắt | `isGrounded` = true |
| player_webcast_air | AirStates | bật, Exit Time 1 | `isGrounded` = false |
| player_dodge | AirStates → **player_jump** | tắt | `jump` |
| player_dodge | GroundStates | bật, Exit Time 1 | `isGrounded` = true |
| player_dodge | AirStates | bật, Exit Time 1 | `isGrounded` = false |
| player_webpull_fly | GroundStates | tắt | `isWebPullFlying` = false; `isGrounded` = true |
| player_webpull_fly | AirStates | tắt | `isWebPullFlying` = false; `isGrounded` = false |

- Mũi tên `jump`: code cho nhảy ngay trong 0.2 s khựng lúc bắn; thiếu mũi tên này thì lúc đáp đất bị chớp dáng nhảy.
- Clip lộn không dài đúng 0.25 s (hoặc sau này giảm Dodge Duration): Speed của state player_dodge = độ dài clip ÷ Dodge Duration.
- Clip bắn dưới đất dài hơn 0.2 s: đặt Speed = độ dài clip ÷ 0.2, hoặc tăng *Web Cast Lock Duration* bằng độ dài clip.

## 6. Ảnh sợi tơ (ô *Web Line Texture*)
1. Piskel: vẽ **1 đoạn tơ nằm ngang**, gợi ý **16×4 px** (rộng 16 px = 1 ô). Mép trái và mép phải phải nối liền nhau (game lặp ảnh theo chiều dài dây), phần trên/dưới sợi tơ để trong suốt. Chỉ 1 khung, không phải sheet nhiều khung.
2. Kéo vào ví dụ `Assets/Art/NEUNEU/webrope/`, Inspector:
   - Texture Type: Sprite (2D and UI), Sprite Mode: **Single**, Pixels Per Unit **16**
   - Advanced: tick Alpha Is Transparency, bỏ tick Generate Mipmap, **Wrap Mode: Repeat**
   - Filter Mode: **Point (no filter)**, Compression: **None** → **Apply**
3. Project → `Assets/Characters/Player` → double-click **Player.prefab** → component **PlayerController** → nhóm **"Dây Tơ Kéo (hiện sợi tơ khi bấm Q)"**:
   - Kéo **dòng file PNG** (dòng ngoài, không phải dòng con) vào ô **Web Line Texture**
   - Web Line Renderer: **để trống** (game tự tạo dây)
   - Web Line Pixels Per Unit: 16. Độ dày dây tự lấy theo chiều cao ảnh (ảnh cao 4 px → dày 0.25 ô)
   - Web Line Color: trắng (giữ đúng màu đã vẽ)
   - **Web Line Hand Offset** = vị trí tay trong dáng bắn/bay: X = (cột tay − cột điểm neo) ÷ 16, Y = (độ cao tay tính từ đáy khung − 20) ÷ 16. Ví dụ khung 90×60, neo cột 54, tay ở cột 70 cao 26 px → X = 1, Y = 0.375
   - Thoát Prefab Mode bằng mũi tên `<` ở Hierarchy (Auto Save tắt thì bấm Save)
4. Gán ở **prefab** (không phải Player trong scene) để mọi màn đều có dây. Ảnh dây chỉ đọc lúc vào màn, đổi ảnh khi đang Play sẽ không thấy đổi; riêng Hand Offset thì chỉnh lúc Play thấy ngay (nhưng nhớ ghi lại số rồi nhập lại sau khi thoát Play).

## 7. Canh thông số theo hình vẽ (Player.prefab → PlayerController)
- **Web Cast Spawn Delay** = lúc khung "nhả tơ" (vd nhả ở khung 2, Samples 15 → 1/15 ≈ 0.07). Để 0 thì tơ ra ngay lúc bấm.
- **Web Cast Lock Duration** (0.2) ≈ độ dài clip bắn tơ dưới đất.
- **Dodge Duration** (0.25) = độ dài clip lộn (hoặc chỉnh Speed như mục 5).

## 8. Cách test
Play ở GameplayScene, click Player, mở cửa sổ Animator (thanh xanh chạy trên state đang phát).
- **Q vào tường**: thấy sợi tơ từ tay tới tường (trắng nếu chưa gán ảnh; không hồng, không đen), dáng bay lặp đều, tới nơi thì rơi/đứng.
- **Q vào khoảng không**: tơ vụt ra 8 ô rồi biến mất. **Q vào quái đang bị trói**: tơ lóe lên, quái bị giật về.
- **R lúc đứng đất**: đứng khựng, dáng bắn ~0.2 s rồi về idle/đi. Bắn xong bấm Space ngay: nhảy, không chớp hình khi đáp.
- **R trên không**: dáng bắn trên không, vẫn rơi, chạm đất là về idle.
- **Shift**: lộn ~0.25 s rồi về idle/đi; lộn ra mép vực thì hết cú lộn mới rơi.
- **Chết lúc đang bay/lộn**: animation chết chạy 1 lần, không nhảy qua lại.
- Console có dòng "Animator nhân vật chưa có tham số …" → tham số đó chưa tạo hoặc gõ sai tên.
- Dây không hiện (vô hình) → báo lại; cách dự phòng: tạo object con của Player, Add Component **Line Renderer**, Material chọn *Sprite-Unlit-Default*, rồi kéo object đó vào ô **Web Line Renderer**.
