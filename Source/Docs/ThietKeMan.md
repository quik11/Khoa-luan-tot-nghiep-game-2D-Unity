# Thiết kế màn chơi — Brand NEU day

Tài liệu tổng hợp nghiên cứu (10/2026) để thiết kế 6 màn chơi. Gồm 3 phần: (1) cách các game 2D nổi tiếng thiết kế màn, (2) các bài hướng dẫn/tutorial về level design, (3) số đo thật của nhân vật đọc từ code + mô phỏng vật lý. Dùng làm căn cứ khi vẽ màn mới và có thể đưa vào Chương 3 của báo cáo.

> **Lưu ý khi trích dẫn vào báo cáo:** trong lúc nghiên cứu, nội dung các trang web được lấy qua bản tóm tắt của công cụ tìm kiếm (không mở được trang gốc). Trước khi trích dẫn câu chữ cụ thể, hãy mở lại đường link ở mục 9 để kiểm tra.

---

## 1. Số đo nhân vật (đo từ code và scene, không phải ước lượng)

Toàn bộ số đo tính theo các thông số **đang chỉnh trong GameplayScene** (đi 9.5, trên không 7, lực nhảy 16, gravityScale 2.25, rơi ×2.5, nhả Space sớm ×2). Prefab `Player.prefab` gốc vẫn là 5 / 3 / 10 / 1 — công cụ dựng màn sao chép GameplayScene nên các màn mới tự giữ đúng thông số này. 1 ô = 1 unit.

| Đại lượng | Giá trị | Ghi chú thiết kế |
|---|---|---|
| Thân người chơi | rộng 1, cao 2.5 ô | Hành lang tối thiểu 3 hàng trống, chuẩn 4 |
| Khung hình camera | 16 hàng × ~28 cột (16:9) | Rơi sâu hơn ~6 hàng là ra khỏi khung hình |
| Nhảy cao nhất | 5.6 ô (giữ Space) | Nhấp nhả: 2.7 ô. Phòng có nhảy cần trần ≥ 9 hàng |
| Nhảy xa nhất (cùng độ cao) | 8.1–8.3 cột | Chạy đà KHÔNG giúp nhảy xa hơn (rời đất là tốc độ về 7) |
| Hố nhảy theo độ khó | ≤4 thoải mái · 5 vừa · 6–7 phải giữ Space · 8 sát giới hạn · 9 chỉ qua bằng nhảy trễ (coyote, canh từng khung) · 10 qua được bằng lộn né sát mép rồi nhảy ngay · **≥11 chắc chắn không qua** | Đo lại 14/10/2026 (mô phỏng có nhảy trễ + lộn né rồi nhảy) |
| Bậc lên | 1–2 hàng nhấp là lên · 3–4 phải giữ · 5 rất khó · ≥6 không lên được | Không có tự bước lên bậc; Quỷ Gối coi bậc 1 hàng là tường |
| Bật tường | lực (8, 12), khóa phím 0.2 s | Giữ Space, mỗi cú lên: giếng rộng 3 ≈ 2.1–2.3 hàng, 4 ≈ 2.9, 5 ≈ 3.1, 6 ≈ 2.5, 7 ≈ 0.7, 8 tụt → **giếng ≥8 mới chắc chắn không leo** (nhấp Space thì chỉ ≤5). Leo 1 vách (bật ra rồi bấm quay lại) làm được ở mọi độ rộng — coi là mẹo |
| Tơ kéo (Q) | tia ngang từ tâm người (chân + 1.25), dài 8, bay 22 ô/s | Bay + treo tổng cộng tối đa 0.6 s kể từ lúc bấm, rồi rơi thẳng xuống |
| Lộn né | 16 ô/s × 0.25 s = 4 cột, vận tốc dọc bị ép = 0 | Lộn ngang qua được rãnh ~3 cột. **Bấm nhảy trong lúc né, rời mép ở mấy khung cuối → nhảy ngay khung hết né, không mất độ cao: qua hố 10 cột** |
| Trần thấp chặn nhảy | trống 3 hàng → nhảy xa 1.3; 4 → 2.5; 5 → 3.6; 6 → 4.5; 7 → 5.7; 8 → 7.6 | Hố qua được thật (thân thò mép lúc cất nhảy/đáp): trần 3 → 1 cột, 4 → 3, 5 → 4, 6 → 5, 7 → 6, 8 → 8 |
| Đòn trên không (U) | mỗi vòng 3 đòn hất lên vy = 7 | Bấm U lúc đang rơi kéo dài cú nhảy thêm 2–4 cột (mẹo nâng cao) |
| Tơ đạn (R) | bay ngang ở chân + 2.1 | Không bắn chéo được; hiện bay xuyên tường |

Các bảng trên lấy từ script mô phỏng 50 Hz đúng thứ tự `FixedUpdate` (sai khác công thức giải tích ~3%).

---

## 2. Nguyên tắc thiết kế (rút ra từ game nổi tiếng + tutorial)

1. **Thiết kế theo số đo, khoảng cách phải rõ ràng.** Hố hoặc chắc chắn qua được, hoặc chắc chắn không. Khoảng "sát giới hạn" làm người chơi thấy bị lừa. *(Suzy Cube, Platformer Toolkit của GMTK, Level Design Book.)*
2. **Mỗi màn một ý chính, theo 4 nhịp: giới thiệu → phát triển → biến tấu → kết** (Kishōtenketsu, Koichi Hayashida – Nintendo). Gặp cơ chế ở chỗ an toàn, rồi khó dần, rồi ghép với thứ khác, cuối cùng là bài kiểm tra. *(Super Mario 3D Land/3D World.)*
3. **Dạy bằng địa hình, không bằng chữ.** Lần đầu gặp cơ chế thì thất bại không mất gì, hoặc chỉ còn đúng một lối thoát là dùng cơ chế đó. *(World 1-1 của Super Mario Bros, màn mở đầu Mega Man X dạy leo tường, Inside.)*
4. **Thất bại phải rẻ.** Mỗi lần hỏng chỉ mất vài giây. Chưa có hồi sinh thì "phạt mềm": rơi xuống tầng dưới rồi tự leo lên lại. *(Celeste, Super Meat Boy, Shovel Knight.)*
5. **Nhịp căng – nghỉ.** Mỗi màn 4–6 đoạn, mỗi đoạn 20–40 giây, xen kẽ leo/nhảy → đánh → khoảng nghỉ có vật phẩm → thử thách khó hơn → sân cuối.
6. **Đặt máu/đạn trước đoạn khó và không bao giờ để kẹt màn vì hết tài nguyên.** Tơ dùng chung cho R và Q, không tự hồi, bắn hụt vẫn mất → trước mỗi chỗ bắt buộc Q phải có trạm hồi tơ. *(Shovel Knight, Donkey Kong Country 2 — mẫu "guidance" của Khalifa và cộng sự.)*
7. **Giếng bật tường đúng số đo**, có "nắp" ở chỗ không muốn cho leo tắt. *(Super Metroid, Super Hyperactive Ninja.)*
8. **Đích tơ kéo phải dễ đọc**, khoảng cách rõ ràng nằm ngoài tầm nhảy, bên kia có chỗ đáp. *(Grapple Dog chỉ cho móc vào bề mặt tô màu riêng; so sánh Ori với Hollow Knight về độ dễ đọc.)*
9. **Sân đấu cận chiến phẳng, có tường/vực chặn hai đầu**; quái bay không lượn trên hố bắt buộc. Quái làm nổi bật cái khó của địa hình nhưng luôn chừa đường né. *(Hollow Knight; Castlevania bị chê vì quái hất người chơi xuống vực.)*
10. **Cho thấy đích sớm và làm rõ điều kiện mở cửa.** HUD "Quái còn lại: N"; không giấu quái ở nhánh cụt hay chỗ không tới được. *(Mẫu "phòng khóa tới khi hết quái" của Zelda, Katana ZERO.)*
11. **Phần thưởng nằm lệch khỏi đường chính**, đòi kỹ năng cao hơn — chỗ đặt quyển sách nâng cấp. *(Dâu tây của Celeste.)*
12. **Màn 1–3 mỗi màn thêm một cơ chế, màn 4–6 ghép cơ chế** ("layering", "foreshadowing" — Khalifa, de Mesentier Silva, Togelius tổng hợp từ hơn 30 game 2D).
13. **Dựng bản nháp khối trơn (blockout), chơi thử sớm, rồi mới vẽ đẹp.** Lưới chữ → Tilemap chính là blockout. *(Neon White, Level Design Book, e-book level design của Unity.)*

---

## 3. Các mẫu dựng sẵn (dùng lại cho mọi màn)

**Hố nhảy có đáy nông** — rộng 4–7 cột, đáy sâu 3–4 hàng: rơi xuống thì nhảy lên lại được, không cần hồi sinh.

**Giếng bật tường** — lòng rộng 3–4 cột (màn 2 tối đa 16 hàng). Hai vách kết thúc cùng độ cao, có bệ ra ≥3 cột ở đỉnh. Không đặt gai trên vách (mất chỗ đạp). Không bắt buộc leo một tường đơn.

**Hố bắt buộc tơ kéo — mẫu A (hầm trần thấp):** trần trống 4 hàng, hố 5 cột, bệ đáp 2 cột thấp hơn 1 hàng, khối tường phía sau bệ phủ đúng độ cao tia (chân + 1.25). Đứng sát mép bấm Q. Dưới trần 4 hàng chỉ nhảy được 2.5 cột, lộn né được ~3 → chắc chắn phải dùng tơ. *(Mô phỏng sau này cho thấy: lộn né + bật vách gần của hố đều qua được hố 5 cột này — muốn chắc chắn bắt buộc Q thì hố ≥ 6 cột dưới trần ≤ 4 hàng VÀ bệ đáp không thấp hơn mép, hoặc ngoài trời ≥ 9 cột.)*

**Hố bắt buộc tơ kéo — mẫu B (hố dài ngoài trời):** hố 10 cột (màn mới dùng **11 cột** — 10 cột qua được bằng lộn né rồi nhảy hoặc nhảy hụt rồi leo 1 vách), bệ đáp 2 cột, tường bám cao ≥7 hàng cách mép ≤12 cột. Chạy sát mép, nhảy giữ Space, bấm Q quanh đỉnh nhảy (mô phỏng: cửa sổ ~0.4 s nếu cất nhảy sát mép). Nhảy thường xa nhất 8.1 → không qua được.

**Mẫu C (bệ đáp cao hơn 3 hàng):** khoảng cách 8–9 cột (nhảy chỉ được 7.3), bấm Q khi chân đã cao hơn bệ.

**Đường quay lại dưới hố:** chưa có hố chết/hồi sinh nên dưới mỗi hố bắt buộc tơ kéo có buồng + đường hầm dẫn về một giếng bật tường rộng 3, leo lên ngay trước đoạn đó để làm lại. Buồng được khoét rộng sang ngang để không có cặp vách nào đủ gần cho bật tường leo tắt.

**Sân đấu Quỷ Gối:** sàn phẳng 8–16 cột, không bậc ở giữa (bậc làm quái quay đầu, cắt combo). Chỗ người chơi bước vào cách vùng tuần tra 6–8 cột (đủ bắn 2 phát tơ trói). Quái bay cao 5 hàng trên sàn ở màn 2 (phải nhảy lên đánh).

---

## 4. Màn 2 — "Leo tường, bắn tơ qua vực" (`Assets/Levels/Man2.txt`)

Chủ đề chính: **tơ kéo Q**; chủ đề phụ: **bật tường liên tục**. Màn 2 cũng là màn đầu tiên dùng được **đòn mạnh (I)** (mở khóa khi qua Màn 1). Kích thước 165 × 46 ô, 7 quái (4 Quỷ Gối + 3 quái bay).

| Cột | Đoạn | Nhịp | Lý do |
|---|---|---|---|
| 2–13 | Xuất phát | nghỉ | Cho người chơi nhìn thấy sân 1 trước khi bước xuống |
| 14–31 | Sân 1: 2 Quỷ Gối đi cạnh nhau + 1 quái bay cao 5 | căng | Chém 1 nhát trúng 2 con; chọn trói con nào trước. Sân thấp hơn chỗ xuất phát 2 hàng để quái không đi lên |
| 32–48 | 2 hố nhảy (6 và 5 cột, lên bệ +3), đáy nông | vừa | Biến tấu nhảy của màn 1, rơi chỉ mất vài giây |
| 49–64 | Giếng bật tường rộng 4, cao 14 | vừa | Đỉnh vách trái có **sách nâng cấp** (phần thưởng lệch đường chính) |
| 65–77 | Sân 2: Quỷ Gối + quái bay | căng | Cao hơn miệng giếng 1 bậc → quái không đứng chặn miệng giếng |
| 78–107 | Hầm trần thấp, **hố mẫu A** (lần đầu bắt buộc Q) | thử thách | Trạm hồi tơ ở cột 90; rơi thì đi hầm về giếng cột 78–80 |
| 108–136 | **Hố mẫu B** 10 cột (nhảy rồi bấm Q) | thử thách khó hơn | Trạm hồi tơ cột 115; rơi thì về giếng cột 108–110; bám tường xong đi qua đường hầm dưới tường |
| 137–162 | Sân cuối: Quỷ Gối + quái bay, cửa | kết | Đùi gà trước sân; sân cao hơn đường hầm 2 hàng để quái không đứng ở chỗ đáp |

Rủi ro đã biết (chờ chơi thử):
- Hố mẫu A: tầm tơ 8 nên phải đứng trong khoảng 1 cột sát mép mới bắn tới. Nếu thấy khó quá thì thu hẹp hố còn 4 cột (đổi 1 ký tự `.` thành `#` ở mép) — nhưng khi đó lộn né có thể vừa đủ qua.
- Người leo tường rất giỏi (nhả rồi bấm lại phím hướng sau mỗi cú bật) có thể leo một vách đơn ra khỏi buồng dưới hố mà không cần tơ. Chấp nhận như mẹo nâng cao.
- Người chơi giỏi dùng mẹo bấm U lúc rơi có thể nhảy xa ~10 cột → vượt được hố mẫu B không cần tơ. Chấp nhận như mẹo nâng cao.
- (Mô phỏng 15/10/2026) Hố mẫu B 10 cột cũng qua được không cần tơ: lộn né sát mép rồi nhảy ngay, nhảy trễ rồi leo 1 vách bệ đáp. Chấp nhận như mẹo; màn 3–4 dùng hố 11 cột.
- (Mô phỏng 14/10/2026 phát hiện) Hố mẫu A còn qua được **không cần tơ và không cần lộn né**: bước khỏi mép, bấm quay lại chạm vách gần của chính cái hố rồi bật tường lên bệ bên kia (bệ thấp hơn 1 hàng nên với tới). Chấp nhận như mẹo nâng cao; màn mới làm hố bắt buộc Q chắc hơn (xem mục 3).

---

## 4b. Màn 3 — "Trói rồi giật" (`Assets/Levels/Man3.txt`, 191 × 46, 10 quái + 1 sách = 14 kiến thức)

Chủ đề chính: **trói bằng tơ đạn R (2 phát) rồi tơ kéo Q giật Quỷ gối xuống vực chết**; ghép kỹ năng màn 2 (2 giếng bật tường rộng 3 và 4, 1 vực bắt buộc tơ kéo 11 cột). 5 điểm lưu. Mọi vực chết khoét tới đáy bản đồ (đứng ở mép không thấy đáy → không nhầm với hố nông có đáy). Vực giật quái rộng 6 cột, người đứng ngang hàng quái, có **gờ an toàn 2 ô** thấp hơn mép 3 hàng sát vách bên quái (lỡ bấm Q lúc quái chưa bị trói, hay bị đánh văng, thì rơi vào gờ chứ không chết).

| Cột | Đoạn | Nhịp |
|---|---|---|
| 2–17 | Xuất phát (cao hơn bờ trước 2 ô) | nghỉ |
| 18–41 | Bài 1: trạm hồi tơ 25, vực 30–35, Quỷ gối trên đảo 36–39 không sang được → đứng cột 29 tập trói + giật, an toàn | giới thiệu |
| 42–61 | Hội trường trũng: Quỷ gối + quái bay cao 5, đùi gà; giếng bật tường rộng 3 (59–61) cao 9 | nghỉ / đánh thường |
| 62–80 | Bài 2: điểm lưu 63, trạm 67, vực 71–76, Quỷ gối gác bệ đáp duy nhất 77–80 → giật nó trước rồi nhảy | phát triển |
| 81–110 | Bài 3: điểm lưu 82, quái bay dọc 89, trạm 92, vực 97–102, Quỷ gối ở chân giếng → giật rồi leo giếng rộng 4 (107–110) cao 13 | biến tấu |
| nhánh sách | Từ đỉnh giếng ra kệ 96–106, nhảy trái + Q bám vách ống khói 83, bật tường lên hốc sách 85 | phần thưởng phụ |
| 111–157 | Bài 4: điểm lưu 114, trạm 117; giật Quỷ gối khỏi đảo 128–132 (vực 122–127); **vực 11 cột 133–143 bắt buộc Q** (bấm 0.75–1.05 s sau khi nhảy) bám khối treo 146–152, đáp bệ 144–145 | biến tấu khó |
| 158–188 | Sân cuối: điểm lưu 160, đùi gà, trạm 164, vực 166–171, sân 17 ô có 2 Quỷ gối + quái bay, cửa 187 | kết |

Mô phỏng: tới cửa, 0 chỗ kẹt; bỏ tơ kéo dừng ở cột 132 (cả khi có lộn né rồi nhảy); bỏ bật tường dừng ở cột 61. Mẹo đã biết (chấp nhận): bấm U lúc rơi qua được vực 11 cột; leo 1 vách lên được hốc sách không cần Q.

## 4c. Màn 4 — "Đấu trường đông" (`Assets/Levels/Man4.txt`, 200 × 40, 15 quái + 1 sách = 19 kiến thức)

Chủ đề chính: **sân đấu đông, đòn mạnh I là chủ lực** — Quỷ gối đi thành cặp/nhóm 3 sát mép vực chết, 1 nhát I (hất 3–5 ô) đánh rơi cả nhóm. Trộn quái bay cao 3, 4, 5 ô và 1 con bay dọc. Giật quái kiểu màn 3 vẫn dùng được ở 3 sân (không bắt buộc). Hết Màn 4: kiến thức cộng dồn 16 + 11 + 14 + 19 = 60 → đủ nâng cấp thứ 5 (mốc 58).

| Cột | Đoạn | Nhịp |
|---|---|---|
| 2–7 | Xuất phát, cao hơn sân 1 hai ô, nhìn thấy sân 1 | nghỉ |
| 8–29 | Sân 1 "mép vực": 2 Quỷ gối cạnh nhau + quái bay cao 4; vực 24–29 cuối sân → bài học I hất xuống vực | giới thiệu |
| 30–37 | Nghỉ 1: điểm lưu 31, đùi gà, đạn tơ | nghỉ |
| 38–58 | Nối: hố nông có đáy 38–42, bậc +3, vực chết 48–53 | vừa |
| 59–72 | Giếng bật tường rộng 4 (61–64) cao 12; đỉnh: điểm lưu 67 | cổng bật tường |
| nhánh sách | Kệ 82–85 cao +5, cách mép 9 → nhảy + Q (cửa sổ ~0.4 s) | phần thưởng phụ |
| 73–96 | Sân 2: 2 Quỷ gối gần vực 91–96 + 2 quái bay cao 3 và 4 | phát triển |
| 97–108 | Nghỉ 2: đùi gà, điểm lưu 99, trạm hồi tơ 106 dưới khối treo 102–106 | nghỉ |
| 109–129 | **Vực 11 cột bắt buộc Q** (109–119), bám khối treo 122–126 (cửa sổ ~0.35 s), đáp bệ 120–121 | thử thách |
| 130–161 | Sân 3 "đảo giữa 2 vực": bậc an toàn 130–135 (điểm lưu 131, trạm 133), đảo 142–155 có 2 Quỷ gối + quái bay | biến tấu |
| 162–167 | Nghỉ 3: điểm lưu 163, đùi gà, đạn tơ | nghỉ |
| 168–197 | Sân cuối "đại sảnh": quái bay thấp + quái bay dọc + nhóm 3 Quỷ gối sát vực 188–193; bệ cửa 194–197 | kết |

Mô phỏng: tới cửa, 0 chỗ kẹt; bỏ tơ kéo dừng ở cột 108; bỏ bật tường dừng ở cột 64.

## 4d. Màn 6 — "Đỉnh đồi chạng vạng" (`Assets/Levels/Man6.txt`, 236 × 50, 17 quái + 1 sách = 21 kiến thức)

Mở đầu **phần 2** (người dùng 23/10/2026: "coi như sang phần khác để đánh con boss khác", nền chạng vạng). Chủ đề: **bài thi tổng hợp** — mỗi đoạn kiểm tra lại 1 kỹ năng, về sau ghép nhiều kỹ năng; leo dần từ mặt sàn hàng 10 lên hàng 36. Không có cơ chế màn mới, chỉ dùng ký hiệu đã có (thêm S/N). Cửa D cuối màn là "cổng" dẫn tới boss mới (chưa làm).

| Cột | Đoạn | Kỹ năng kiểm tra |
|---|---|---|
| 2–11 | Xuất phát cao hơn sân 2 ô (lệch chân 2 ô nên quái không lao tới) | — |
| 12–30 | Sân lao húc: 2 Quỷ gối thường; bậc xuất phát cao 2 ô làm tường | nhảy qua / lộn né cú húc, dụ húc tường cho choáng |
| 31–50 | Giếng bật tường rộng 4 cao 14 (cột treo 31–32 để đi dưới vào giếng), đỉnh: điểm lưu 43 | bật tường (bắt buộc) |
| 51–76 | Hành lang trần thấp 53–66 (trống 4 hàng, không nhảy qua đầu quái), Quỷ gối mất ngủ 62 + thường 73 | lộn né hoàn hảo xuyên cú húc |
| 77–92 | Điểm lưu 78, trạm 79; vực giật quái 81–86 (gờ an toàn 85–86); bọc thép 88 tuần trên bệ đáp duy nhất 87–92 | trói + giật (hoặc đòn I / đánh sau lưng) |
| 93–125 | Điểm lưu 95, trạm 97; **vực tơ kéo 11 cột 101–111**, khối treo 114–118, bệ đáp 112–113 (giống màn 4); điểm lưu 121, đùi gà 123 | tơ kéo (bắt buộc) |
| 126–151 | Đấu trường: 2 bọc thép (136, 142) + 1 thường (139) gần vực chết 146–151, quái bay cao 4 (132) | đòn I vỡ giáp + hất xuống vực |
| 152–162 | Nghỉ: điểm lưu 154, đùi gà, đạn tơ | — |
| 163–183 | Hầm dậm: lỗ 3 cột rơi xuống phòng kín (sàn 20, cao 8), khối cao 6 ô chặn lối trên; 3 Quỷ gối đứng gần (170, 175 mất ngủ, 178) | đòn dậm S + U, kén nổ, lộn né trong phòng kín |
| 184–194 | Giếng bật tường rộng 4 cao 14 ra khỏi phòng; đỉnh: điểm lưu 189, đùi gà, trạm | bật tường |
| nhánh sách | Kệ treo 204–207 cao hơn đỉnh giếng 5 ô, cách mép 9 cột (giống màn 4) | nhảy + Q |
| 195–233 | Đấu trường cuối thấp hơn 4 ô: mất ngủ 206, bọc thép 213, thường 216, quái bay ngang 210 + dọc 219; vực chết 222–227; bệ cửa 228–233, cửa 231 | tổng hợp |

Mô phỏng: tới cửa, 0 chỗ kẹt, 0 lỗi; bỏ tơ kéo dừng ở cột 100 (thử cả mẹo U lúc rơi vẫn dừng); bỏ bật tường dừng ở cột 36. Sách: lấp tạm vực tơ kéo rồi bỏ Q thì vẫn tới được sách (mẹo leo 1 vách, giống kệ màn 4 — chấp nhận). Nền chạng vạng kiểm bằng mô phỏng camera (độ cao −4…+32 so với chỗ xuất phát, 3 tỉ lệ màn hình): không lộ khung. Giờ chuẩn GPA 420 s.

## 5. Lộ trình 6 màn (đề xuất)

| Màn | Cơ chế chủ đề | Ghép thêm |
|---|---|---|
| 1 | Đi, nhảy, đấm, Quỷ Gối (GameplayScene hiện có) | — |
| 2 | Tơ kéo Q + bật tường | Quái bay cao, đòn mạnh I bắt đầu dùng được |
| 3 | Tơ đạn trói + giật quái đang trói (kéo quái rơi khỏi gờ) | Gai + hố chết/checkpoint (nếu làm) |
| 4 | Đấu trường đông, ghép 2 loại quái | Đòn mạnh I thành chủ lực |
| 5 | **Màn boss — kết thúc đồ án** (người dùng chốt 14/10/2026). Ý tưởng: boss to gần kín màn hình đánh người chơi; cách làm bàn sau | — |
| 6 | **Phần 2 mở đầu (23/10/2026):** bài thi tổng hợp mọi kỹ năng, nền chạng vạng, cửa cuối dẫn tới boss mới (chưa làm) — mục 4d | Biến thể bọc thép / mất ngủ, đòn dậm, né hoàn hảo |

Gợi ý chủ đề NEU cho từng màn (để viết cốt truyện): giảng đường, thư viện ("kệ sách" = nền một chiều), ký túc xá (Quỷ Gối), căng tin, phòng thi, lễ tốt nghiệp.

---

## 6. Cơ chế bổ sung đề xuất (CHƯA làm — cần người dùng duyệt)

Xếp theo giá trị / công sức:

1. **Hố chết + checkpoint** (giá trị rất cao, ~50 dòng code): rơi khỏi map thì trừ ít máu và về checkpoint; quái rơi khỏi map thì chết thật (nếu không, cửa không bao giờ mở). Cần có trước khi làm bất kỳ hố không đáy nào.
2. **Gai** trên Tilemap riêng (~30 dòng): chạm thì mất 20 máu, hất lên.
3. ~~Trạm hồi tơ~~ — **đã làm** (ô `respawnTime` trong `WebAmmoPickUp`, ký tự `A`).
4. **Nền một chiều** (nhảy xuyên từ dưới lên): làm trong Unity bằng PlatformEffector2D, sửa 1 dòng trong `TouchingDirections`.
5. **Điểm neo tơ trên không** (~15 dòng): kéo tới neo thì được nhảy thêm 1 lần — bước đệm rẻ trước khi làm đu tơ thật.
6. **Khối sụp** (~40 dòng), 7. **Đệm nảy** (~15 dòng), 8. **Tường đấm vỡ** (cần thêm cờ "không tính là quái" cho cửa), 9. **Thang máy dọc**.
10. **Đu tơ con lắc** (DistanceJoint2D) — đắt nhất (1–2 ngày + nhiều vòng test), để sau màn 2.

Sửa nhỏ nên cân nhắc (mỗi cái 1 commit, hỏi trước):
- Tơ đạn tự hủy khi chạm đất/tường và sau ~0.5 s (hiện bay xuyên tường mãi).
- Cho bấm Space cắt ngang lúc đang treo tơ kéo (hiện lệnh nhảy bị "nuốt").
- Sau 0.2 s khóa của bật tường thì quay mặt theo phím đang giữ (giữ phím là leo được 1 tường) — đổi cảm giác chơi.

---

## 7. Cách dựng màn bằng công cụ

1. Viết file chữ trong `Assets/Levels/` (xem `Man2.txt`; ký hiệu ghi ở đầu file và trong `Assets/Editor/LevelBuilder.cs`).
2. Mở Unity → menu trên cùng **Brand NEU day → Dựng màn từ file chữ...** → chọn file.
3. Công cụ sao chép GameplayScene (giữ camera, nền, HUD, nhân vật, nhạc), xóa ô gạch/quái/vật phẩm cũ, vẽ lại theo file, tự thêm scene vào Build Profiles.
4. Chơi thử → ghi lại chỗ khó/kẹt → sửa file chữ → dựng lại (scene cũ bị ghi đè; chỉnh tay trong scene sẽ mất, nên sửa trong file chữ là chính).

**Công cụ mô phỏng `Docs/CongCu/levelsim/levelsim.py`** (14/10/2026, cần Python 3 + gcc; lõi C tự biên dịch lần đầu): `python3 Docs/CongCu/levelsim/levelsim.py Assets/Levels/Man3.txt --png xem.png`. Chạy lại đúng thứ tự `FixedUpdate` 50 Hz của PlayerController với thông số trong GameplayScene (đi, nhảy giữ/nhả, coyote, bật tường, tơ kéo, lộn né; `--meo` thử thêm mẹo U) rồi dựng đồ thị các chỗ đứng tới được. In ra: tới được cửa chưa, quái/vật phẩm/điểm lưu nào không tới được, chỗ "kẹt" (vào được mà không quay về cửa được), tổng kiến thức của màn, Quỷ gối nào kéo xuống vực được, và **bỏ từng cơ chế (tơ kéo / bật tường / lộn né) thì mất những gì** → chứng minh một đoạn đúng là bắt buộc tơ kéo. Đã kiểm tra khớp số đo mục 1 (hố 8 cột nhảy qua, 9 cột không; bậc 5 nhảy lên, 6 phải bật tường).

**Quái biến thể (23/10/2026):** `S` = Quỷ gối bọc thép (xanh thép, to hơn 15%, máu ×1.6, chậm ×0.7, đỡ mọi đòn cận chiến thường đánh từ phía trước; đánh sau lưng, đòn dậm, tơ đạn, trói rồi đánh đều ăn; đòn mạnh I trúng mặt trước là vỡ giáp; không lao húc), `N` = Quỷ gối mất ngủ (hồng tím, nhỏ 10%, máu ×0.7, nhanh ×1.35, lao húc ở mọi màn, gồng ngắn, nghỉ giữa 2 lần lao 2 giây). Đặt như `E` (ô ngang chân). Quỷ gối thường lao húc từ Màn 2 (người chơi đứng trước mặt cách 3–8 ô, ngang tầm chân ±1.5 hàng, không có tường chắn): gồng 0.6 s (dấu ! + nháy đỏ) → lao 10 ô/s tối đa 0.9 s → đâm tường thì choáng 1.6 s, tới mép vực thì tự phanh (không tự lao xuống). Thiết kế sân: chừa 1 bức tường ở đầu sân để người chơi dụ quái húc tường. Quái bay bổ nhào từ Màn 2 (người chơi thấp hơn 1.5–7 hàng, lệch ngang ≤ 5 cột): cắm xuống đất thì kẹt 1.5 s; bổ nhào hụt (không chạm đất trong 1.2 s, vd lao xuống vực) thì bay lên lại.

Luật kiểm tra trước khi dựng (Claude chạy bằng script khi viết màn): ký hiệu đứng (P, E, S, N, D) có đất bên dưới và ≥3 ô trống phía trên; không có lỗ rộng đúng 1 cột; mỗi hố bắt buộc Q có trạm hồi tơ trong 10–12 cột phía trước; quái không đặt trong vòng 4 cột quanh chỗ đáp bắt buộc hoặc miệng giếng; mọi quái đều có chỗ đứng để đánh cận chiến.

---

## 8. Điểm bất ngờ trong code ảnh hưởng thiết kế màn

- Chạy đà không làm nhảy xa hơn (tốc độ trên không cố định 7).
- Bấm U ngay lúc vừa bật lên làm cụt cú nhảy (vy 16 → 7); bấm lúc rơi thì kéo dài cú nhảy.
- Tia tơ kéo trúng cả collider trigger trên layer Ground → không đặt trigger trên layer Ground.
- Quái và người chơi đi xuyên nhau (ma trận va chạm tắt Player–Enemy); quái dồn cục khi tuần tra cùng sàn.
- Giật quái bay đang trói không có tác dụng (script quái bay ép vận tốc về 0 khi bị trói).
- Rơi không mất máu.

---

## 9. Nguồn tham khảo

Game và phân tích:
- Level Design Book — metrics, blockout, playtesting, pacing, composition: https://book.leveldesignbook.com/process/blockout/metrics · https://book.leveldesignbook.com/process/blockout · https://book.leveldesignbook.com/process/preproduction/pacing · https://book.leveldesignbook.com/process/combat/encounter
- Nintendo 4 bước (Hayashida, GMTK): https://www.mcvuk.com/development/video-nintendos-level-design-secrets-in-four-steps
- World 1-1 (Miyamoto): https://www.gamedeveloper.com/design/how-miyamoto-built-i-super-mario-bros-i-legendary-world-1-1
- Mega Man X dạy leo tường: https://nintendolife.com/features/soapbox-mega-man-xs-tutorial-is-the-perfect-intro-to-capcoms-sublime-sub-series
- Inside — dạy bằng thiết kế màn: https://www.gamedeveloper.com/design/inside-teaching-through-level-design
- Shovel Knight: Specter of Torment level design deep dive: https://www.yachtclubgames.com/blog/specter-of-torment-level-design-deep-dive-4-5
- Khalifa, de Mesentier Silva, Togelius — Level Design Patterns in 2D Games: https://www.gamedeveloper.com/design/level-design-patterns-in-2d-games · https://par.nsf.gov/servlets/purl/10132604
- Suzy Cube — đo đạc: https://www.gamedeveloper.com/design/lessons-from-suzy-cube-measuring-up
- GMTK Platformer Toolkit: https://gmtk.itch.io/platformer-toolkit/devlog/395523/behind-the-code
- Level design cho hệ thống cận chiến: https://www.gamedeveloper.com/design/level-design-for-melee-combat-systems
- Dead Cells — thiết kế lai: https://deepnight.net/tutorial/the-level-design-of-dead-cells-a-hybrid-approach/
- Disneyland "weenie": https://craphound.com/disneylandleveldesign.txt
- Grapple Bot (bề mặt móc được): https://raphicci.itch.io/grapple-bot
- Spider-Man 2 — postmortem đu tơ (GDC): https://gdcvault.com/play/1025725/Classic-Game-Design-Postmortem-Swinging

Unity:
- Introduction to Tilemaps: https://learn.unity.com/tutorial/introduction-to-tilemaps
- PlatformEffector2D: https://docs.unity3d.com/Manual/class-PlatformEffector2D.html
- E-book cho level designer: https://unity.com/blog/games/e-book-for-level-designers
