using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ProjectShrink.AI;

/// <summary>
/// Trí tuệ Nhân tạo Enemy (Nightmare Beetle / Mutant Rat) theo chuẩn FA26SE278:
/// - FSM 4 trạng thái: Patrol (Tuần tra), Alert (Cảnh giác/Điều tra âm thanh), Chase (Rượt đuổi), Attack (Tấn công), Dead (Chết).
/// - Tích hợp NavMeshAgent thông minh (có fallback tự động khi chưa bake NavMesh).
/// - Cảm biến Thị giác (Vision Sensor): Nón nhìn FOV + Raycast chặn nhìn xuyên tường.
/// - Cảm biến Thính giác (Hearing Sensor): Lắng nghe SoundEmitter khi Player chạy/ném đồ.
/// - Combat 2 chiều: Đánh Player (PlayerHealth), nhận sát thương (TakeDamage), chết rơi HealthSyringe.
/// - Thanh máu Visual trên đầu hiển thị Realtime.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Dead }

    [Header("1. Chỉ Số Chiến Đấu (Combat Stats)")]
    public float maxHealth = 50f;
    public float currentHealth = 50f;
    [Tooltip("Tốc độ bò tuần tra")]
    public float patrolSpeed = 0.18f;
    [Tooltip("Tốc độ bò rượt đuổi người chơi")]
    public float chaseSpeed = 0.45f;
    public float rotationSpeed = 6f;
    public float attackDamage = 15f;
    [Tooltip("Khoảng cách chạm sát vào người chơi để cắn (mét)")]
    public float attackRange = 0.22f;
    public float attackCooldown = 1.4f;

    // Tương thích ngược với các Editor Script cũ
    public float moveSpeed { get => patrolSpeed; set => patrolSpeed = value; }
    public float detectionRange { get => viewRadius; set => viewRadius = value; }

    [Header("2. Cảm Biến Thị Giác (Vision Sensor)")]
    [Tooltip("Bán kính tầm nhìn (mét) - Quét 1/4 căn phòng (~0.7m)")]
    public float viewRadius = 2.2f;
    [Range(0, 360)]
    [Tooltip("Góc quét hình nón FOV (độ) - Góc nhìn tập trung phía trước")]
    public float viewAngle = 85f;
    [Tooltip("Layer vật cản che khuất tầm nhìn (Tường, Cửa)")]
    public LayerMask obstacleMask;

    [Header("3. Cảm Biến Thính Giác (Hearing Sensor)")]
    [Tooltip("Bán kính tai nghe tiếng chạy")]
    public float hearingRadius = 2.5f;
    [Tooltip("Thời gian cảnh giác điều tra âm thanh (giây)")]
    public float alertInvestigateDuration = 3.0f;

    [Header("4. Tuần Tra (Patrol)")]
    public Transform[] waypoints;
    [Tooltip("Cự ly chấp nhận đã đến điểm tuần tra")]
    public float waypointTolerance = 0.12f;
    [Tooltip("Thời gian dừng nghỉ quan sát tại mỗi điểm (giây)")]
    public float waitAtWaypointTime = 1.8f;
    [Tooltip("Khoảng cách bò tuần tra tới lui nếu không gán Waypoints")]
    public float patrolDistance = 0.60f;

    [Header("5. Rơi Đồ Khi Chết (Drop Item)")]
    public GameObject dropItemPrefab;
    public float dropYOffset = 0.04f;

    [Header("6. Tham Chiếu")]
    public Animator animator;
    public Transform targetPlayer;
    public Transform eyeTransform;

    [Header("7. Trạng Thái Hiện Tại (Read Only)")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    private NavMeshAgent navAgent;
    private Collider enemyCollider;
    private float lastAttackTime = -999f;
    private bool isAttacking = false;
    private int currentWaypointIndex = 0;
    private float waypointTimer = 0f;
    private float alertTimer = 0f;
    private Vector3 soundInvestigationTarget;
    private Vector3 spawnPosition;
    private Vector3 currentPatrolTarget;
    private int patrolDirection = 1;
    private Camera mainCamera;
    private float displayedHealthRatio = 1f;

    public EnemyState CurrentState => currentState;

    void Awake()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        enemyCollider = GetComponent<Collider>();
        navAgent = GetComponent<NavMeshAgent>();

        if (eyeTransform == null)
            eyeTransform = transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        mainCamera = Camera.main;
    }

    
    public void EnsureTargetPlayer()
    {
        if (targetPlayer == null || !targetPlayer.gameObject.activeInHierarchy)
        {
            GameObject p2 = GameObject.Find("player_v2");
            if (p2 != null && p2.activeInHierarchy)
            {
                targetPlayer = p2.transform;
                return;
            }

            GameObject pTag = GameObject.FindWithTag("Player");
            if (pTag != null && pTag.activeInHierarchy)
            {
                targetPlayer = pTag.transform;
                return;
            }

            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (allPlayers.Length > 0)
            {
                targetPlayer = allPlayers[0].transform;
            }
        }
    }

    void Start()
    {
        EnsureTargetPlayer();
        if (viewRadius < 1.5f) viewRadius = 2.2f;
        if (viewAngle < 80f) viewAngle = 85f;
        if (hearingRadius < 2.0f) hearingRadius = 2.5f;
        if (chaseSpeed < 0.40f) chaseSpeed = 0.45f;
        if (patrolSpeed < 0.16f) patrolSpeed = 0.18f;
        if (attackRange < 0.20f) attackRange = 0.22f;

        // Tự động tìm nhân vật trong Scene nếu chưa gán
        if (targetPlayer == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                targetPlayer = player.transform;
        }

        // Tự động tìm Prefab HealthSyringe nếu chưa gán
        if (dropItemPrefab == null)
        {
            dropItemPrefab = Resources.Load<GameObject>("HealthSyringe");
            if (dropItemPrefab == null)
            {
                // Thử tìm trong Prefabs
                GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var g in all)
                {
                    if (g.name.Equals("HealthSyringe") && g.scene.rootCount == 0)
                    {
                        dropItemPrefab = g;
                        break;
                    }
                }
            }
        }

        FixMissingMaterials();

        // Khởi tạo điểm tuần tra đầu tiên cách vị trí đứng patrolDistance
        float initialZ = Mathf.Clamp(spawnPosition.z - patrolDistance, -0.5f, 1.6f);
        currentPatrolTarget = new Vector3(spawnPosition.x, spawnPosition.y, initialZ);

        ChangeState(EnemyState.Idle);
    }

    void OnEnable()
    {
        // Đăng ký nhận sóng âm thanh toàn cục
        SoundEmitter.OnSoundEmitted += HandleSoundHeard;
    }

    void OnDisable()
    {
        SoundEmitter.OnSoundEmitted -= HandleSoundHeard;
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;
        EnsureTargetPlayer();

        if (targetPlayer == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) targetPlayer = player.transform;
            return;
        }

        // Cập nhật thanh máu mượt
        float targetRatio = Mathf.Clamp01(currentHealth / maxHealth);
        displayedHealthRatio = Mathf.Lerp(displayedHealthRatio, targetRatio, 10f * Time.deltaTime);

        // Kiểm tra tầm nhìn liên tục
        bool playerSeen = CanSeeTarget(targetPlayer);

        // Xử lý chuyển đổi FSM
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState(playerSeen);
                break;

            case EnemyState.Patrol:
                UpdatePatrolState(playerSeen);
                break;

            case EnemyState.Alert:
                UpdateAlertState(playerSeen);
                break;

            case EnemyState.Chase:
                UpdateChaseState(playerSeen);
                break;

            case EnemyState.Attack:
                UpdateAttackState();
                break;
        }
    }

    #region FSM State Updates
    void UpdateIdleState(bool playerSeen)
    {
        if (playerSeen)
        {
            Debug.Log($"👁️ [{name}] Nhìn thấy người chơi! Bắt đầu đuổi theo (Chase)!");
            ChangeState(EnemyState.Chase);
            return;
        }

        waypointTimer += Time.deltaTime;
        if (waypointTimer >= waitAtWaypointTime)
        {
            waypointTimer = 0f;
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdatePatrolState(bool playerSeen)
    {
        if (playerSeen)
        {
            Debug.Log($"👁️ [{name}] Nhìn thấy người chơi khi đang tuần tra! Bắt đầu đuổi theo (Chase)!");
            ChangeState(EnemyState.Chase);
            return;
        }

        Vector3 destination;
        if (waypoints != null && waypoints.Length > 0 && waypoints[currentWaypointIndex] != null)
        {
            destination = waypoints[currentWaypointIndex].position;
        }
        else
        {
            destination = currentPatrolTarget;
        }

        MoveTowards(destination, patrolSpeed);

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                      new Vector3(destination.x, 0, destination.z));

        if (dist <= waypointTolerance)
        {
            if (waypoints != null && waypoints.Length > 0)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
            else
            {
                // Đảo chiều tuần tra tới lui dọc hành lang phòng
                patrolDirection = -patrolDirection;
                float targetZ = Mathf.Clamp(spawnPosition.z + patrolDirection * patrolDistance, -0.5f, 1.6f);
                currentPatrolTarget = new Vector3(spawnPosition.x, spawnPosition.y, targetZ);
            }
            ChangeState(EnemyState.Idle);
        }
    }

    void UpdateAlertState(bool playerSeen)
    {
        if (playerSeen)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        alertTimer += Time.deltaTime;

        // Di chuyển điều tra vị trí phát ra âm thanh
        MoveTowards(soundInvestigationTarget, patrolSpeed * 1.2f);

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                      new Vector3(soundInvestigationTarget.x, 0, soundInvestigationTarget.z));

        // Khi đã đến gần vị trí nghe âm thanh -> Xoay vòng tìm kiếm
        if (dist <= waypointTolerance * 1.5f)
        {
            transform.Rotate(Vector3.up, 50f * Time.deltaTime);
        }

        if (alertTimer >= alertInvestigateDuration)
        {
            alertTimer = 0f;
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdateChaseState(bool playerSeen)
    {
        if (targetPlayer == null) return;

        float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        if (distToPlayer <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        // Nếu mất dấu tầm nhìn quá xa (> viewRadius * 1.4)
        if (!playerSeen && distToPlayer > viewRadius * 1.4f)
        {
            soundInvestigationTarget = targetPlayer.position;
            ChangeState(EnemyState.Alert);
            return;
        }

        // Rượt đuổi theo người chơi
        MoveTowards(targetPlayer.position, chaseSpeed);
    }

    void UpdateAttackState()
    {
        if (targetPlayer == null) return;

        float dist = Vector3.Distance(transform.position, targetPlayer.position);

        // Nếu người chơi lùi ra ngoài tầm đánh
        if (dist > attackRange * 1.35f && !isAttacking)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // Luôn xoay mặt nhìn theo người chơi
        Vector3 lookDir = (targetPlayer.position - transform.position);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), rotationSpeed * 2f * Time.deltaTime);
        }

        // Đánh khi hết cooldown
        if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
        {
            StartCoroutine(PerformAttackRoutine());
        }
    }
    #endregion

    #region Navigation & Movement
    void MoveTowards(Vector3 destination, float speed)
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.speed = speed;
            navAgent.SetDestination(destination);
        }
        else
        {
            // Fallback mượt mà khi scene chưa bake NavMesh
            Vector3 dir = (destination - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                transform.position += dir.normalized * speed * Time.deltaTime;
            }
        }
    }

    void StopMoving()
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }
    }
    #endregion

    #region Sensors (Vision & Hearing)
    public bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;

        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 0.1f;
        Vector3 targetPos = target.position + Vector3.up * 0.1f;
        Vector3 dirToTarget = (targetPos - eyePos);
        float distToTarget = dirToTarget.magnitude;

        // 1. Kiểm tra cự ly
        if (distToTarget > viewRadius) return false;

        // 2. Kiểm tra góc FOV
        Vector3 forward = transform.forward;
        float angle = Vector3.Angle(forward, new Vector3(dirToTarget.x, 0, dirToTarget.z));
        if (angle > viewAngle * 0.5f) return false;

        // 3. Kiểm tra vật cản che khuất bằng Raycast
        if (Physics.Raycast(eyePos, dirToTarget.normalized, out RaycastHit hit, distToTarget, obstacleMask))
        {
            if (hit.transform.root != target.root)
            {
                return false; // Bị tường hoặc vật cản che chắn
            }
        }

        return true;
    }

    void HandleSoundHeard(Vector3 soundOrigin, float soundRadius, GameObject source)
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Chase || currentState == EnemyState.Attack)
            return;

        // Bỏ qua nếu chính mình phát ra tiếng
        if (source != null && source.transform.root == transform.root)
            return;

        float distToSound = Vector3.Distance(transform.position, soundOrigin);
        if (distToSound <= (hearingRadius + soundRadius))
        {
            soundInvestigationTarget = soundOrigin;
            alertTimer = 0f;
            ChangeState(EnemyState.Alert);
            Debug.Log($"👂 [{name}] Cảm biến thính giác nghe thấy tiếng động tại {soundOrigin}! Chuyển sang Cảnh giác (Alert).");
        }
    }
    #endregion

    #region State Transition & Animations
    void ChangeState(EnemyState newState)
    {
        if (currentState == EnemyState.Dead) return;

        currentState = newState;

        switch (currentState)
        {
            case EnemyState.Idle:
                StopMoving();
                PlayAnim("idle1", 0.2f);
                break;

            case EnemyState.Patrol:
                PlayAnim("walk", 0.15f);
                break;

            case EnemyState.Alert:
                PlayAnim("walk", 0.15f);
                break;

            case EnemyState.Chase:
                PlayAnim("walk", 0.1f);
                break;

            case EnemyState.Attack:
                StopMoving();
                break;

            case EnemyState.Dead:
                StopMoving();
                PlayAnim("death", 0.1f);
                break;
        }
    }

    void PlayAnim(string stateName, float crossFade = 0.1f)
    {
        if (animator == null) return;
        animator.CrossFade(stateName, crossFade);
    }
    #endregion

    #region Combat (TakeDamage, Attack, Die)
    IEnumerator PerformAttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        PlayAnim("atack1", 0.08f);

        // Chờ đúng thời điểm vung càng cắn (0.45s)
        yield return new WaitForSeconds(0.45f);

        if (currentState != EnemyState.Dead && targetPlayer != null)
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.position);
            // Chỉ gây sát thương khi thực sự chạm sát người chơi (cự ly chạm đầu kiến ~ 0.20m)
            if (dist <= attackRange + 0.04f)
            {
                PlayerHealth playerHealth = targetPlayer.GetComponent<PlayerHealth>();
                if (playerHealth == null) playerHealth = targetPlayer.GetComponentInParent<PlayerHealth>();

                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                    Debug.Log($"💥 [ENEMY ATTACK] {name} cắn trúng người chơi! -{attackDamage} HP");
                }
            }
            else
            {
                Debug.Log($"💨 [ENEMY MISS] {name} cắn hụt vì người chơi đã kịp né lùi ra xa! (Cự ly: {dist:F2}m)");
            }
        }

        yield return new WaitForSeconds(0.35f);
        isAttacking = false;

        if (currentState != EnemyState.Dead && currentState == EnemyState.Attack)
        {
            PlayAnim("idle1", 0.15f);
        }
    }

    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"⚔️ [PLAYER HIT] Đánh trúng {name}! Sát thương: {damage} | Máu quái: {Mathf.Max(0, currentHealth)}/{maxHealth}");

        // Khi bị đánh mà đang tuần tra hoặc cảnh giác -> Ngay lập tức rượt đuổi mục tiêu
        if (targetPlayer != null && (currentState == EnemyState.Patrol || currentState == EnemyState.Alert || currentState == EnemyState.Idle))
        {
            ChangeState(EnemyState.Chase);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            PlayAnim("gethit", 0.05f);
        }
    }

    void Die()
    {
        ChangeState(EnemyState.Dead);

        if (enemyCollider != null)
            enemyCollider.enabled = false;

        if (navAgent != null && navAgent.isActiveAndEnabled)
            navAgent.enabled = false;

        Debug.Log($"💀 [ENEMY DEAD] {name} đã bị tiêu diệt!");

        // Rơi ra vật phẩm hồi máu HealthSyringe
        if (dropItemPrefab != null)
        {
            Vector3 dropPos = transform.position + Vector3.up * dropYOffset;
            GameObject dropped = Instantiate(dropItemPrefab, dropPos, Quaternion.identity);
            dropped.name = "Health_Syringe_Drop";
            Debug.Log("🎁 [DROP ITEM] Rơi ra HealthSyringe tại điểm quái ngã gục!");
        }

        Destroy(gameObject, 4.5f);
    }
    #endregion

    #region GUI Health Bar (Floating HP Bar)
    void OnGUI()
    {
        if (currentState == EnemyState.Dead) return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Tọa độ trên lưng/đầu quái
        Vector3 headPos = transform.position + Vector3.up * 0.11f;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(headPos);

        // Nếu nằm phía sau camera thì không vẽ
        if (screenPos.z <= 0) return;

        // Kích thước to rõ, dễ nhìn từ xa
        float width = 110f;
        float height = 11f;
        float x = screenPos.x - width * 0.5f;
        float y = Screen.height - screenPos.y - height;

        // 1. Viền ngoài đen đậm có độ dày
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);

        // 2. Nền thanh tối
        GUI.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        // 3. Thanh máu màu đỏ tươi / cam rực rỡ
        float currentRatio = Mathf.Clamp01(displayedHealthRatio);
        float barFill = currentRatio * width;
        Color hpColor = (currentRatio > 0.4f) ? new Color(0.95f, 0.25f, 0.2f, 1f) : new Color(1f, 0.1f, 0.1f, 1f);
        GUI.color = hpColor;
        GUI.DrawTexture(new Rect(x, y, barFill, height), Texture2D.whiteTexture);

        // 4. Nhãn tên và chỉ số máu hiển thị sắc nét phía trên thanh
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 11
        };

        // Đổ bóng chữ đen
        labelStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(x, y - 18, width, 16), $"🐜 Quái Kiến: {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", labelStyle);
        // Chữ trắng nổi bật
        labelStyle.normal.textColor = (currentHealth < 20f) ? Color.yellow : Color.white;
        GUI.Label(new Rect(x - 1, y - 19, width, 16), $"🐜 Quái Kiến: {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", labelStyle);

        GUI.color = Color.white;
    }
    #endregion

    void FixMissingMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Material validMat = null;
        foreach (Renderer r in renderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.shader != null && !r.sharedMaterial.shader.name.Contains("Error"))
            {
                validMat = r.sharedMaterial;
                break;
            }
        }

        if (validMat != null)
        {
            foreach (Renderer r in renderers)
            {
                if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("Error"))
                {
                    r.sharedMaterial = validMat;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // 1. Vẽ bán kính thính giác (Màu tím)
        Gizmos.color = new Color(0.7f, 0.2f, 0.9f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        // 2. Vẽ tầm nhìn nón FOV (Màu vàng khi bình thường, Màu đỏ khi thấy Player)
        bool playerInSight = (targetPlayer != null && CanSeeTarget(targetPlayer));
        Gizmos.color = playerInSight ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 forward = transform.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle * 0.5f, 0) * forward;

        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

        // 3. Tầm tấn công (Màu đỏ tươi)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 4. Tuyến đường tuần tra (Màu xanh dương)
        Gizmos.color = Color.cyan;
        if (waypoints != null && waypoints.Length > 0)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawSphere(waypoints[i].position, 0.04f);
                    if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                        Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                }
            }
        }
        else
        {
            Vector3 pt1 = new Vector3(transform.position.x, transform.position.y, Mathf.Clamp(transform.position.z - patrolDistance, -0.5f, 1.6f));
            Vector3 pt2 = new Vector3(transform.position.x, transform.position.y, Mathf.Clamp(transform.position.z + patrolDistance, -0.5f, 1.6f));
            Gizmos.DrawLine(pt1, pt2);
            Gizmos.DrawWireSphere(pt1, 0.04f);
            Gizmos.DrawWireSphere(pt2, 0.04f);
        }
    }
}
