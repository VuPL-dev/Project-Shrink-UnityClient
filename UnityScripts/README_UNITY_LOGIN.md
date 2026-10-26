# HƯỚNG DẪN TÍCH HỢP LOGIN API VÀO UNITY GAME

Toàn bộ các file C# vừa tạo đã được cấu trúc theo chuẩn Kiến trúc mạng Game Unity và lưu sẵn tại thư mục:
`UnityScripts/` (Copy thẳng vào thư mục `Assets/` trong project Unity của bạn).

---

## 📁 1. Cấu Trúc Các File C#

* **`UnityScripts/Network/ApiConfig.cs`**:
  * Định nghĩa tập trung địa chỉ `BASE_URL` của Backend (`http://localhost:3000`) và các Endpoint (`/api/auth/login`, `/api/auth/register`, `/api/auth/send-otp`, `/api/auth/verify-otp`).
  * Khi build game ra thiết bị thực tế, bạn chỉ cần đổi `localhost` thành IP máy tính hoặc Domain server tại file này.

* **`UnityScripts/Network/ApiModels.cs`**:
  * Định nghĩa các Class Serializable (`LoginRequest`, `AuthResponse`, `UserInfo`, `ApiResponse<T>`) tương thích 100% với JSON trả về từ Controller `authController.js` của BE_Doan.

* **`UnityScripts/Network/AuthManager.cs`**:
  * Quản lý phiên mạng (Network Session) dạng **Singleton** (`DontDestroyOnLoad`).
  * Thực hiện gọi API bất đồng bộ qua `UnityWebRequest` (xử lý timeout, parse lỗi từ server).
  * **Tự động lưu Token:** Sau khi đăng nhập thành công, token được lưu vào `PlayerPrefs` để người chơi không phải đăng nhập lại mỗi lần mở game.
  * Tự động gắn header `Authorization: Bearer <token>` cho mọi request sau đó.

* **`UnityScripts/UI/LoginUIController.cs`**:
  * Gắn vào UI Canvas trong Scene Login của Unity.
  * Lắng nghe sự kiện click nút Đăng nhập / Gửi OTP / Xác thực OTP, hiển thị thông báo lỗi/thành công và tự động chuyển sang Scene game chính (`SampleScene`).

---

## 🎮 2. Các Bước Thiết Lập Trong Unity Editor

1. **Copy Scripts vào Unity:**
   * Kéo toàn bộ thư mục `UnityScripts/` vào thư mục `Assets/` của dự án Unity.

2. **Tạo GameObject AuthManager:**
   * Trong Scene Login, tạo một GameObject rỗng (Empty GameObject), đặt tên là `AuthManager`.
   * Gắn script `AuthManager.cs` vào GameObject này.

3. **Gắn Script vào UI Canvas:**
   * Tạo Canvas UI đăng nhập (gồm InputField `Username`, `Password`, Button `Login`, Text `StatusMessage`).
   * Gắn script `LoginUIController.cs` vào Canvas hoặc Panel Login.
   * Kéo thả các thành phần UI tương ứng vào các ô trường trong Inspector:
     * `Input Username` -> InputField nhập tên
     * `Input Password` -> InputField nhập mật khẩu
     * `Btn Login` -> Nút bấm Đăng nhập
     * `Txt Status Message` -> Text hiển thị thông báo
     * `Next Scene Name` -> Tên Scene cần chuyển đến sau khi đăng nhập (mặc định là `SampleScene`).

---

## 🧪 3. Tài Khoản Demo Sẵn Có Để Test Ngay

Bạn có thể chạy thử ngay trên Unity Editor với các tài khoản đã được nạp sẵn trong Database:
* **Admin:** Username: `admin_demo` | Password: `Admin@123`
* **Moderator:** Username: `mod_demo` | Password: `Mod@123`
