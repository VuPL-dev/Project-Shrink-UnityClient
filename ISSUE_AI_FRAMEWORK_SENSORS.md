# [Feature/AI] Khởi tạo Framework AI & Sensors (W1-GC-04)

## 📖 1. Context & Description (Bối cảnh & Mục tiêu)
Thực hiện gói công việc **Tuần 1 (Task W1-GC-04)** thuộc phân hệ **Game AI & Narrative** do **Phan Ngọc Huy Hoàng** phụ trách:
* Xây dựng kiến trúc nền tảng AI cơ sở cho các quái vật trong game.
* Hiện thực hóa Cảm biến thị giác (Vision Sensor) với nón nhìn FOV và Raycast chặn nhìn xuyên tường.
* Hiện thực hóa Cảm biến thính giác (Hearing Sensor) lắng nghe sóng âm bước chân và đồ vật ném.
* Chuẩn bị sẵn sàng cấu trúc FSM để bước sang Tuần 2 làm chi tiết hành vi Chuột Đột Biến (W2-GC-05).

---

## 🎯 2. Acceptance Criteria (Tiêu chí hoàn thành Tuần 1 - DoD)

### 👁️ Cảm biến Thị giác (VisionSensor.cs)
- [x] Tạo nón thị giác hình nón (FOV) có thể tùy chỉnh `viewAngle` và `viewRadius`.
- [x] Bắn tia `Physics.Raycast` chặn quái nhìn xuyên qua tường hoặc vật cản (`obstacleMask`).
- [x] Tự động bỏ qua chính bản thân quái vật (`col.transform.root == transform.root`).
- [x] Vẽ Gizmos trực quan trong Unity Editor:
  - Màu vàng khi tuần tra bình thường.
  - Tự động chuyển sang màu đỏ rực và khóa tia mục tiêu khi phát hiện Player.
- [x] Hỗ trợ `[ExecuteAlways]` quét Realtime ngay trong Scene View mà không cần bấm Play.

### 👂 Cảm biến Thính giác (HearingSensor.cs & SoundEmitter.cs)
- [x] Xây dựng bộ phát sóng âm toàn cục `SoundEmitter.cs` để Player (chạy bộ, nhảy) và đồ vật rơi có thể phát ra âm thanh.
- [x] `HearingSensor.cs` tự động bắt sóng âm, tính toán khoảng cách theo bán kính `hearingRadius` và độ nhạy `hearingSensitivity`.
- [x] Vẽ Gizmos vòng cầu thính giác và điểm chấm tím đánh dấu vị trí phát ra âm thanh.

### 🧠 Base AI Entity (AIEntity.cs)
- [x] Lớp cha định nghĩa vòng đời FSM chuẩn: `Idle`, `Patrol`, `Alert`, `Chase`, `Attack`, `Dead`.
- [x] Tự động liên kết với 2 cảm biến: thấy người thì chuyển trạng thái rượt đuổi, nghe tiếng động thì quay đầu điều tra.

---

## ⏳ 3. Next Steps (Kế hoạch tiếp theo cho Tuần 2: 15/10 - 21/10)
- [ ] **W2-GC-05**: Lập trình chi tiết AI Chuột Đột Biến (Mutant Rat) trên NavMesh của map chính thức.
- [ ] Gắn Animation Controller (Run, Idle, Bite Attack) cho model quái vật.
- [ ] Tích hợp tiếng ném chai lọ từ hệ thống Inventory của Vương để đánh lạc hướng quái.

---

## 📂 4. Files Created
- `UnityScripts/AI/SoundEmitter.cs`
- `UnityScripts/AI/VisionSensor.cs`
- `UnityScripts/AI/HearingSensor.cs`
- `UnityScripts/AI/AIEntity.cs`
- `UnityScripts/AI/MutantRatAI.cs` *(File khung mẫu)*
- `UnityScripts/AI/README_AI_GUIDE.md`

## 👤 Assignee
- Phan Ngọc Huy Hoàng (Member / Game AI & Narrative Dev)
