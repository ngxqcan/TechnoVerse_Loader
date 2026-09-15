# ⚡ TechnoVerse Client Loader (Discord Authentication Edition)

Ứng dụng **Client Loader** chuyên dụng dành cho hệ sinh thái **TechnoVerse Bot** (`C:\Users\Administrator\Downloads\TechnoVerse-bot`), hỗ trợ **Đăng nhập bằng tài khoản Discord** và tự động đồng bộ danh sách tất cả các sản phẩm đã mua của khách hàng.

---

## 🌟 Tính Năng Nổi Bật

1. **Đăng Nhập Bằng Discord Siêu Tiện Lợi**:
   - Khách hàng không cần phải nhớ hay tìm lại chuỗi mã License Key dài.
   - **Cách 1 - Qua trình duyệt**: Bấm nút **`🔷 ĐĂNG NHẬP BẰNG DISCORD`**, Loader mở trình duyệt web xác nhận và tự động đăng nhập callback về Loader.
   - **Cách 2 - Nhập trực tiếp**: Nhập Discord User ID (hoặc mã được cấp) để đăng nhập tức thì.
   - Tự động ghi nhớ tài khoản cho các lần mở sau, mở lên là vào thẳng danh sách sản phẩm.

2. **Tự Động Nhận Diện & Hiển Thị Các Sản Phẩm Đã Mua**:
   - Quét từ hệ thống hóa đơn và kho key của bot (`database.json`).
   - Lọc chính xác các sản phẩm còn hạn của tài khoản Discord (ví dụ: `Internal Vip`, `Emulator No Restart`, `Spoofer`...).
   - Hiển thị đầy đủ thông tin:
     - Tên sản phẩm & Biểu tượng (`📌 INTERNAL VIP`, `🔄 EMULATOR NO RESTART`...).
     - Gói bản quyền: `Vĩnh viễn (Lifetime)` hoặc thời hạn còn lại.
     - **Nút Load riêng biệt cho từng sản phẩm**: `LOAD INTERNAL VIP`, `LOAD SPOOFER`...

3. **Engine Tải & Khởi Chạy Tự Động (Auto-Download & Run as Admin)**:
   - Tải file payload tương ứng từ server qua `/api/loader/payload/download?key=...`.
   - Kiểm tra hash SHA-256 chống tải trùng lặp.
   - Tự động giải nén (nếu là `.zip`) hoặc chạy trực tiếp `.exe` dưới quyền **Administrator**.
   - Tự động đóng Loader sau khi khởi chạy game/tool.

---

## 📁 Vị Trí File Phân Phối
File thực thi đã được build và đặt sẵn trong thư mục kho tải của bot:
```
C:\Users\Administrator\Downloads\TechnoVerse-bot\backend\data\loader_files\_main_loader\TechnoVerseLoader.exe
```
Người dùng tải trực tiếp qua:
```
http://localhost:3000/api/loader/download
```
