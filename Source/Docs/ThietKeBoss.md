# Thiết kế boss màn cuối — "Deadline cận kề" (Màn 5)

Màn 5 là màn cuối của đồ án; Màn 6 để trống, bảng chọn màn hiện "(sắp có)". Hạ boss → nhặt **Tấm bằng Cử nhân xuất sắc** → bảng kết thúc game (cảm ơn + ghi công) → về menu.

Đã bỏ khỏi bản thiết kế ban đầu (người dùng đồng ý 16/10/2026, cho gọn và chắc chạy): sân tối dần, bóng khổng lồ, sàn sụp thành vực, gọi lính, đoạn chạy trốn, phóng to/thu nhỏ camera.

## 1. Hình ảnh

- Bộ hình **Bringer of Death (Free)** của Clembod (`Assets/Art/BringerOfDeath/`). Giấy phép: dùng cá nhân và thương mại, được chỉnh sửa, không bắt buộc ghi công nhưng nên ghi; cấm phát tán lại / bán lại bộ hình.
- 7 bộ khung, mỗi khung 140×93 px: đứng 8, đi 8, chém 10, trúng đòn 3, chết 10, niệm phép 9, phép bàn tay 16. Hình gốc quay mặt sang trái, thân boss nằm ở cột ~104 của khung.
- Công cụ **Brand NEU day → Tạo - cập nhật prefab boss Deadline** (`Assets/Editor/BossBuilder.cs`) tự làm các việc sau:
  - chỉnh cách nạp ảnh: 16 pixel = 1 ô, không làm mờ, không nén;
  - đặt tâm hình ở chân boss: thân ở (104/140, 1/93), bàn tay phép ở (67/140, 1/93);
  - phóng hình 1.6 lần, nên thân boss cao ~5.6 ô (người chơi cao 2.5 ô);
  - tạo prefab `Assets/Characters/Enemies/Boss/DeadlineBoss.prefab`.
- Đồng hồ cát và tấm bằng được vẽ pixel bằng code (`PixelArt.cs`). Có hình tự vẽ thì kéo vào ô `Hourglass Sprite` / `Diploma Sprite` trên boss.
- Không dùng Animator. Khung hình được phát bằng code theo danh sách, để canh đúng khung nào lưỡi hái gây sát thương.

## 2. Máy trạng thái (FSM)

```
Chờ (có khiên ẩn, chưa đánh được)
  └─ người chơi qua cổng → Mở đầu (tên boss + niệm phép) → Nghỉ
Nghỉ ──(chọn đòn)──┬─ Chém (người chơi trong 6 ô: 65%)
                   ├─ Niệm phép bàn tay
                   ├─ Dịch chuyển (mờ đi, hiện ra phía bên kia người chơi) → Chém
                   └─ Đi tới (tối đa 2.5 s) → Chém nếu đã đủ gần
   mọi đòn xong → Nghỉ
Bất kỳ lúc nào:
  - bị trói (6 phát tơ đạn trong 5 s, hoặc đòn mạnh I) → Choáng (đánh thoải mái) → Nghỉ
  - máu xuống ≤ 60% / ≤ 30% → Khiên đồng hồ cát (đầu phase 2 / 3)
  - người chơi chết → Chờ hồi sinh → về chỗ cũ, máu về đầu phase → (phase 2-3: Khiên) → Nghỉ
  - máu = 0 → Chết → chậm hình → mở cổng → Tấm bằng rơi xuống
```

## 3. Các đòn

| Đòn | Báo trước | Sát thương | Cách né |
|---|---|---|---|
| Chém lưỡi hái | Vung tay (4 khung) rồi khựng 0.4 s, đỏ người | 18, hất (7, 4) | Lùi ra xa (tầm ~10 ô trước mặt), hoặc lộn né xuyên qua boss ra sau lưng |
| Bàn tay hạn chót | Vạch đỏ dưới đất 0.45 s + bàn tay thò xuống ~0.6 s | 20, hất lên | Chạy khỏi vạch đỏ |
| Phase 2: 3 bàn tay | Như trên, cách nhau 0.5 s, đuổi theo người chơi | 20 mỗi cái | Chạy liên tục |
| Phase 3: 5 bàn tay đuổi | Như trên, cách nhau 0.5 s | 20 mỗi cái | Chạy liên tục, lộn né |
| Phase 3: hàng bàn tay to (×1.5) | Cả hàng vạch đỏ cách nhau 6 ô, mỗi lần lệch nửa khoảng | 20 | Đứng vào khe giữa 2 vạch |
| Phase 3: chém xong giáng tay | 2 bàn tay rơi vào chỗ người chơi ngay sau nhát chém | 20 | Né chém xong chạy tiếp, đừng đứng yên |
| Phase 3: chuỗi dịch chuyển chém | 4 lần liền: mờ đi 0.15 s, hiện ra sát người chơi phía bên kia, đỏ người rồi chém | 18 mỗi nhát | Mỗi nhát lộn né xuyên qua boss ra sau lưng (lưỡi hái chỉ chém phía trước). Hết chuỗi boss đứng thở 1.2 s = lúc đánh trả |
| Quá hạn (hết giờ khiên) | Cả sàn nhấp nháy đỏ 1.2 s | 25, hất lên | Đứng trên bệ hoặc nhảy đúng lúc (sóng cao 1.6 ô) |

## 4. Phase

| Phase | Máu | Tốc độ | Thêm gì |
|---|---|---|---|
| 1 | 100% → 60% | ×1 | Chém, 1 bàn tay, dịch chuyển |
| 2 | 60% → 30% | ×1.2 | Mở đầu bằng khiên đồng hồ cát; mỗi lần niệm 3 bàn tay |
| 3 | 30% → 0 | ×1.35 | Vẫn dáng phase 2 nhưng dồn đòn: mở đầu bằng khiên **3 đồng hồ cát** (20 s, cứ 2.5 s 1 bàn tay); nghỉ giữa đòn 0.45 s; niệm nhiều hơn (5 bàn tay đuổi, hoặc 40% là hàng bàn tay to); chém xong giáng 2 bàn tay; dịch chuyển nhiều hơn (được dịch chuyển 2 lần liền) và 30% số lần chọn đòn là **chuỗi dịch chuyển chém 4 nhát** |

**Khiên đồng hồ cát:**
- Boss về giữa sân, bật khiên (người nhấp nháy xanh, không ăn đòn) và thả đồng hồ cát lơ lửng cao 5.5 ô: phase 2 có 2 cái (trên mép ngoài 2 bệ), phase 3 có 3 cái (thêm 1 cái giữa sân).
- HUD đếm ngược 15 s (phase 3: 20 s). Trong lúc đó cứ 3.5 s (phase 3: 2.5 s) boss giáng 1 bàn tay xuống chỗ người chơi.
- Phá đồng hồ bằng 1 trong 3 cách:
  - đánh cho hết 20 máu;
  - bắn 2 phát tơ đạn để trói (đứng im 4 s), rồi bấm Q giật xuống cho rơi chạm đất là vỡ;
  - Q bám vào đồng hồ để bay tới rồi đánh trên không.
- Phá hết trong hạn: khiên vỡ, boss choáng 3 s, rơi 1 đùi gà.
- Hết giờ: "QUÁ HẠN!", sóng tím quét cả sàn, đồng hồ còn lại biến mất, boss đánh tiếp.

## 5. Sân đấu (Màn 5, `Assets/Levels/Man5.txt`)

- Hành lang 21 ô (đùi gà, trạm hồi tơ) → cổng `|` → sân kín rộng 26 ô (vừa khung hình camera ~28 ô), trần cao 14 ô, 2 bệ rộng 4 ô cao 4 ô.
- Qua cổng 1.5 ô thì cổng hóa đất đóng lại. Camera khóa giữa sân, nhạc đổi sang "Furious Battle" (YouFulca), thanh máu boss hiện trên cùng có vạch đánh dấu 60% và 30%.
- Điểm lưu ngay sau cổng; hồi sinh không giới hạn (`@hoiSinh -1`) để lúc bảo vệ chắc chắn xem được đoạn kết.
- Trạm hồi tơ: 1 cái ở hành lang, 1 cái ở góc phải sân.
- Boss lướt ngang trên sàn, không va chạm gì (Rigidbody2D Kinematic), nên đi qua dưới bệ được. Người chơi đi xuyên boss như mọi quái.
- Công cụ mô phỏng: đi được tới mọi chỗ trong sân và lên được cả 2 bệ, kể cả khi chỉ đi + nhảy.

## 6. Thông số chính (chỉnh trên prefab `DeadlineBoss`)

| Ô | Mặc định | Ý nghĩa |
|---|---|---|
| Damageable → Max Health | 800 | Máu boss |
| Damageable → Web Hits To Stun / Stun Duration | 6 / 1.5 s | Số phát tơ đạn để trói boss |
| ChemHitbox → tanCong | 18, (7, 4) | Sát thương / hất của đòn chém |
| Phase 2 At / Phase 3 At | 0.6 / 0.3 | Ngưỡng máu đổi phase |
| Phase 2 Speed / Phase 3 Speed | 1.2 / 1.35 | Nhanh hơn bao nhiêu lần |
| Slash Telegraph | 0.4 s | Khựng tay trước khi chém |
| Hand Damage, Hand Warning Time | 20, 0.45 s | Bàn tay |
| Shield Time Limit / Phase 3 Shield Time Limit | 15 / 20 s | Thời hạn phá đồng hồ |
| Phase 2 Hourglasses / Phase 3 Hourglasses | 2 / 3 | Số đồng hồ cát phải phá |
| Phase 3 Idle Time, Phase 3 Hand Count, Phase 3 Row Chance, Phase 3 Slash Follow Up Hands | 0.45 s, 5, 0.4, 2 | Độ dồn đòn ở phase 3 |
| Phase 3 Blink Chain Chance, Phase 3 Blink Count, Blink Fade Time, Blink Chain Recovery | 0.3, 4, 0.15 s, 1.2 s | Chuỗi dịch chuyển chém (đoạn tập trung né) |
| Hourglass Health / Stun Time | 20 / 4 s | Đồng hồ cát |
| Overdue Damage / Height | 25 / 1.6 ô | Sóng Quá hạn |

## 7. Code

| File | Vai trò |
|---|---|
| `Assets/Scripts/Boss/BossController.cs` | FSM boss, phase, khiên, thanh máu, chết + tấm bằng |
| `Assets/Scripts/Boss/BossArena.cs` | Vạch bắt đầu, đóng/mở cổng (vẽ ô đất lúc chạy), khóa camera, đổi nhạc |
| `Assets/Scripts/Boss/BossStrike.cs` | Đòn từ trên giáng xuống: vạch đỏ → hình → hitbox (bàn tay, sóng Quá hạn) |
| `Assets/Scripts/Boss/BossHourglass.cs` | Đồng hồ cát lơ lửng, trói + giật xuống thì vỡ |
| `Assets/Scripts/Boss/DiplomaPickUp.cs` | Tấm bằng: nhặt → hoàn thành màn → bảng kết thúc |
| `Assets/Scripts/Boss/PixelArt.cs` | Vẽ sprite pixel nhỏ bằng code |
| `Assets/Editor/BossBuilder.cs` | Nạp hình Bringer of Death + tạo prefab boss |
| `Assets/Editor/LevelBuilder.cs` | Ký hiệu `O` boss, `|` cổng, `h` đồng hồ cát, `@hoiSinh`, `@nhacBoss` |

Sửa thêm ở code cũ:
- `Damageable`: thêm `superArmor` (trúng đòn không bị ngắt), `canBePulled` (Q không giật boss), `IsShielded` (khiên); object không có Animator vẫn chạy được.
- `PlayerController`: Q chỉ giật quái `canBePulled`.
- `CheckpointManager`: sự kiện `PlayerRespawned` + `cameraFollowOverride` (hồi sinh trong sân thì camera vẫn khóa giữa sân).
- `PauseMenu`: `ShowEnding` (bảng kết thúc nhân bản từ bảng tạm dừng).

## 8. Hướng phát triển (chưa làm)

- Boss "Ám ảnh điểm F" dạng khổng lồ kín màn hình cho Màn 6, dùng lại khung `BossController` / `BossStrike`.
- Sàn sụp, gọi lính, đoạn chạy trốn trước sân boss. (Sân tối dần và bóng khổng lồ ở phase 3 đã bỏ hẳn theo ý người dùng 16/10/2026.)
- Tự vẽ hình đồng hồ cát và tấm bằng thay hình vẽ bằng code.
