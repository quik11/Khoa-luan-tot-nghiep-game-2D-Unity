# Công cụ chuẩn bị nền parallax theo màn

Dùng khi thêm / đổi bộ nền (chạy ngoài Unity, cần Python 3 + Pillow).

1. Giải nén các gói nền vào 1 thư mục (vd `goi/`), sửa danh sách `THEMES` trong `chuan_bi_nen.py`
   (thư mục nguồn, khung chuẩn, thứ tự lớp xa → gần, độ trôi `fx`, mây tự trôi `scroll`, màn dùng).
2. `python3 chuan_bi_nen.py goi/ <thư mục repo> thong_so.json`
   → ghi ảnh + `.meta` vào `Assets/Art/Backgrounds/<bộ>/`, bộ nền vào `Assets/Resources/NenManChoi/<tên>.asset`
   và `Assets/Resources/NenManChoi/DanhSach.txt` (màn nào dùng bộ nào + ghi công).
   Công cụ tự: thu nhỏ ảnh phóng sẵn 2/4/8 lần (không mất chi tiết), cắt phần trong suốt, tô kín đáy lớp trời,
   cắt cột mờ ở mép, lật ngang ghép thêm cho lớp vẫn không lặp liền (dời 1/4 ô để trục gương không ở giữa khung),
   kéo dài đáy lớp lơ lửng (`extend_down`) / mép trên (`extend_up`), phủ sương (`haze`) cho rừng tối trùng màu ô đất,
   đặt kiểu giữ mép (trời 2 mép, lớp chạm đáy giữ mép dưới, mây lơ lửng không giữ).
3. `python3 ve_thu_nen.py thong_so.json <thư mục ra>` → ảnh vẽ thử mỗi bộ ở 5 vị trí camera
   (đầu màn, đi xa 60 ô, leo 15 / 29 ô, rơi 10 ô) theo đúng công thức `LevelBackground` + `ParallaxEffect`.

Chạy lại bước 2 giữ nguyên guid cũ (đọc lại từ .meta) nhưng viết lại file .asset bộ nền → chỉnh trong Inspector sẽ mất,
nên chỉnh xong ở Unity thì ghi số vào `THEMES` cho khớp.
Đổi màn nào dùng nền nào: sửa `DanhSach.txt` bằng Notepad, không cần chạy lại công cụ.
