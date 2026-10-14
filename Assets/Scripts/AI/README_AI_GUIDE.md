# 🐀 HƯỚNG DẪN CÀI ĐẶT HỆ THỐNG AI & CẢM BIẾN (PROJECT SHRINK)

> **Tác giả phụ trách**: Phan Ngọc Huy Hoàng (Member / Game AI & Narrative Dev)  
> **Áp dụng**: Tuần 1 (`W1-GC-04`) & Tuần 2 (`W2-GC-05`)

---

## 📁 DANH SÁCH FILE CODE C# ĐÃ TẠO

Toàn bộ code nằm trong thư mục: `UnityScripts/AI/`

1. **`SoundEmitter.cs`**:
   * Hệ thống phát sóng âm toàn cục (Decoupled Sound System).
   * Bất kỳ khi nào Player chạy, nhảy, hoặc ném đồ vật va chạm sàn, chỉ cần gọi `SoundEmitter.Emit(...)`.
2. **`VisionSensor.cs`**:
   * Cảm biến nón thị giác (Field of View Angle, Tầm nhìn xa View Radius).
   * Bắn tia `Physics.Raycast` ngăn quái nhìn xuyên tường hoặc kệ tủ.
   * Vẽ Gizmos nón nhìn (Màu vàng khi tuần tra, chuyển sang Màu đỏ khi thấy Player).
3. **`HearingSensor.cs`**:
   * Cảm biến thính giác. Tự động nhận diện sóng âm từ `SoundEmitter`.
   * Vẽ Gizmos vòng tròn thính giác (Màu xanh cyan, chấm tím đánh dấu vị trí âm thanh).
4. **`AIEntity.cs`**:
   * Lớp cơ sở (Base Class) FSM hoàn chỉnh: `Idle`, `Patrol`, `Alert`, `Chase`, `Attack`, `Dead`.
   * Tự động liên kết và chuyển đổi trạng thái khi các cảm biến bắt được tín hiệu.
5. **`MutantRatAI.cs`**:
   * AI Chuột Đột Biến hoàn chỉnh kế thừa `AIEntity`.
   * Xử lý đi tuần theo Waypoints, nghi ngờ quay đầu tìm kiếm, rượt đuổi người chơi và cắn cận chiến.

---

## 🛠️ CÁCH GẮN VÀO UNITY (3 BƯỚC ĐƠN GIẢN)

### Bước 1: Thiết lập Layer trong Unity
1. Vào **Edit > Project Settings > Tags and Layers**.
2. Thêm 2 Layer:
   * **Player** (gán layer này cho nhân vật Ethan).
   * **Obstacle** (gán layer này cho Tường, Kệ tủ sắt, Thùng gỗ che tầm nhìn).

### Bước 2: Tạo Prefab Quái Vật (Chuột Đột Biến)
1. Tạo một GameObject (hoặc kéo model 3D Chuột vào Scene).
2. Thêm các Component sau:
   * `NavMeshAgent` (Điều chỉnh `Radius`, `Height`, `Speed = 2.0`, `Angular Speed = 360`).
   * `CapsuleCollider` (để va chạm).
   * `MutantRatAI` (Script này sẽ tự động gắn kèm `VisionSensor` và `HearingSensor`).
3. Trên Inspector của **VisionSensor**:
   * `Target Mask`: Chọn Layer **Player**.
   * `Obstacle Mask`: Chọn Layer **Obstacle** (và **Default** nếu tường thuộc layer Default).
   * `View Radius`: 10 (mét).
   * `View Angle`: 90 (độ).
4. Trên Inspector của **HearingSensor**:
   * `Hearing Radius`: 12 (mét).
5. Trên Inspector của **MutantRatAI**:
   * Kéo các điểm GameObject Waypoint trong Scene vào danh sách `Waypoints`.

### Bước 3: Xem Gizmos trực quan trong Unity Scene View
* Bấm chọn con Quái vật trong Hierarchy.
* Trong cửa sổ **Scene**, bạn sẽ thấy:
  * Vòng tròn màu xanh cyan: **Tầm nghe âm thanh của quái**.
  * Hình nón màu vàng: **Nón thị giác của mắt quái**.
  * Khi đưa Player vào nón nhìn, nón sẽ lập tức đổi sang **màu đỏ** và có tia nối thẳng tới Player!

---

## 🤝 GIAO TIẾP VỚI CÁC THÀNH VIÊN KHÁC TRONG NHÓM

### Giao tiếp với Vương (Player Controller & Ném đồ):
Khi Vương lập trình tiếng bước chân hoặc ném đồ vật, bảo Vương thêm 1 dòng code duy nhất:

```csharp
// Khi Player chạy bộ (bán kính phát tiếng 4m)
ProjectShrink.AI.SoundEmitter.Emit(transform.position, 4f, gameObject);

// Khi đồ vật rơi va chạm sàn (bán kính phát tiếng 8m)
ProjectShrink.AI.SoundEmitter.Emit(collision.contacts[0].point, 8f, gameObject);
```
Ngay lập tức, con Chuột của Hoàng sẽ nghe thấy, chuyển sang trạng thái **`Alert`** và tự động chạy tới vị trí đó để kiểm tra!
