# ⚡ TechnoVerse Client Loader - Project Documentation & Architecture Guide

Tài liệu này cung cấp hướng dẫn toàn diện về kiến trúc, các module chức năng, quy trình xác thực, cơ chế khởi chạy payload và hướng dẫn build dự án **TechnoVerse Loader**.

---

## 📌 1. Tổng Quan Dự Án

- **Tên dự án:** TechnoVerse Client Loader
- **Nền tảng / Ngôn ngữ:** C# (.NET 10.0-windows), Windows Forms (Custom Cyberpunk Theme)
- **Kiến trúc đích:** `win-x64`
- **Mục tiêu:** Ứng dụng Desktop dành cho khách hàng của TechnoVerse Bot/Backend, hỗ trợ xác thực bằng tài khoản Discord, đồng bộ các sản phẩm đã mua, tự động tải/giải mã/inject/chạy các tool hoặc game payload với cơ chế bảo mật ẩn danh (Stealth Mode) và tốc độ mở tức thì (Zero-Delay Launch).

---

## 🏗️ 2. Cấu Trúc Thư Mục & Các Thành Phần Chính

```
TechnoVerse_Loader/
├── Config/
│   └── AppConfig.cs             # Quản lý cấu hình, HWID, Server URL, lưu trữ %LocalAppData%
├── Services/
│   ├── AntiDebugService.cs      # Cơ chế chống Reverse Engineering, Debugger, Sandbox
│   ├── ApiService.cs            # Giao tiếp HTTP REST với Backend (bỏ qua cảnh báo ngrok)
│   ├── CheeseHookLoader.cs      # Inject DLL hook cho game (VALORANT) & tự dọn dẹp khi game tắt
│   ├── DownloadLaunchService.cs # Tải payload, hash SHA256, giải mã, tắt tiến trình cũ & khởi chạy
│   └── PayloadSecurityService.cs# Xử lý giải mã payload TVBIN1 / AES
├── UI/
│   ├── Components/
│   │   └── CyberButton.cs       # Custom button giao diện viền neon, hiệu ứng hover/pressed
│   ├── MainForm.cs              # Form chính: Login, Dashboard, Callback listener, đa tùy chọn
│   └── Theme.cs                 # Bảng màu Dark Cyber (#090d16, #111827, #10b981, #38bdf8...)
├── app.manifest                 # Yêu cầu quyền Administrator (requireAdministrator)
├── build.bat                    # Script build tự động cả 2 bản (Standalone & Dependent)
├── publish_to_bot.bat           # Build và sao chép trực tiếp vào kho lưu trữ backend của bot
├── Program.cs                   # Entry point, Single Instance Mutex, Khởi động Anti-Debug
└── TechnoVerseLoader.csproj     # Project file (.NET 10.0-windows, WinForms, SingleFile)
```

---

## 🔐 3. Các Quy Trình Cốt Lõi (Core Flows)

### 3.1. Quy trình Xác Thực Discord (OAuth2 Flow)
1. **Khởi tạo:**
   - Loader mở một `HttpListener` cục bộ tại `http://127.0.0.1:39182/callback/`.
   - Loader mở trình duyệt mặc định với URL OAuth:
     `https://<server-domain>/api/loader/discord/oauth?port=39182`.
2. **Ủy quyền:**
   - Người dùng đăng nhập và bấm **Authorize** trên Discord.
   - Discord chuyển hướng về backend (`/api/loader/discord/callback`).
   - Backend chuyển tiếp trình duyệt về `http://127.0.0.1:39182/callback/?discordId=...&username=...`.
3. **Phản hồi & Kích hoạt:**
   - `HttpListener` nhận request, trả về mã HTML thông báo *"Xác thực thành công"*.
   - Loader tự động kích hoạt cửa sổ nhảy lên trên cùng (`TopMost = true; TopMost = false; Activate(); Focus();`).
   - Loader gọi backend xác thực HWID và lấy danh sách sản phẩm còn hạn.

### 3.2. Quy trình Khởi Chạy Payload (Payload Execution & Zero-Delay)
1. **Kiểm tra & Tải:**
   - Kiểm tra hash SHA-256 trong thư mục cache:
     - Với tool thông thường: `%LocalAppData%\TechnoVerse\Payloads\<ProductKey>\`
     - Với Emulator/Stealth: `%LocalAppData%\TechnoVerse\.cache\sys_bin\` (thư mục ẩn).
   - Nếu file đã tồn tại và hash khớp, bỏ qua bước tải mạng để khởi chạy ngay lập tức.
2. **Tắt tiến trình cũ siêu tốc (`KillEmulatorProcesses`):**
   - Quét trực tiếp danh sách tiến trình trong RAM qua Win32 .NET Process API.
   - Không chạy các lệnh shell `taskkill.exe` ngoài để loại bỏ hoàn toàn độ trễ 4-5s.
3. **Thực thi:**
   - **Đối với file `.dll`:** Sử dụng `CheeseHookLoader` để inject module vào game mục tiêu (VD: VALORANT). Khi game tắt, module tự động unhook và xóa sạch DLL khỏi ổ đĩa.
   - **Đối với file `.exe` / `.bat`:** Khởi chạy bằng `Process.Start` dưới quyền Administrator (`Verb = "runas"`), `UseShellExecute = true`. Không dùng bất kỳ `Task.Delay` giả lập nào.
   - **Tiêu thụ License:** Nếu gói mua là dạng dùng 1 lần (One-Time), Loader tự động gọi API `ConsumeKeyAsync` để vô hiệu hóa key.

---

## 🛠️ 4. Hướng Dẫn Build & Phát Hành

### Yêu Cầu Môi Trường
- **.NET 10.0 SDK** (x64)
- Đường dẫn dotnet cài đặt: `C:\Users\qcan\AppData\Local\Microsoft\dotnet\dotnet.exe`

### Lệnh Build

#### 1. Bản Framework-dependent (Dung lượng nhỏ ~600KB, mở siêu nhanh)
```powershell
& "C:\Users\qcan\AppData\Local\Microsoft\dotnet\dotnet.exe" publish -c Release -r win-x64 --no-self-contained -o ./dist/latest
```

#### 2. Bản Standalone (Dung lượng ~50MB, tự kèm .NET Runtime, không cần cài đặt .NET)
```powershell
& "C:\Users\qcan\AppData\Local\Microsoft\dotnet\dotnet.exe" publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist/latest-standalone
```

#### 3. Đồng bộ vào Backend Server để Bot phân phối
```powershell
Copy-Item -Path "./dist/latest/TechnoVerseLoader.exe" -Destination "D:\Source\TechnoVerse-backend-main\data\loader_files\_main_loader\TechnoVerseLoader.exe" -Force
```

---

## ⚠️ 5. Các Quy Tắc Kỹ Thuật Quan Trọng

1. **Chính sách bảo mật Trình duyệt:**
   - Khi mở tab OAuth bằng `Process.Start`, trình duyệt coi tab đó là do người dùng/hệ điều hành mở. Trình duyệt hiện đại (Chrome/Edge/Brave) chặn lệnh `window.close()` từ script. Do đó, trang xác thực hiển thị card thành công và Loader dùng cơ chế `TopMost` + `Activate` để tự nhảy lên màn hình phía trước.
2. **Bảo toàn tốc độ Khởi chạy (Zero Delay):**
   - Không thêm các câu lệnh `await Task.Delay(...)` hoặc `Thread.Sleep(...)` vào quy trình tải và chạy payload trừ khi cần thiết cho I/O lock handling.
   - Tránh gọi các công cụ dòng lệnh phụ trợ (`taskkill`, `cmd.exe`) khi có thể thao tác trực tiếp qua API của hệ điều hành / .NET.
3. **Escaping String trong C# Interpolation:**
   - Trong chuỗi `$@"..."` ở `MainForm.cs`, bất kỳ cú pháp JavaScript `{` hoặc `}` đều phải được escape thành `{{` và `}}`.
