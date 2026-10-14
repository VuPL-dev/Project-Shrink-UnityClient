using System;
using System.Collections;
using UnityEngine;

namespace ProjectShrink.AI
{
    /// <summary>
    /// Cảm biến thị giác (Vision Sensor) cho quái vật (Chuột đột biến, Robot an ninh).
    /// Hỗ trợ nón nhìn (Field of View - FOV), kiểm tra vật cản bằng Raycast và vẽ Gizmos trực quan.
    /// Hoạt động realtime ngay cả trong Scene View (Edit Mode) lẫn khi bấm Play.
    /// Thuộc gói công việc Tuần 1: W1-GC-04.
    /// </summary>
    [ExecuteAlways]
    public class VisionSensor : MonoBehaviour
    {
        [Header("--- Cấu hình Tầm nhìn (FOV) ---")]
        [Tooltip("Điểm đặt mắt quái vật. Nếu để trống sẽ lấy chính Transform này.")]
        [SerializeField] private Transform eyePoint;

        [Tooltip("Bán kính tối đa quái có thể nhìn thấy (mét).")]
        [SerializeField] private float viewRadius = 10f;

        [Range(0, 360)]
        [Tooltip("Góc quét hình nón của mắt (độ).")]
        [SerializeField] private float viewAngle = 90f;

        [Tooltip("Layer chứa mục tiêu (Player).")]
        [SerializeField] private LayerMask targetMask = ~0; // Mặc định quét mọi Layer để test nhanh

        [Tooltip("Layer chứa vật cản che khuất (Tường, Hộp gỗ).")]
        [SerializeField] private LayerMask obstacleMask;

        [Header("--- Tần số quét (Tối ưu hiệu năng khi Play) ---")]
        [Tooltip("Chu kỳ quét tầm nhìn (giây). 0.1s nghĩa là quét 10 lần/giây thay vì mỗi frame.")]
        [SerializeField] private float scanInterval = 0.1f;

        [Header("--- Trạng thái hiện tại ---")]
        [SerializeField] private bool targetInSight = false;
        [SerializeField] private Transform currentTarget;

        // Events thông báo khi phát hiện hoặc mất dấu mục tiêu
        public event Action<Transform> OnTargetSpotted;
        public event Action OnTargetLost;

        public bool HasTargetInSight => targetInSight;
        public Transform CurrentTarget => currentTarget;
        public float ViewRadius => viewRadius;
        public float ViewAngle => viewAngle;

        private Transform Eye => eyePoint != null ? eyePoint : transform;

        private void Start()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(ScanRoutine());
            }
        }

        private IEnumerator ScanRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(scanInterval);
            while (true)
            {
                yield return wait;
                ScanForTargets();
            }
        }

        /// <summary>
        /// Thực hiện quét các đối tượng lọt vào nón thị giác.
        /// </summary>
        public void ScanForTargets()
        {
            Transform eye = Eye;
            if (eye == null) return;

            Collider[] targetsInRadius = Physics.OverlapSphere(eye.position, viewRadius, targetMask);

            bool foundTargetThisScan = false;
            Transform spottedTarget = null;

            for (int i = 0; i < targetsInRadius.Length; i++)
            {
                Collider col = targetsInRadius[i];
                if (col == null) continue;

                // 1. Bỏ qua chính bản thân con quái vật
                if (col.transform.root == transform.root) continue;

                // 2. Lấy trọng tâm (Center) của Collider mục tiêu
                Vector3 targetCenter = col.bounds.center;
                Vector3 dirToTarget = (targetCenter - eye.position).normalized;

                // 3. Kiểm tra góc nhìn (so với hướng Forward của con bọ)
                float angleToTarget = Vector3.Angle(eye.forward, dirToTarget);
                if (angleToTarget <= viewAngle / 2f)
                {
                    float distToTarget = Vector3.Distance(eye.position, targetCenter);

                    // 4. Bắn Raycast kiểm tra vật cản (nếu obstacleMask != 0)
                    if (obstacleMask == 0 || !Physics.Raycast(eye.position, dirToTarget, distToTarget, obstacleMask))
                    {
                        foundTargetThisScan = true;
                        spottedTarget = col.transform;
                        break; // Đã thấy mục tiêu đầu tiên
                    }
                }
            }

            if (foundTargetThisScan)
            {
                currentTarget = spottedTarget;
                if (!targetInSight)
                {
                    targetInSight = true;
                    OnTargetSpotted?.Invoke(currentTarget);
                }
            }
            else
            {
                if (targetInSight)
                {
                    targetInSight = false;
                    currentTarget = null;
                    OnTargetLost?.Invoke();
                }
            }
        }

        /// <summary>
        /// Chuyển đổi một góc theo hướng nhìn thành Vector phương hướng (phục vụ vẽ Gizmos).
        /// </summary>
        public Vector3 DirFromAngle(float angleInDegrees, bool isGlobal)
        {
            if (!isGlobal)
            {
                angleInDegrees += Eye.eulerAngles.y;
            }
            return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
        }

        #region Gizmos Editor Visualization
        private void OnDrawGizmosSelected()
        {
            Transform eye = Eye;
            if (eye == null) return;

            // Quét liên tục ngay cả trong Scene View mà không cần bấm Play!
            ScanForTargets();

            // Màu của nón nhìn: Đỏ rực nếu đang phát hiện mục tiêu, Vàng nếu chưa thấy ai
            Gizmos.color = targetInSight ? new Color(1f, 0f, 0f, 0.5f) : new Color(1f, 0.92f, 0.016f, 0.35f);
            
            // Vẽ nón tầm nhìn
            Vector3 viewAngleA = DirFromAngle(-viewAngle / 2f, false);
            Vector3 viewAngleB = DirFromAngle(viewAngle / 2f, false);

            Gizmos.DrawWireSphere(eye.position, viewRadius);

            Gizmos.color = targetInSight ? Color.red : Color.yellow;
            Gizmos.DrawLine(eye.position, eye.position + viewAngleA * viewRadius);
            Gizmos.DrawLine(eye.position, eye.position + viewAngleB * viewRadius);

            // Nối tia thẳng tới mục tiêu khi phát hiện được
            if (targetInSight && currentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(eye.position, currentTarget.position);
                Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
            }
        }
        #endregion
    }
}
