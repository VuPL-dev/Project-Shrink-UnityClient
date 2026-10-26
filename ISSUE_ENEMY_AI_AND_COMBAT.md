# [Feature/AI & Gameplay] Triển khai Trí tuệ Nhân tạo Enemy (NavMesh + FSM + Sensors) & Cơ chế Combat 2 chiều (W2-GC-05 & W2-GC-06)

## 📖 1. Context & Objective (Bối cảnh & Mục tiêu)
Thực hiện gói công việc Tuần 2 theo **Phiếu giao việc Capstone FA26SE278 (Phan Ngọc Huy Hoàng - SE183125)**:
* **Mục tiêu 1 (W2-GC-05)**: Nâng cấp AI cho Enemy (Chuột đột biến / Côn trùng) với máy trạng thái hữu hạn FSM (Patrol, Alert/Search, Chase, Attack) trên NavMesh, tích hợp cảm biến thị giác (Vision Sensor - FOV cone & Raycast) và thính giác (Hearing Sensor - lắng nghe bước chân và tiếng đồ vật va chạm).
* **Mục tiêu 2 (W2-GC-06)**: Hoàn thiện cơ chế Combat 2 chiều tương tác trực tiếp giữa Player và Enemy:
  * Player tung đòn đánh (đấm, móc, đá) -> Quái nhận sát thương, giật lùi, chết và rơi bình máu (`health_syringe`).
  * Quái áp sát cắn/cào -> Player nhận sát thương, trừ thanh máu HUD, chết và hồi sinh tại điểm Checkpoint.
* **Mục tiêu 3 (W2-GC-07)**: Tích hợp module đồng bộ thông số runtime `GameConfigSync.cs` kết nối REST API cấu hình từ Web Admin CMS.

---

## 🎯 2. Acceptance Criteria (Tiêu chí nghiệm thu - Definition of Done)

### 🧠 A. AI Enemy & Cảm biến (Sensors & FSM)
- [ ] Enemy di chuyển thông minh bằng `NavMeshAgent` (tự né vật cản, có fallback di chuyển mượt khi chưa bake NavMesh).
- [ ] Cảm biến thị giác (Vision Sensor):
  - [ ] Quét hình nón FOV với góc nhìn `viewAngle` và tầm nhìn `viewRadius`.
  - [ ] Raycast kiểm tra vật cản (`obstacleMask`), không nhìn xuyên tường/cửa.
- [ ] Cảm biến thính giác (Hearing Sensor):
  - [ ] Nhận sóng âm từ `SoundEmitter` khi Player chạy hoặc nhảy.
  - [ ] Chuyển sang trạng thái Cảnh giác (`Alert/Search`), quay đầu và di chuyển đến vị trí phát ra âm thanh.
- [ ] FSM 4 trạng thái hoạt động mượt mà:
  - `Patrol`: Tuần tra giữa các điểm Waypoints (hoặc tuần tra bán kính).
  - `Alert`: Nghi ngờ, xoay tìm mục tiêu khi nghe tiếng động.
  - `Chase`: Tăng tốc rượt đuổi mục tiêu khi nhìn thấy.
  - `Attack`: Tấn công khi vào cự ly áp sát (< 0.35m - 1.2m).

### ⚔️ B. Combat 2 chiều (Player vs Enemy)
- [ ] Player đánh Enemy:
  - [ ] Đấm thẳng (J / Chuột Trái) gây 15 sát thương.
  - [ ] Đòn đá (L / E / O) gây 20-25 sát thương.
  - [ ] Enemy nhận sát thương (`TakeDamage`), phát hiệu ứng hit reaction (`gethit`).
  - [ ] Enemy chết (`Die`): Phát animation `death`, tắt collider, văng bình máu `health_syringe` có thể nhặt được.
- [ ] Enemy đánh Player:
  - [ ] Đòn cắn trúng Player trừ máu qua `PlayerHealth.TakeDamage(damage)`.
  - [ ] Cập nhật thanh máu HUD trên màn hình mượt mà.
  - [ ] Khi máu về 0: Player kích hoạt trạng thái tử trận (`Die`).

### 🌐 C. Đồng bộ thông số (`GameConfigSync`)
- [ ] Script `GameConfigSync.cs` tự động gọi API `GET /api/v1/game/attributes`.
- [ ] Cập nhật realtime các chỉ số: Máu tối đa, Sát thương, Tốc độ của Player và Enemy.
- [ ] Cơ chế Fallback an toàn: Dùng thông số Inspector nếu server chưa bật hoặc mất kết nối mạng.

---

## 📂 3. Files Impacted / Created
* `Assets/EnemyAI.cs`: Nâng cấp NavMesh, FSM 4 trạng thái, tích hợp cảm biến Vision & Hearing, Combat 2 chiều.
* `Assets/PlayerController.cs`: Tích hợp phát sóng âm bước chân (`SoundEmitter`) và khớp hit box đòn đánh.
* `Assets/Scripts/AI/SoundEmitter.cs`: Hệ thống phát âm thanh toàn cục.
* `Assets/Scripts/Config/GameConfigSync.cs`: Script kết nối Web API đồng bộ thông số.
* `ISSUE_ENEMY_AI_AND_COMBAT.md`: Tài liệu issue nghiệm thu.

## 👤 Assignee
* **Phan Ngọc Huy Hoàng (SE183125)** - Game AI & Narrative Dev
