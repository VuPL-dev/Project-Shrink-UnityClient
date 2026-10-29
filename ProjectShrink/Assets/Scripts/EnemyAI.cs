using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShrink.AI;

/// <summary>
/// Trí tuệ Nhân tạo Enemy (Nightmare Beetle / Mutant Rat / Quái Kiến) theo chuẩn FA26SE278:
/// - FSM 4 trạng thái: Patrol (Tuần tra), Alert (Cảnh giác/Điều tra âm thanh), Chase (Rượt đuổi), Attack (Tấn công), Dead (Chết).
/// - Tích hợp NavMeshAgent thông minh (có fallback tự động khi chưa bake NavMesh).
/// - Cảm biến Thị giác (Vision Sensor): Nón nhìn FOV + Raycast chặn nhìn xuyên tường.
/// - Cảm biến Thính giác (Hearing Sensor): Lắng nghe SoundEmitter khi Player chạy/ném đồ.
/// - Combat 2 chiều: Đánh Player (PlayerHealth), nhận sát thương (TakeDamage), chết rơi đồ chế tạo/HealthSyringe.
/// - Thanh máu Visual trên đầu hiển thị Realtime + Hiệu ứng UI sát thương nổi (Combat Text).
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyAI : MonoBehaviour
{
    public enum Species { Beetle = 0, Ant = 1 }
    public enum EnemySpecies { Beetle = 0, Ant = 1 }
    public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Dead }

    public static readonly List<EnemyAI> ActiveEnemies = new List<EnemyAI>();

    [Header("0. Thông Tin Quái Vật")]
    public Species species = Species.Ant;
    public string enemyName = "Quái Kiến";

    [Header("1. Chỉ Số Chiến Đấu (Combat Stats)")]
    public float maxHealth = 50f;
    public float currentHealth = 50f;
    [Tooltip("Tốc độ bò tuần tra")]
    public float patrolSpeed = 0.15f;
    [Tooltip("Tốc độ bò rượt đuổi người chơi")]
    public float chaseSpeed = 0.32f;
    public float rotationSpeed = 5f;
    public float attackDamage = 15f;
    [Tooltip("Khoảng cách chạm sát vào người chơi để cắn (mét)")]
    public float attackRange = 0.16f;
    public float attackCooldown = 1.5f;

    // Tương thích ngược với các Editor Script cũ
    public float moveSpeed { get => patrolSpeed; set => patrolSpeed = value; }
    public float detectionRange { get => viewRadius; set => viewRadius = value; }
    public bool useTerritoryPatrol = true;
    public Vector3 territoryCenter = Vector3.zero;
    public float territoryRadius = 0.46f;
    public float minStopDistance = 0.085f;
    public bool showHealthBar = true;
    public float healthBarVisibleDistance = 0.65f;
    public float healthBarHeightOffset = 0.052f;
    public float minEnemySeparation = 0.085f;

    [Header("2. Cảm Biến Thị Giác (Vision Sensor)")]
    [Tooltip("Bán kính tầm nhìn (mét) - Quét 1/4 căn phòng (~0.7m)")]
    public float viewRadius = 0.70f;
    [Range(0, 360)]
    [Tooltip("Góc quét hình nón FOV (độ) - Góc nhìn tập trung phía trước")]
    public float viewAngle = 65f;
    [Tooltip("Layer vật cản che khuất tầm nhìn (Tường, Cửa)")]
    public LayerMask obstacleMask;

    [Header("3. Cảm Biến Thính Giác (Hearing Sensor)")]
    [Tooltip("Bán kính tai nghe tiếng chạy")]
    public float hearingRadius = 1.1f;
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

    // Hiệu ứng UI sát thương nổi (Combat Text Floating)
    private string lastDamageText = "";
    private float lastDamageTextTime = -999f;
    private Color lastDamageColor = Color.red;

    public EnemyState CurrentState => currentState;

    /// <summary>
    /// Đồng bộ chỉ số máu, sát thương, tốc độ quái từ Web Server & Backend
    /// </summary>
    public void ApplyServerConfig()
    {
        string lookupId = (species == Species.Ant || name.ToLower().Contains("ant")) ? "ant" : "beetle";
        if (InventoryManager.ServerEnemyCache != null)
        {
            if (InventoryManager.ServerEnemyCache.TryGetValue(lookupId, out var info) ||
                InventoryManager.ServerEnemyCache.TryGetValue(name.ToLower(), out info))
            {
                if (info.hp > 0f) { maxHealth = info.hp; currentHealth = Mathf.Min(currentHealth, maxHealth); }
                if (info.damage > 0f) attackDamage = info.damage;
                if (info.speed > 0f) { patrolSpeed = info.speed; chaseSpeed = info.speed * 2f; }
                if (info.attackRange > 0f) attackRange = info.attackRange;
                if (info.detectionRange > 0f) viewRadius = info.detectionRange;
            }
        }
    }

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

    void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
            ActiveEnemies.Add(this);

        SoundEmitter.OnSoundTypeEmitted += HandleSoundHeard;
    }

    void OnDisable()
    {
        ActiveEnemies.Remove(this);
        SoundEmitter.OnSoundTypeEmitted -= HandleSoundHeard;
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
        ApplyServerConfig();

        if (dropItemPrefab == null)
        {
            dropItemPrefab = Resources.Load<GameObject>("HealthSyringe");
        }

        FixMissingMaterials();
        currentPatrolTarget = spawnPosition;
        ChangeState(EnemyState.Patrol);
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        EnsureTargetPlayer();

        // 1. Quét Cảm biến Thị giác FOV liên tục
        if (currentState != EnemyState.Chase && currentState != EnemyState.Attack)
        {
            if (targetPlayer != null && CanSeeTarget(targetPlayer))
            {
                Debug.Log($"👁️ [{enemyName}] Phát hiện Người chơi qua Cảm biến Thị giác FOV!");
                ChangeState(EnemyState.Chase);
            }
        }

        // 2. Chạy Finite State Machine (FSM)
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;
            case EnemyState.Alert:
                UpdateAlertState();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
        }
    }

    #region FSM State Updates
    void UpdateIdleState()
    {
        waypointTimer += Time.deltaTime;
        if (waypointTimer >= waitAtWaypointTime)
        {
            waypointTimer = 0f;
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdatePatrolState()
    {
        Vector3 targetDest = GetCurrentPatrolDestination();
        MoveTowards(targetDest, patrolSpeed);

        float dist = Vector3.Distance(transform.position, targetDest);
        if (dist <= waypointTolerance)
        {
            AdvanceToNextWaypoint();
            ChangeState(EnemyState.Idle);
        }
    }

    void UpdateAlertState()
    {
        MoveTowards(soundInvestigationTarget, patrolSpeed * 1.2f);

        alertTimer += Time.deltaTime;
        float distToSound = Vector3.Distance(transform.position, soundInvestigationTarget);

        if (distToSound <= waypointTolerance || alertTimer >= alertInvestigateDuration)
        {
            Debug.Log($"👀 [{enemyName}] Điều tra xong vị trí âm thanh, không thấy gì. Quay về tuần tra.");
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdateChaseState()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, targetPlayer.position);

        // Mất dấu nếu người chơi chạy quá xa (gấp 1.5 lần tầm nhìn)
        if (distToPlayer > viewRadius * 1.5f)
        {
            Debug.Log($"❓ [{enemyName}] Mất dấu người chơi! Chuyển sang Cảnh giác.");
            soundInvestigationTarget = targetPlayer.position;
            ChangeState(EnemyState.Alert);
            return;
        }

        MoveTowards(targetPlayer.position, chaseSpeed);

        // Đủ cự ly tấn công
        if (distToPlayer <= attackRange)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    void UpdateAttackState()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        RotateTowards(targetPlayer.position);

        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        if (dist > attackRange * 1.35f && !isAttacking)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
        {
            StartCoroutine(PerformAttackRoutine());
        }
    }
    #endregion

    #region Movement Helpers
    void MoveTowards(Vector3 targetPos, float speed)
    {
        targetPos.y = transform.position.y;
        RotateTowards(targetPos);

        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.speed = speed;
            navAgent.SetDestination(targetPos);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        }
    }

    void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    void StopMoving()
    {
        if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
        }
    }

    Vector3 GetCurrentPatrolDestination()
    {
        if (waypoints != null && waypoints.Length > 0)
        {
            if (waypoints[currentWaypointIndex] != null)
                return waypoints[currentWaypointIndex].position;
        }

        return currentPatrolTarget;
    }

    void AdvanceToNextWaypoint()
    {
        if (waypoints != null && waypoints.Length > 1)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            patrolDirection = -patrolDirection;
            float targetZ = spawnPosition.z + (patrolDistance * patrolDirection);
            currentPatrolTarget = new Vector3(spawnPosition.x, spawnPosition.y, targetZ);
        }
    }
    #endregion

    #region State Transition & Animation
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
            if (dist <= attackRange + 0.04f)
            {
                PlayerController pc = targetPlayer.GetComponent<PlayerController>();
                PlayerHealth playerHealth = targetPlayer.GetComponent<PlayerHealth>() ?? targetPlayer.GetComponentInParent<PlayerHealth>();

                // Kiểm tra né đòn Miss (Dodge / Evade)
                if (pc != null && pc.isDodging)
                {
                    Debug.Log($"💨 [ENEMY MISS] {enemyName} cắn hụt vì Người chơi đã NÉ ĐÒN!");
                    ShowDamagePopup("💨 NÉ ĐÒN!", Color.cyan);
                }
                else if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                    Debug.Log($"💥 [ENEMY ATTACK] {enemyName} cắn trúng người chơi! -{attackDamage} HP");
                    ShowDamagePopup($"💥 CẮN! -{Mathf.CeilToInt(attackDamage)} HP", Color.red);
                }
            }
            else
            {
                Debug.Log($"💨 [ENEMY MISS] {enemyName} cắn hụt vì người chơi đã kịp né lùi ra xa! (Cự ly: {dist:F2}m)");
                ShowDamagePopup("💨 CẮN HỤT!", Color.gray);
            }
        }

        yield return new WaitForSeconds(0.35f);
        isAttacking = false;

        if (currentState != EnemyState.Dead && currentState == EnemyState.Attack)
        {
            PlayAnim("idle1", 0.15f);
        }
    }

    public void ShowDamagePopup(string text, Color color)
    {
        lastDamageText = text;
        lastDamageTextTime = Time.time;
        lastDamageColor = color;
    }

    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"⚔️ [PLAYER HIT] Đánh trúng {enemyName}! Sát thương: {damage} | Máu quái: {Mathf.Max(0, currentHealth)}/{maxHealth}");
        ShowDamagePopup($"-{Mathf.CeilToInt(damage)} HP", Color.yellow);

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

        Debug.Log($"💀 [ENEMY DEAD] {enemyName} đã bị tiêu diệt!");

        Vector3 dropPos = transform.position + Vector3.up * dropYOffset;
        if (dropItemPrefab != null)
        {
            GameObject dropped = Instantiate(dropItemPrefab, dropPos, Quaternion.identity);
            dropped.name = "Health_Syringe_Drop";
            Debug.Log("🎁 [DROP ITEM] Rơi ra HealthSyringe tại điểm quái ngã gục!");
        }
        else
        {
            string dropId = (species == Species.Ant || name.ToLower().Contains("ant")) ? "long_bone" : "spider_silk";
            ItemPickup.CreateWorldDropById(dropId, dropPos);
            Debug.Log($"🎁 [DROP ITEM] Rơi ra nguyên liệu: {dropId} tại điểm quái ngã gục!");
        }

        Destroy(gameObject, 4.5f);
    }
    #endregion

    #region GUI Health Bar & Combat Text
    void OnGUI()
    {
        if (currentState == EnemyState.Dead) return;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Tọa độ trên lưng/đầu quái vật (0.058m trong tỉ lệ thu nhỏ)
        Vector3 headPos = transform.position + Vector3.up * 0.058f;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(headPos);

        // Nếu nằm phía sau camera thì không vẽ
        if (screenPos.z <= 0) return;

        // Kích thước to rõ, dễ nhìn
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
        displayedHealthRatio = Mathf.Lerp(displayedHealthRatio, Mathf.Clamp01(currentHealth / maxHealth), 10f * Time.deltaTime);
        float barFill = displayedHealthRatio * width;
        Color hpColor = (displayedHealthRatio > 0.4f) ? new Color(0.95f, 0.25f, 0.2f, 1f) : new Color(1f, 0.1f, 0.1f, 1f);
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
        GUI.Label(new Rect(x, y - 18, width, 16), $"🐜 {enemyName}: {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", labelStyle);
        // Chữ trắng nổi bật
        labelStyle.normal.textColor = (currentHealth < 20f) ? Color.yellow : Color.white;
        GUI.Label(new Rect(x - 1, y - 19, width, 16), $"🐜 {enemyName}: {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", labelStyle);

        // 5. Hiệu ứng UI sát thương / tấn công nổi (Damage Popup)
        if (Time.time - lastDamageTextTime < 1.35f && !string.IsNullOrEmpty(lastDamageText))
        {
            float elapsed = Time.time - lastDamageTextTime;
            float floatY = y - 36f - (elapsed * 24f);
            GUIStyle combatStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            combatStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(x - 45f + 1, floatY + 1, width + 90f, 22f), lastDamageText, combatStyle);
            combatStyle.normal.textColor = lastDamageColor;
            GUI.Label(new Rect(x - 45f, floatY, width + 90f, 22f), lastDamageText, combatStyle);
        }

        GUI.color = Color.white;
    }
    #endregion

    #region Vision & Hearing Sensors
    public bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;

        Vector3 eyePos = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 0.04f;
        Vector3 targetPos = target.position + Vector3.up * 0.04f;

        float distToTarget = Vector3.Distance(eyePos, targetPos);
        if (distToTarget > viewRadius) return false;

        Vector3 dirToTarget = (targetPos - eyePos);
        float angleToTarget = Vector3.Angle(transform.forward, dirToTarget.normalized);
        if (angleToTarget > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eyePos, dirToTarget.normalized, out RaycastHit hit, distToTarget, obstacleMask))
        {
            if (hit.transform != target && !hit.transform.IsChildOf(target))
            {
                return false;
            }
        }

        return true;
    }

    private void HandleSoundHeard(Vector3 soundPos, SoundEmitter.SoundType soundType, float loudness)
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Chase || currentState == EnemyState.Attack)
            return;

        float dist = Vector3.Distance(transform.position, soundPos);
        float effectiveRadius = hearingRadius * loudness;

        if (dist <= effectiveRadius)
        {
            Debug.Log($"👂 [{enemyName}] Nghe thấy âm thanh {soundType} tại khoảng cách {dist:F2}m! Chuyển sang Cảnh giác điều tra.");
            soundInvestigationTarget = soundPos;
            ChangeState(EnemyState.Alert);
        }
    }

    public void InvestigateSound(Vector3 soundPos)
    {
        if (currentState == EnemyState.Dead) return;
        soundInvestigationTarget = soundPos;
        ChangeState(EnemyState.Alert);
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
