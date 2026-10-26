using System;
using UnityEngine;

namespace ProjectShrink.AI
{
    /// <summary>
    /// Cảm biến thính giác (Hearing Sensor) cho quái vật (Chuột đột biến, Robot an ninh).
    /// Lắng nghe sóng âm từ người chơi chạy bộ, nhảy hoặc ném đồ vật xuống sàn.
    /// Thuộc gói công việc Tuần 1: W1-GC-04.
    /// </summary>
    public class HearingSensor : MonoBehaviour
    {
        [Header("--- Cấu hình Thính giác ---")]
        [Tooltip("Bán kính tai nghe tối đa của quái vật (mét).")]
        [SerializeField] private float hearingRadius = 12f;

        [Tooltip("Độ nhạy thính giác (1.0 = bình thường, > 1.0 = thính tai hơn).")]
        [Range(0.5f, 2.5f)]
        [SerializeField] private float hearingSensitivity = 1.0f;

        [Header("--- Trạng thái Âm thanh vừa nghe ---")]
        [SerializeField] private bool heardSoundRecently = false;
        [SerializeField] private Vector3 lastHeardPosition;
        [SerializeField] private float timeSinceLastSound = 999f;
        [SerializeField] private float alertDurationAfterSound = 3f;

        // Event bắn ra khi nghe thấy tiếng động: (Tọa độ nguồn âm, Đối tượng gây tiếng động)
        public event Action<Vector3, GameObject> OnSoundHeard;

        public bool HeardSoundRecently => heardSoundRecently;
        public Vector3 LastHeardPosition => lastHeardPosition;
        public float HearingRadius => hearingRadius;

        private void OnEnable()
        {
            // Tự động đăng ký lắng nghe bộ phát sóng âm toàn cục
            SoundEmitter.OnSoundEmitted += HandleGlobalSoundEmitted;
        }

        private void OnDisable()
        {
            SoundEmitter.OnSoundEmitted -= HandleGlobalSoundEmitted;
        }

        private void Update()
        {
            if (heardSoundRecently)
            {
                timeSinceLastSound += Time.deltaTime;
                if (timeSinceLastSound >= alertDurationAfterSound)
                {
                    heardSoundRecently = false;
                }
            }
        }

        /// <summary>
        /// Xử lý khi có bất kỳ âm thanh nào phát ra trong game.
        /// </summary>
        private void HandleGlobalSoundEmitted(Vector3 soundOrigin, float soundRadius, GameObject source)
        {
            // Bỏ qua nếu âm thanh do chính bản thân quái vật phát ra
            if (source != null && source.transform.root == transform.root) return;

            float distanceToSound = Vector3.Distance(transform.position, soundOrigin);

            // Điều kiện nghe thấy: Khoảng cách <= (Tầm nghe của quái * Độ nhạy) + Bán kính âm thanh phát ra
            float effectiveAudibleDistance = (hearingRadius * hearingSensitivity) + soundRadius;

            if (distanceToSound <= effectiveAudibleDistance)
            {
                lastHeardPosition = soundOrigin;
                timeSinceLastSound = 0f;
                heardSoundRecently = true;

                // Kích hoạt sự kiện để FSM hoặc AIEntity chuyển sang trạng thái Alert/Search
                OnSoundHeard?.Invoke(soundOrigin, source);
            }
        }

        #region Gizmos Editor Visualization
        private void OnDrawGizmosSelected()
        {
            // Vẽ bán kính thính giác màu xanh ngọc / cyan
            Gizmos.color = heardSoundRecently ? new Color(1f, 0.5f, 0f, 0.4f) : new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, hearingRadius);

            // Nếu vừa nghe thấy tiếng động, vẽ marker tại vị trí âm thanh
            if (heardSoundRecently)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(lastHeardPosition, 0.35f);
                Gizmos.DrawLine(transform.position, lastHeardPosition);
            }
        }
        #endregion
    }
}
