# Brand NEU day

Trò chơi hành động đi cảnh 2D đồ họa pixel art, sản phẩm khóa luận tốt nghiệp
**"Phát triển trò chơi hành động 2D sử dụng Unity"**.

- Sinh viên: Trần Quang Khánh – 11236132 – Công nghệ thông tin 65B
- Giảng viên hướng dẫn: TS. Lưu Minh Tuấn
- Trường Công nghệ – Đại học Kinh tế Quốc dân, 2026

Người chơi vào vai một sinh viên NEU, dùng đòn cận chiến và kỹ năng tơ nhện để vượt 6 màn chơi.
Kẻ địch là các thói xấu được nhân cách hóa: Quỷ gối (lười biếng, ngủ nướng), quái vật bay (xao nhãng)
và boss "Deadline cận kề" ở màn 5. Mỗi màn được chấm điểm GPA thang 4.

## Yêu cầu

| Thành phần | Phiên bản |
|---|---|
| Unity Editor | **6000.3.10f1** (Unity 6), cài kèm *Windows Build Support (Mono)* |
| Hệ điều hành | Windows 10/11 64 bit |
| Gói Unity chính (tự cài theo `Packages/manifest.json`) | Universal Render Pipeline 17.3 (2D), Input System 1.18, Cinemachine 3.1.6, TextMesh Pro, 2D Tilemap |
| Soạn code (tùy chọn) | Visual Studio Code + extension Unity |
| Công cụ mô phỏng màn (tùy chọn) | Python 3, gcc |

## Cài đặt và chạy trong Unity

1. Tải mã nguồn: `git clone https://github.com/quik11/Game-2D-Unity-khoa-luan.git`
   (hoặc Code → Download ZIP rồi giải nén).
2. Mở **Unity Hub → Projects → Add → Add project from disk**, chọn thư mục **`Source`**
   (thư mục có `Assets`, `Packages`, `ProjectSettings`).
3. Mở bằng Unity **6000.3.10f1**. Lần mở đầu tiên Unity mất vài phút để nhập tài nguyên.
4. Trong cửa sổ Project, mở `Assets/Scenes/MainMenuScene.unity` và bấm **Play**.
   Muốn thử nhanh một màn: mở thẳng `GameplayScene` (màn 1) hoặc `Man2` … `Man6` rồi bấm Play.

## Chơi thử không cần Unity

Bản đã đóng gói cho Windows (khoảng 45 MB) nằm ở mục **Releases** của repo (cột bên phải trang chính):

1. Tải file ZIP trong release mới nhất và giải nén.
2. Chạy `number1.exe` (để nguyên các thư mục `number1_Data`, `MonoBleedingEdge`, `D3D12` và file `UnityPlayer.dll` cạnh nó).

Không cần cài Unity. Nếu Windows hiện cảnh báo SmartScreen, bấm *More info → Run anyway*.

## Đóng gói (build) bản chạy trên Windows

1. **File → Build Profiles → Windows**.
2. Kiểm tra *Scene List* đủ 8 scene, `MainMenuScene` đứng đầu:
   MainMenuScene, GameplayScene, QuitScene, Man2, Man3, Man4, Man5, Man6.
3. Bỏ tick *Development Build*, bấm **Build**, chọn một thư mục nằm ngoài thư mục `Source`.
4. Chạy file `.exe` trong thư mục build. Khi chép sang máy khác phải chép cả thư mục (gồm thư mục `_Data`).

## Điều khiển

| Phím | Hành động |
|---|---|
| A, D / mũi tên | Di chuyển |
| Space | Nhảy (giữ lâu nhảy cao; áp tường trên không thì bật tường) |
| U | Đánh cận chiến (đứng trong cửa đã mở thì qua màn) |
| S + U (trên không) | Đòn dậm |
| I | Đòn mạnh (mở khóa sau màn 1) |
| R | Tơ đạn (2 phát trong 5 giây thì trói kẻ địch) |
| Q | Tơ kéo |
| Left Shift | Lộn né |
| Esc | Tạm dừng |

## Cấu trúc dự án

```
Readme.md               Tài liệu này
Source/                 Project Unity (mở thư mục này bằng Unity Hub)
  Assets/
    Scripts/            Mã nguồn C# chạy trong game (nhân vật, kẻ địch, boss, giao diện, tiến trình)
      PlayerController/ Điều khiển nhân vật chính, tên tham số Animator
      Statemachine/     Damageable (máu, trúng đòn dùng chung), StateMachineBehaviour
      Boss/             Boss "Deadline cận kề", sân đấu, đòn giáng, đồng hồ cát
    Editor/             Công cụ chỉ chạy trong Unity Editor (menu "Brand NEU day"):
                        dựng màn từ file chữ, bảng chỉnh thông số, tạo prefab boss, tạo font
    Scenes/             MainMenuScene, GameplayScene (màn 1), Man2–Man6, QuitScene
    Levels/             Man2.txt … Man6.txt: bản đồ màn chơi viết bằng ký tự
    Resources/          Cốt truyện, chữ hướng dẫn, mẹo chơi (.txt), bộ nền từng màn, font VT323
    Characters/         Prefab và hoạt ảnh nhân vật, Quỷ gối, quái vật bay, boss
    Objects/, UI/       Prefab vật phẩm, viên tơ, cửa qua màn, điểm lưu; giao diện trong màn (GameHUD)
    Art/, music/, Fonts/  Hình ảnh, âm thanh, phông chữ
  Docs/
    CongCu/levelsim/    Công cụ mô phỏng (Python) kiểm tra màn chữ có tới được cửa không
    ThietKeMan.md, ThietKeBoss.md, HuongDanAnimation.md  Ghi chép thiết kế màn, boss, cách gắn hoạt ảnh
  Packages/, ProjectSettings/  Cấu hình project Unity
```

Các file quan trọng: `Source/Assets/Scripts/PlayerController/PlayerController.cs` (nhân vật),
`Source/Assets/Scripts/Statemachine/Damageable.cs` (máu, trúng đòn), `Source/Assets/Scripts/HiepSi.cs` (Quỷ gối),
`Source/Assets/Scripts/FlyingEye.cs` (quái vật bay), `Source/Assets/Scripts/Boss/BossController.cs` (boss),
`Source/Assets/Editor/LevelBuilder.cs` (dựng màn từ file chữ).

## Công cụ hỗ trợ

- **Dựng màn từ file chữ:** Unity → menu **Brand NEU day → Dựng màn từ file chữ...** → chọn `Assets/Levels/ManN.txt`.
  Mỗi ký tự là một ô (`#` đất, `P` xuất phát, `E` Quỷ gối, `F` quái bay, `C` điểm lưu, `X` vực chết, `D` cửa…).
  Dựng lại sẽ ghi đè scene tương ứng.
- **Bảng chỉnh thông số:** menu **Brand NEU day → Bảng chỉnh thông số** (mở khóa mọi màn, thử nâng cấp, chỉnh thông số).
- **Mô phỏng màn:** `python3 Docs/CongCu/levelsim/levelsim.py Assets/Levels/Man3.txt --png man3.png`

## Tài nguyên sử dụng

Nhân vật chính và Quỷ gối do tác giả tự vẽ bằng Piskel. Các tài nguyên miễn phí khác:

- Quái vật bay: *Monsters Creatures Fantasy* – Luiz Melo (itch.io)
- Boss: *Bringer of Death* – Clembod (itch.io)
- Nền: Bongseng (rừng), ansimuz (Mountain Dusk), kayillustrations (Parallax Sunset), Vicente Nitti – vnitti (Glacial Mountains, CC BY 4.0)
- Nhạc: *Legendary JRPG Battle Music Pack* – YouFulca; *Free Music Loop Bundle* – Tallbeard Studios (nhạc của Abstraction)
- Hiệu ứng âm thanh: *RPG Essentials SFX* – Leohpaz
- Phông chữ: VT323 – Peter Hull (SIL Open Font License 1.1); Super Squad
- Biểu tượng: Kyrise's Free 16x16 RPG Icon Pack
- Ô gạch, vật phẩm: các gói miễn phí trong `Source/Assets/Art` (Legacy-Fantasy High Forest, Dungeon Tile Set, Platformer – desert, pixel food…)
