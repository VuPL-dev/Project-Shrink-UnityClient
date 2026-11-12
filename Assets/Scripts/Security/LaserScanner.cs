using UnityEngine;

namespace ProjectShrink.Security
{
    /// <summary>
    /// Bẫy Tia Laser An Ninh (Phòng 4 - Khu An Ninh & Camera):
    /// - Hỗ trợ 4 chế độ: Tia Cố Định, Quét Qua Lại (Sweep), Xoay Tròn, và Nhấp Nháy Theo Nhịp (Pulse).
    /// - Tự động Raycast bị chắn bởi Kệ Sắt / Tủ / Tường (người chơi có thể nấp sau vật cản).
    /// - Nếu tia laser quét trúng Player (không đang né LeftShift), gây sát thương và kích hoạt Báo Động cho Drone.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LaserScanner : MonoBehaviour
    {
        public enum LaserMode
        {
            Static = 0,
            SweepPingPong = 1,
            RotateContinuous = 2,
            PulseBlink = 3
        }

        [Header("1. Chế Độ Quét Laser")]
        public LaserMode mode = LaserMode.Static;
        public bool isLaserActive = true;
        public float maxLength = 2.0f;
        public float beamWidth = 0.008f;

        [Header("2. Quét / Xoay / Nhấp Nháy")]
        [Tooltip("Biên độ góc quét (độ) khi dùng SweepPingPong")]
        public float sweepAngle = 35f;
        public float sweepSpeed = 1.4f;
        public float rotateSpeed = 55f;
        public float pulseOnDuration = 1.6f;
        public float pulseOffDuration = 1.4f;
        public float phaseOffset = 0f;

        [Header("3. Sát Thương & Va Chạm")]
        public float laserDamage = 18f;
        public float hitCooldown = 0.65f;
        public float hitRadius = 0.024f;
        public LayerMask obstacleMask = ~0;

        [Header("4. Màu Sắc Tia")]
        public Color laserColor = new Color(1f, 0.12f, 0.12f, 1f);
        public Color warningPulseColor = new Color(1f, 0.55f, 0.1f, 0.5f);

        private LineRenderer lineRenderer;
        private Quaternion baseLocalRotation;
        private float lastPlayerHitTime = -10f;
        private Transform impactDot;

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            baseLocalRotation = transform.localRotation;
            SetupLineRenderer();
        }

        void Start()
        {
            if (SecurityAlarmSystem.Instance != null)
                SecurityAlarmSystem.Instance.RegisterLaser(this);
        }

        void SetupLineRenderer()
        {
            if (lineRenderer == null) return;

            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = beamWidth;
            lineRenderer.endWidth = beamWidth;
            lineRenderer.numCapVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = laserColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", laserColor);
                lineRenderer.material = mat;
            }

            lineRenderer.startColor = laserColor;
            lineRenderer.endColor = laserColor;

            // Tạo điểm sáng nhỏ ở đầu tia laser chạm tường
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "LaserImpactDot";
            dot.transform.SetParent(transform, false);
            dot.transform.localScale = Vector3.one * (beamWidth * 2.2f);
            Collider col = dot.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = dot.GetComponent<Renderer>();
            if (r != null && shader != null)
            {
                Material dotMat = new Material(shader);
                dotMat.color = new Color(1f, 0.45f, 0.45f, 1f);
                if (dotMat.HasProperty("_BaseColor")) dotMat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.45f, 1f));
                r.material = dotMat;
            }
            impactDot = dot.transform;
        }

        public void SetLaserEnabled(bool enabledState)
        {
            isLaserActive = enabledState;
            if (lineRenderer != null) lineRenderer.enabled = enabledState;
            if (impactDot != null) impactDot.gameObject.SetActive(enabledState);
        }

        void Update()
        {
            if (!isLaserActive)
            {
                if (lineRenderer != null && lineRenderer.enabled) lineRenderer.enabled = false;
                if (impactDot != null && impactDot.gameObject.activeSelf) impactDot.gameObject.SetActive(false);
                return;
            }

            float t = Time.time + phaseOffset;

            // 1. Chế độ nhấp nháy
            bool beamCurrentlyOn = true;
            bool isPreWarning = false;
            if (mode == LaserMode.PulseBlink)
            {
                float cycle = pulseOnDuration + pulseOffDuration;
                float mod = Mathf.Repeat(t, cycle);
                beamCurrentlyOn = mod < pulseOnDuration;
                if (!beamCurrentlyOn && (cycle - mod) < 0.35f)
                {
                    isPreWarning = true;
                }
            }

            // 2. Xoay / Quét
            if (mode == LaserMode.SweepPingPong)
            {
                float angleOffset = Mathf.Sin(t * sweepSpeed) * sweepAngle;
                transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, angleOffset, 0f);
            }
            else if (mode == LaserMode.RotateContinuous)
            {
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
            }

            if (!beamCurrentlyOn && !isPreWarning)
            {
                lineRenderer.enabled = false;
                if (impactDot != null) impactDot.gameObject.SetActive(false);
                return;
            }

            lineRenderer.enabled = true;

            // 3. Raycast kiểm tra điểm cuối bị chắn bởi tường/kệ
            Vector3 startPos = transform.position;
            Vector3 dir = transform.forward;
            float currentLength = maxLength;

            if (Physics.Raycast(startPos, dir, out RaycastHit hit, maxLength, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.CompareTag("Player"))
                {
                    currentLength = hit.distance;
                }
            }

            Vector3 endPos = startPos + dir * currentLength;
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, endPos);

            if (isPreWarning)
            {
                lineRenderer.startWidth = beamWidth * 0.35f;
                lineRenderer.endWidth = beamWidth * 0.35f;
                lineRenderer.startColor = warningPulseColor;
                lineRenderer.endColor = warningPulseColor;
                if (impactDot != null) impactDot.gameObject.SetActive(false);
                return;
            }

            // Hiệu ứng rung nhẹ cường độ sáng tia laser
            float pulseWidth = beamWidth * (0.9f + 0.15f * Mathf.Sin(Time.time * 24f));
            lineRenderer.startWidth = pulseWidth;
            lineRenderer.endWidth = pulseWidth;
            lineRenderer.startColor = laserColor;
            lineRenderer.endColor = laserColor;

            if (impactDot != null)
            {
                impactDot.gameObject.SetActive(true);
                impactDot.position = endPos;
            }

            // 4. Kiểm tra va chạm với Player
            CheckPlayerIntersection(startPos, endPos);
        }

        void CheckPlayerIntersection(Vector3 a, Vector3 b)
        {
            PlayerController pc = PlayerController.Instance;
            if (pc == null) return;
            if (pc.isDodging) return; // Lướt né LeftShift vượt qua tia laser an toàn!

            Vector3 playerPos = pc.transform.position;
            float pHeight = Mathf.Max(0.05f, pc.playerHeight);

            // Kiểm tra 2 điểm trên thân Player (chân và ngực) tới đoạn thẳng tia Laser
            Vector3 pLow = playerPos + Vector3.up * (pHeight * 0.25f);
            Vector3 pHigh = playerPos + Vector3.up * (pHeight * 0.78f);

            float distLow = DistancePointToSegment(pLow, a, b);
            float distHigh = DistancePointToSegment(pHigh, a, b);
            float minDist = Mathf.Min(distLow, distHigh);

            if (minDist <= hitRadius)
            {
                if (Time.time - lastPlayerHitTime >= hitCooldown)
                {
                    lastPlayerHitTime = Time.time;

                    PlayerHealth ph = pc.GetComponent<PlayerHealth>();
                    if (ph != null)
                    {
                        ph.TakeDamage(laserDamage);
                    }

                    if (SecurityAlarmSystem.Instance != null)
                    {
                        SecurityAlarmSystem.Instance.TriggerAlarm(playerPos, gameObject.name);
                    }

                    Debug.Log($"⚡ [LASER HIT] Tia {gameObject.name} bắn trúng người chơi (-{laserDamage} HP)!");
                }
            }
        }

        static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < 0.000001f) return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / sqrLen);
            Vector3 proj = a + ab * t;
            return Vector3.Distance(p, proj);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = isLaserActive ? Color.red : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.015f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * maxLength);
        }
    }
}
