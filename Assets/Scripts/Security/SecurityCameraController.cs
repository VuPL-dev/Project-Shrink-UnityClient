using UnityEngine;

namespace ProjectShrink.Security
{
    /// <summary>
    /// Camera An Ninh Quét Góc (Phòng 4 - Khu An Ninh & Camera):
    /// - Xoay quét khu vực bên dưới với đèn Spotlight đổi màu theo trạng thái (Xanh dương/Vàng = Bình thường, Đỏ = Phát hiện Player).
    /// - Khi quét trúng Player (không bị tường/kệ sắt che), kích hoạt báo động gọi Security Drone tới bắn hạ!
    /// </summary>
    public class SecurityCameraController : MonoBehaviour
    {
        [Header("1. Quét Góc Camera")]
        public bool isOnline = true;
        public float sweepAngle = 50f;
        public float sweepSpeed = 1.0f;
        public float phaseOffset = 0f;

        [Header("2. Cảm Biến Phát Hiện")]
        public float viewRadius = 0.85f;
        [Range(10f, 120f)]
        public float viewAngle = 48f;
        public LayerMask obstacleMask = ~0;

        [Header("3. Đèn & Trạng Thái")]
        public Light scannerLight;
        public Transform headPivot;
        public Color safeColor = new Color(0.15f, 0.85f, 1f, 1f);
        public Color alertColor = new Color(1f, 0.12f, 0.12f, 1f);
        public Renderer indicatorRenderer;

        private Quaternion baseLocalRotation;
        private bool isDetectingPlayer = false;
        private float alertTriggerCooldown = 0f;

        void Awake()
        {
            if (headPivot == null) headPivot = transform;
            baseLocalRotation = headPivot.localRotation;

            if (scannerLight == null)
                scannerLight = GetComponentInChildren<Light>(true);
        }

        void Start()
        {
            if (SecurityAlarmSystem.Instance != null)
                SecurityAlarmSystem.Instance.RegisterCamera(this);

            UpdateVisualColor(safeColor);
        }

        public void SetCameraOffline()
        {
            isOnline = false;
            isDetectingPlayer = false;
            if (scannerLight != null) scannerLight.enabled = false;
            UpdateVisualColor(new Color(0.25f, 0.25f, 0.25f, 1f));
        }

        void Update()
        {
            if (!isOnline) return;

            if (alertTriggerCooldown > 0f)
                alertTriggerCooldown -= Time.deltaTime;

            // Quét qua lại nếu chưa khóa mục tiêu, hoặc hơi chậm lại khi phát hiện
            float speedMul = isDetectingPlayer ? 0.25f : 1f;
            float angle = Mathf.Sin((Time.time + phaseOffset) * sweepSpeed * speedMul) * sweepAngle;
            headPivot.localRotation = baseLocalRotation * Quaternion.Euler(0f, angle, 0f);

            // Kiểm tra tầm nhìn phát hiện Player
            bool spotted = CheckPlayerInCone();
            if (spotted)
            {
                isDetectingPlayer = true;
                UpdateVisualColor(alertColor);

                if (alertTriggerCooldown <= 0f && PlayerController.Instance != null)
                {
                    alertTriggerCooldown = 0.8f;
                    if (SecurityAlarmSystem.Instance != null)
                    {
                        SecurityAlarmSystem.Instance.TriggerAlarm(PlayerController.Instance.transform.position, gameObject.name);
                    }
                }
            }
            else
            {
                if (isDetectingPlayer)
                {
                    isDetectingPlayer = false;
                    UpdateVisualColor(safeColor);
                }
            }
        }

        bool CheckPlayerInCone()
        {
            PlayerController pc = PlayerController.Instance;
            if (pc == null) return false;

            Vector3 camPos = headPivot.position;
            Vector3 playerTarget = pc.transform.position + Vector3.up * (pc.playerHeight * 0.5f);
            Vector3 toPlayer = playerTarget - camPos;
            float dist = toPlayer.magnitude;

            if (dist > viewRadius) return false;

            Vector3 dir = toPlayer / Mathf.Max(0.0001f, dist);
            float angleToPlayer = Vector3.Angle(headPivot.forward, dir);
            if (angleToPlayer > viewAngle * 0.5f) return false;

            // Kiểm tra có bị kệ sắt hoặc tường che khuất không
            if (Physics.Raycast(camPos, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.CompareTag("Player") && hit.collider.transform.root != pc.transform.root)
                {
                    return false;
                }
            }

            return true;
        }

        void UpdateVisualColor(Color c)
        {
            if (scannerLight != null)
            {
                scannerLight.color = c;
            }
            if (indicatorRenderer != null && indicatorRenderer.material != null)
            {
                indicatorRenderer.material.color = c;
                if (indicatorRenderer.material.HasProperty("_BaseColor"))
                    indicatorRenderer.material.SetColor("_BaseColor", c);
                if (indicatorRenderer.material.HasProperty("_EmissionColor"))
                    indicatorRenderer.material.SetColor("_EmissionColor", c * 2f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Transform pivot = headPivot != null ? headPivot : transform;
            Gizmos.color = isDetectingPlayer ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(pivot.position, viewRadius);
            Vector3 leftRay = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * pivot.forward * viewRadius;
            Vector3 rightRay = Quaternion.Euler(0, viewAngle * 0.5f, 0) * pivot.forward * viewRadius;
            Gizmos.DrawLine(pivot.position, pivot.position + leftRay);
            Gizmos.DrawLine(pivot.position, pivot.position + rightRay);
        }
    }
}
