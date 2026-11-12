using System.Collections.Generic;
using UnityEngine;
using ProjectShrink.AI;

namespace ProjectShrink.Security
{
    /// <summary>
    /// Robot An Ninh Bay Tuần Tra Bắn Laser (Phòng 4 - Khu An Ninh & Camera):
    /// - Bay lơ lửng tuần tra theo Waypoint / Lãnh thổ, quay cánh quạt động cơ.
    /// - Cảm biến Thị giác (FOV + Raycast tường/kệ) & Cảm biến Thính giác (SoundEmitter).
    /// - Chuỗi chiến đấu bắn Laser công bằng: Khóa mục tiêu & Chiếu tia ngắm cảnh báo (0.85s) -> Bắn chùm Laser năng lượng cao!
    /// - Người chơi có thể né tia laser bằng phím [LeftShift] hoặc nấp sau các Kệ Sắt / Tủ, hoặc áp sát đánh hạ Drone.
    /// </summary>
    public class SecurityDroneAI : MonoBehaviour
    {
        public static readonly List<SecurityDroneAI> ActiveDrones = new List<SecurityDroneAI>();

        public enum DroneState
        {
            Patrol,
            Alert,
            Aiming,
            Cooldown,
            Destroyed
        }

        [Header("1. Thông Tin & Máu")]
        public string droneName = "Robot An Ninh (Drone)";
        public float maxHealth = 60f;
        public float currentHealth = 60f;
        public GameObject dropItemPrefab;

        [Header("2. Bay & Tuần Tra")]
        public float patrolSpeed = 0.24f;
        public float chaseSpeed = 0.34f;
        public float turnSpeed = 7f;
        public float hoverBobAmplitude = 0.012f;
        public float hoverBobSpeed = 3.2f;
        public Vector3 territoryCenter;
        public float territoryRadius = 0.55f;
        public Transform[] waypoints;

        [Header("3. Cảm Biến Thị Giác & Thính Giác")]
        public float viewRadius = 0.95f;
        [Range(20f, 180f)]
        public float viewAngle = 80f;
        public float hearingRadius = 1.25f;
        public LayerMask obstacleMask = ~0;

        [Header("4. Vũ Khí Laser")]
        public float attackRange = 0.85f;
        public float preferredCombatDist = 0.32f;
        public float aimDuration = 0.85f;
        public float fireDisplayDuration = 0.22f;
        public float attackCooldown = 1.4f;
        public float laserDamage = 18f;

        [Header("5. Tham Chiếu Visual")]
        public Transform rotorLeft;
        public Transform rotorRight;
        public Transform muzzleTransform;
        public Light scannerLight;
        public Renderer eyeRenderer;
        public LineRenderer laserLine;

        [Header("6. Trạng Thái (Read Only)")]
        [SerializeField] private DroneState currentState = DroneState.Patrol;

        private float baseHoverY;
        private int waypointIndex = 0;
        private Vector3 alertTargetPos;
        private float alertTimer = 0f;
        private float aimTimer = 0f;
        private float fireShowTimer = 0f;
        private float cooldownTimer = 0f;
        private Vector3 lockedTargetPoint;
        private float displayedHpRatio = 1f;
        private Camera mainCamera;

        private static GUIStyle s_hpLabelStyle;
        private readonly Vector3[] autoPatrolPoints = new Vector3[4];

        public bool IsAlive => currentState != DroneState.Destroyed && currentHealth > 0f;

        void Awake()
        {
            if (!ActiveDrones.Contains(this)) ActiveDrones.Add(this);
            currentHealth = maxHealth;
            displayedHpRatio = 1f;
            baseHoverY = transform.position.y;
            if (territoryCenter == Vector3.zero) territoryCenter = transform.position;
            BuildAutoPatrolPoints();
            SetupLaserLine();
        }

        void OnDestroy()
        {
            ActiveDrones.Remove(this);
        }

        void OnEnable()
        {
            if (!ActiveDrones.Contains(this)) ActiveDrones.Add(this);
            SoundEmitter.OnSoundEmitted += HandleSoundHeard;
        }

        void OnDisable()
        {
            ActiveDrones.Remove(this);
            SoundEmitter.OnSoundEmitted -= HandleSoundHeard;
        }

        void Start()
        {
            mainCamera = Camera.main;
            if (SecurityAlarmSystem.Instance != null)
                SecurityAlarmSystem.Instance.RegisterDrone(this);

            UpdateVisualState(new Color(0.18f, 0.85f, 1f));
        }

        void BuildAutoPatrolPoints()
        {
            float r = Mathf.Max(0.18f, territoryRadius);
            autoPatrolPoints[0] = territoryCenter + new Vector3(-r, 0f, -r * 0.5f);
            autoPatrolPoints[1] = territoryCenter + new Vector3(r, 0f, -r * 0.5f);
            autoPatrolPoints[2] = territoryCenter + new Vector3(r, 0f, r * 0.5f);
            autoPatrolPoints[3] = territoryCenter + new Vector3(-r, 0f, r * 0.5f);
        }

        void SetupLaserLine()
        {
            if (laserLine == null) laserLine = GetComponent<LineRenderer>();
            if (laserLine == null) laserLine = gameObject.AddComponent<LineRenderer>();

            laserLine.positionCount = 2;
            laserLine.useWorldSpace = true;
            laserLine.enabled = false;
            laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laserLine.receiveShadows = false;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s != null)
            {
                Material m = new Material(s);
                m.color = Color.red;
                laserLine.material = m;
            }
        }

        void HandleSoundHeard(Vector3 origin, float radius, GameObject source)
        {
            if (!IsAlive) return;
            if (source == gameObject) return;
            if (currentState == DroneState.Aiming) return;

            float dist = Vector3.Distance(transform.position, origin);
            if (dist <= hearingRadius + radius * 0.35f)
            {
                NotifySecurityAlert(origin);
            }
        }

        public void NotifySecurityAlert(Vector3 targetPos)
        {
            if (!IsAlive) return;
            alertTargetPos = targetPos;
            alertTimer = 4.0f;

            if (currentState == DroneState.Patrol)
            {
                currentState = DroneState.Alert;
                UpdateVisualState(new Color(1f, 0.55f, 0.1f));
            }
        }

        void Update()
        {
            if (currentState == DroneState.Destroyed) return;

            if (mainCamera == null) mainCamera = Camera.main;

            // Thanh máu mượt
            float targetRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            displayedHpRatio = Mathf.Lerp(displayedHpRatio, targetRatio, 10f * Time.deltaTime);

            // Xoay cánh quạt & lơ lửng
            AnimateHoverAndRotors();

            // Đang hiện tia bắn sau khi nổ súng
            if (fireShowTimer > 0f)
            {
                fireShowTimer -= Time.deltaTime;
                if (fireShowTimer <= 0f && laserLine != null && currentState != DroneState.Aiming)
                {
                    laserLine.enabled = false;
                }
            }

            PlayerController pc = PlayerController.Instance;
            bool canSeePlayer = CanSeePlayer(pc);

            switch (currentState)
            {
                case DroneState.Patrol:
                    TickPatrol(canSeePlayer);
                    break;
                case DroneState.Alert:
                    TickAlert(canSeePlayer, pc);
                    break;
                case DroneState.Aiming:
                    TickAiming(canSeePlayer, pc);
                    break;
                case DroneState.Cooldown:
                    TickCooldown(canSeePlayer, pc);
                    break;
            }
        }

        void AnimateHoverAndRotors()
        {
            Vector3 p = transform.position;
            p.y = baseHoverY + Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmplitude;
            transform.position = p;

            float spin = 900f * Time.deltaTime;
            if (rotorLeft != null) rotorLeft.Rotate(Vector3.up, spin, Space.Self);
            if (rotorRight != null) rotorRight.Rotate(Vector3.up, -spin, Space.Self);
        }

        void TickPatrol(bool canSeePlayer)
        {
            if (canSeePlayer)
            {
                EnterAimingState();
                return;
            }

            Vector3 target = (waypoints != null && waypoints.Length > 0 && waypoints[waypointIndex] != null)
                ? waypoints[waypointIndex].position
                : autoPatrolPoints[waypointIndex % autoPatrolPoints.Length];

            MoveHorizontalToward(target, patrolSpeed);

            Vector3 flatDiff = target - transform.position;
            flatDiff.y = 0f;
            if (flatDiff.sqrMagnitude <= 0.015f)
            {
                int count = (waypoints != null && waypoints.Length > 0) ? waypoints.Length : autoPatrolPoints.Length;
                waypointIndex = (waypointIndex + 1) % count;
            }
        }

        void TickAlert(bool canSeePlayer, PlayerController pc)
        {
            if (canSeePlayer)
            {
                EnterAimingState();
                return;
            }

            MoveHorizontalToward(alertTargetPos, chaseSpeed);
            alertTimer -= Time.deltaTime;

            if (alertTimer <= 0f)
            {
                currentState = DroneState.Patrol;
                UpdateVisualState(new Color(0.18f, 0.85f, 1f));
            }
        }

        void EnterAimingState()
        {
            currentState = DroneState.Aiming;
            aimTimer = aimDuration;
            UpdateVisualState(new Color(1f, 0.14f, 0.14f));
        }

        void TickAiming(bool canSeePlayer, PlayerController pc)
        {
            if (pc == null || (SecurityAlarmSystem.Instance != null && SecurityAlarmSystem.Instance.IsSecurityHacked && !canSeePlayer))
            {
                laserLine.enabled = false;
                currentState = DroneState.Patrol;
                UpdateVisualState(new Color(0.18f, 0.85f, 1f));
                return;
            }

            Vector3 playerPos = pc.transform.position;
            Vector3 playerTarget = playerPos + Vector3.up * (pc.playerHeight * 0.55f);

            // Luôn xoay mặt về Player
            RotateToward(playerPos);

            // Nếu Player chạy ra xa hơn tầm bắn hoặc khuất tầm nhìn -> bay đuổi theo
            float flatDist = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(playerPos.x, playerPos.z));
            if (flatDist > attackRange || !canSeePlayer)
            {
                MoveHorizontalToward(playerPos, chaseSpeed);
                laserLine.enabled = false;
                aimTimer = Mathf.Min(aimDuration, aimTimer + Time.deltaTime * 0.5f);
                if (flatDist > viewRadius * 1.4f)
                {
                    currentState = DroneState.Alert;
                    alertTargetPos = playerPos;
                    alertTimer = 3.0f;
                    UpdateVisualState(new Color(1f, 0.55f, 0.1f));
                }
                return;
            }

            if (flatDist > preferredCombatDist)
            {
                MoveHorizontalToward(playerPos, patrolSpeed * 0.8f);
            }

            // Chiếu tia Laser ngắm cảnh báo mỏng (màu vàng/đỏ nhấp nháy)
            Vector3 muzzle = muzzleTransform != null ? muzzleTransform.position : transform.position;
            if (aimTimer > 0.16f)
            {
                lockedTargetPoint = playerTarget;
            }

            laserLine.enabled = true;
            bool flash = Mathf.Repeat(Time.time * 16f, 1f) > 0.35f;
            laserLine.startWidth = 0.0025f;
            laserLine.endWidth = 0.0025f;
            Color aimCol = flash ? new Color(1f, 0.35f, 0.1f, 0.85f) : new Color(1f, 0.1f, 0.1f, 0.35f);
            laserLine.startColor = aimCol;
            laserLine.endColor = aimCol;
            laserLine.SetPosition(0, muzzle);
            laserLine.SetPosition(1, lockedTargetPoint);

            aimTimer -= Time.deltaTime;
            if (aimTimer <= 0f)
            {
                FireLaserShot(muzzle, lockedTargetPoint, pc);
            }
        }

        void FireLaserShot(Vector3 muzzle, Vector3 targetPoint, PlayerController pc)
        {
            Vector3 dir = (targetPoint - muzzle).normalized;
            float maxShotDist = Vector3.Distance(muzzle, targetPoint) + 0.25f;
            Vector3 finalEnd = muzzle + dir * maxShotDist;

            bool blockedByObstacle = false;
            if (Physics.Raycast(muzzle, dir, out RaycastHit hit, maxShotDist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform.root != transform.root)
                {
                    finalEnd = hit.point;
                    if (!hit.collider.CompareTag("Player") && (pc == null || hit.collider.transform.root != pc.transform.root))
                    {
                        blockedByObstacle = true;
                    }
                }
            }

            // Hiển thị chùm tia bắn năng lượng cao to sáng
            laserLine.enabled = true;
            laserLine.startWidth = 0.014f;
            laserLine.endWidth = 0.009f;
            laserLine.startColor = new Color(1f, 0.9f, 0.35f, 1f);
            laserLine.endColor = new Color(1f, 0.1f, 0.1f, 1f);
            laserLine.SetPosition(0, muzzle);
            laserLine.SetPosition(1, finalEnd);
            fireShowTimer = fireDisplayDuration;

            // Kiểm tra trúng Player
            if (!blockedByObstacle && pc != null)
            {
                if (pc.isDodging)
                {
                    Debug.Log("💨 [DRONE LASER MISS] Người chơi đã né được tia Laser của Drone!");
                }
                else
                {
                    Vector3 pCenter = pc.transform.position + Vector3.up * (pc.playerHeight * 0.5f);
                    float distToBeam = DistancePointToSegment(pCenter, muzzle, finalEnd);
                    if (distToBeam <= 0.055f)
                    {
                        PlayerHealth ph = pc.GetComponent<PlayerHealth>();
                        if (ph != null)
                        {
                            ph.TakeDamage(laserDamage);
                            Debug.Log($"🔫 [DRONE LASER HIT] Drone bắn trúng người chơi (-{laserDamage} HP)!");
                        }
                    }
                }
            }

            currentState = DroneState.Cooldown;
            cooldownTimer = attackCooldown;
            UpdateVisualState(new Color(1f, 0.55f, 0.1f));
        }

        void TickCooldown(bool canSeePlayer, PlayerController pc)
        {
            if (pc != null) RotateToward(pc.transform.position);
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                if (canSeePlayer)
                {
                    EnterAimingState();
                }
                else
                {
                    currentState = DroneState.Alert;
                    alertTimer = 2.5f;
                }
            }
        }

        bool CanSeePlayer(PlayerController pc)
        {
            if (pc == null) return false;
            Vector3 eye = muzzleTransform != null ? muzzleTransform.position : transform.position;
            Vector3 target = pc.transform.position + Vector3.up * (pc.playerHeight * 0.5f);
            Vector3 diff = target - eye;
            float dist = diff.magnitude;

            if (dist > viewRadius) return false;

            Vector3 flatFwd = transform.forward;
            flatFwd.y = 0f;
            Vector3 flatDiff = diff;
            flatDiff.y = 0f;
            if (flatFwd.sqrMagnitude > 0.0001f && flatDiff.sqrMagnitude > 0.0001f)
            {
                if (Vector3.Angle(flatFwd.normalized, flatDiff.normalized) > viewAngle * 0.5f)
                {
                    return false;
                }
            }

            Vector3 dir = diff / Mathf.Max(0.0001f, dist);
            if (Physics.Raycast(eye, dir, out RaycastHit hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && hit.collider.transform.root != transform.root && hit.collider.transform.root != pc.transform.root && !hit.collider.CompareTag("Player"))
                {
                    return false;
                }
            }
            return true;
        }

        void MoveHorizontalToward(Vector3 destination, float speed)
        {
            Vector3 diff = destination - transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude < 0.0004f) return;

            Vector3 dir = diff.normalized;
            transform.position += dir * (speed * Time.deltaTime);
            RotateToward(destination);
        }

        void RotateToward(Vector3 target)
        {
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);
            Debug.Log($"💥 [DRONE HIT] {droneName} nhận {amount} sát thương -> Còn {currentHealth}/{maxHealth} HP");

            if (PlayerController.Instance != null)
            {
                NotifySecurityAlert(PlayerController.Instance.transform.position);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        void Die()
        {
            currentState = DroneState.Destroyed;
            if (laserLine != null) laserLine.enabled = false;
            if (scannerLight != null) scannerLight.enabled = false;

            if (dropItemPrefab != null)
            {
                Vector3 dropPos = new Vector3(transform.position.x, baseHoverY - 0.04f, transform.position.z);
                Instantiate(dropItemPrefab, dropPos, Quaternion.identity);
            }

            Debug.Log($"💣 [DRONE DESTROYED] {droneName} đã bị tiêu diệt!");
            Destroy(gameObject, 0.25f);
        }

        void UpdateVisualState(Color c)
        {
            if (scannerLight != null) scannerLight.color = c;
            if (eyeRenderer != null && eyeRenderer.material != null)
            {
                eyeRenderer.material.color = c;
                if (eyeRenderer.material.HasProperty("_BaseColor"))
                    eyeRenderer.material.SetColor("_BaseColor", c);
                if (eyeRenderer.material.HasProperty("_EmissionColor"))
                    eyeRenderer.material.SetColor("_EmissionColor", c * 2.5f);
            }
        }

        static float DistancePointToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < 0.000001f) return Vector3.Distance(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / sqrLen);
            return Vector3.Distance(p, a + ab * t);
        }

        void OnGUI()
        {
            if (!IsAlive || Event.current.type != EventType.Repaint) return;
            if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen) return;
            if (mainCamera == null) return;

            Vector3 worldPos = transform.position + Vector3.up * 0.075f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f) return;

            if (s_hpLabelStyle == null)
            {
                s_hpLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 11
                };
                s_hpLabelStyle.normal.textColor = Color.white;
            }

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y;
            float w = 88f;
            float h = 10f;
            Rect bgRect = new Rect(guiX - w * 0.5f, guiY - h * 0.5f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(bgRect.x - 1f, bgRect.y - 1f, w + 2f, h + 2f), Texture2D.whiteTexture);

            GUI.color = new Color(0.25f, 0.28f, 0.32f, 0.95f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            GUI.color = currentState == DroneState.Aiming ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(0.2f, 0.78f, 1f, 1f);
            GUI.DrawTexture(new Rect(bgRect.x, bgRect.y, w * displayedHpRatio, h), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(guiX - 60f, guiY - 22f, 120f, 16f), droneName, s_hpLabelStyle);
        }
    }
}
