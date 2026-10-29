using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShrink.AI;

/// <summary>
/// Trí tuệ Nhân tạo Enemy (Nightmare Beetle / Mutant Rat / Quái Kiến) theo chuẩn FA26SE278:
/// - FSM hoàn chỉnh: Idle (Nghỉ quan sát), Patrol (Tuần tra theo Waypoints/Lãnh thổ), Alert (Cảnh giác điều tra âm thanh), Chase (Rượt đuổi), Attack (Tấn công cận chiến), Dead (Chết rơi đồ).
/// - Tuần tra Lãnh thổ thông minh (Territory Patrol AI): Tự động tạo lộ trình tuần tra khép kín qua các Waypoints trong phòng.
/// - Cảm biến Thị giác (Vision Sensor): Nón nhìn FOV 65° + Raycast kiểm tra vật cản.
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
    public float rotationSpeed = 6f;
    public float attackDamage = 15f;
    [Tooltip("Khoảng cách chạm sát vào người chơi để cắn (mét)")]
    public float attackRange = 0.16f;
    public float attackCooldown = 1.5f;

    // Tương thích ngược với các Editor Script cũ
    public float moveSpeed { get => patrolSpeed; set => patrolSpeed = value; }
    public float detectionRange { get => viewRadius; set => viewRadius = value; }
    public float minStopDistance = 0.085f;
        [Header("1.5. Cấu Hình Thanh Máu (Health Bar Settings)")]
    [Tooltip("Bật/tắt thanh máu nổi trên đầu quái")]
    public bool showHealthBar = true;
    [Tooltip("Độ cao của thanh máu trên đầu quái (mét) - Tăng giảm để thanh máu cao hơn")]
    public float healthBarHeightOffset = 0.075f;
    [Tooltip("Chiều rộng thanh máu trên đầu (pixel)")]
    public float healthBarWidth = 140f;
    [Tooltip("Chiều cao thanh máu trên đầu (pixel)")]
    public float healthBarHeight = 14f;
    [Tooltip("Bật thanh máu mục tiêu to rõ ở đỉnh màn hình")]
    public bool showTopScreenHealthBar = true;
    [Tooltip("Luôn hiển thị thanh máu đỉnh màn hình kể cả khi chưa chạm trán")]
    public bool alwaysShowTopBar = true;
    public float healthBarVisibleDistance = 20.0f;
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

    [Header("4. Tuần Tra & Lãnh Thổ (Patrol & Territory)")]
    [Tooltip("Bật chế độ tuần tra theo vùng lãnh thổ")]
    public bool useTerritoryPatrol = true;
    [Tooltip("Tâm vùng lãnh thổ tuần tra của quái")]
    public Vector3 territoryCenter = Vector3.zero;
    [Tooltip("Bán kính vùng lãnh thổ quái đi tuần")]
    public float territoryRadius = 0.46f;
    [Tooltip("Danh sách các điểm Waypoint tuần tra trong Scene")]
    public Transform[] waypoints;
    [Tooltip("Cự ly chấp nhận đã đến điểm tuần tra")]
    public float waypointTolerance = 0.08f;
    [Tooltip("Thời gian dừng nghỉ quan sát tại mỗi điểm (giây)")]
    public float waitAtWaypointTime = 1.4f;
    [Tooltip("Khoảng cách bò tuần tra tới lui nếu không gán Waypoints")]
    public float patrolDistance = 0.50f;

    [Header("5. Rơi Đồ Khi Chết (Drop Item)")]
    public GameObject dropItemPrefab;
    public float dropYOffset = 0.04f;

    [Header("6. Tham Chiếu")]
    public Animator animator;
    public Transform targetPlayer;
    public Transform eyeTransform;

    [Header("7. Trạng Thái Hiện Tại (Read Only)")]
    [SerializeField] private EnemyState currentState = EnemyState.Patrol;

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

    // Các điểm Waypoint tuần tra trong vùng lãnh thổ (Zero GC Allocation)
    private readonly Vector3[] territoryWaypoints = new Vector3[3];

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
        if (InventoryManager.ServerEnemyCache != null)
        {
            string searchType = (species == Species.Beetle) ? "beetle" : "ant";
            foreach (var kvp in InventoryManager.ServerEnemyCache)
            {
                var info = kvp.Value;
                if (info != null && !string.IsNullOrEmpty(info.enemyId) && info.enemyId.ToLower().Contains(searchType))
                {
                    if (info.hp > 0f) { maxHealth = info.hp; currentHealth = maxHealth; }
                    if (info.damage > 0f) attackDamage = info.damage;
                    if (info.speed > 0f) { patrolSpeed = info.speed; chaseSpeed = info.speed * 2f; }
                    if (info.attackRange > 0f) attackRange = info.attackRange;
                    if (info.detectionRange > 0f) viewRadius = info.detectionRange;
                    Debug.Log($"🌐 [{enemyName}] Đã áp dụng cấu hình Server: HP={maxHealth}, DMG={attackDamage}, Speed={patrolSpeed}");
                    break;
                }
            }
        }
    }

    void Awake()
    {
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        enemyCollider = GetComponent<Collider>();
        navAgent = GetComponent<NavMeshAgent>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (territoryCenter.sqrMagnitude < 0.001f)
            territoryCenter = transform.position;

        BuildTerritoryWaypoints();

        // Tự động tạo các điểm Waypoint trực quan trong Scene nếu chưa được gán
        if ((waypoints == null || waypoints.Length == 0) && useTerritoryPatrol)
        {
            CreateRuntimeTerritoryWaypoints();
        }

        ActiveEnemies.Add(this);
    }

    [ContextMenu("Tạo Lộ Trình Tuần Tra (Waypoints)")]
    public void GenerateWaypointsInEditor()
    {
        BuildTerritoryWaypoints();
        CreateRuntimeTerritoryWaypoints();
    }

    public void BuildTerritoryWaypoints()
    {
        if (territoryCenter.sqrMagnitude < 0.001f)
            territoryCenter = (territoryCenter.sqrMagnitude > 0.001f) ? territoryCenter : ((spawnPosition.sqrMagnitude > 0.001f) ? spawnPosition : transform.position);

        float y = transform.position.y;
        float r = Mathf.Min(territoryRadius * 0.72f, 0.32f);

        territoryWaypoints[0] = new Vector3(
            Mathf.Clamp(territoryCenter.x - r * 0.45f, 1.30f, 1.95f),
            y,
            Mathf.Clamp(territoryCenter.z - r, -0.65f, 1.82f)
        );
        territoryWaypoints[1] = new Vector3(
            Mathf.Clamp(territoryCenter.x + r * 0.50f, 1.30f, 1.95f),
            y,
            Mathf.Clamp(territoryCenter.z, -0.65f, 1.82f)
        );
        territoryWaypoints[2] = new Vector3(
            Mathf.Clamp(territoryCenter.x - r * 0.30f, 1.30f, 1.95f),
            y,
            Mathf.Clamp(territoryCenter.z + r, -0.65f, 1.82f)
        );
    }

    public void CreateRuntimeTerritoryWaypoints()
    {
        string routeName = $"{name}_PatrolRoute";
        GameObject parentGo = GameObject.Find(routeName);
        if (parentGo == null)
        {
            parentGo = new GameObject(routeName);
            if (transform.parent != null) parentGo.transform.SetParent(transform.parent);
        }

        Transform routeParent = parentGo.transform;
        waypoints = new Transform[territoryWaypoints.Length];
        for (int i = 0; i < territoryWaypoints.Length; i++)
        {
            Transform wp = routeParent.Find($"Waypoint_{i + 1}");
            if (wp == null)
            {
                GameObject wpGo = new GameObject($"Waypoint_{i + 1}");
                wp = wpGo.transform;
                wp.SetParent(routeParent);
                wp.position = territoryWaypoints[i];
            }
            waypoints[i] = wp;
        }
    }

    void OnDestroy()
    {
        ActiveEnemies.Remove(this);
    }

    void OnEnable()
    {
        SoundEmitter.OnSoundTypeEmitted += HandleSoundHeard;
    }

    void OnDisable()
    {
        SoundEmitter.OnSoundTypeEmitted -= HandleSoundHeard;
    }

    void EnsureTargetPlayer()
    {
        if (targetPlayer == null || !targetPlayer.gameObject.activeInHierarchy)
        {
            PlayerController localPC = PlayerController.Instance;
            if (localPC != null && localPC.gameObject.activeInHierarchy)
            {
                targetPlayer = localPC.transform;
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
        currentPatrolTarget = GetCurrentPatrolDestination();
        ChangeState(EnemyState.Patrol);
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        EnsureTargetPlayer();

        // 1. Quét Cảm biến Thị giác FOV liên tục khi chưa đuổi đánh
        if (currentState != EnemyState.Chase && currentState != EnemyState.Attack)
        {
            if (targetPlayer != null && CanSeeTarget(targetPlayer))
            {
                Debug.Log($"👁️ [{enemyName}] Phát hiện Người chơi qua Cảm biến Thị giác FOV! Bắt đầu rượt đuổi.");
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

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                      new Vector3(targetDest.x, 0, targetDest.z));
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
        float distPlayerToTerritory = Vector3.Distance(targetPlayer.position, territoryCenter);

        // Mất dấu nếu người chơi rút lui ra xa khỏi lãnh thổ hoặc vượt quá tầm nhìn
        bool playerEscaped = (useTerritoryPatrol && distPlayerToTerritory > territoryRadius * 1.45f && distToPlayer > viewRadius);
        bool lostSight = (!CanSeeTarget(targetPlayer) && distToPlayer > viewRadius * 1.4f);

        if (playerEscaped || lostSight)
        {
            Debug.Log($"👀 [{enemyName}] Người chơi đã rút lui ra xa ({distToPlayer:F2}m). Hủy truy đuổi, quay về tiếp tục tuần tra.");
            ChangeState(EnemyState.Patrol);
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
            int validIdx = currentWaypointIndex % waypoints.Length;
            if (waypoints[validIdx] != null)
                return waypoints[validIdx].position;
        }

        if (useTerritoryPatrol && territoryWaypoints != null && territoryWaypoints.Length > 0)
        {
            return territoryWaypoints[currentWaypointIndex % territoryWaypoints.Length];
        }

        return currentPatrolTarget;
    }

    void AdvanceToNextWaypoint()
    {
        if (waypoints != null && waypoints.Length > 1)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else if (useTerritoryPatrol && territoryWaypoints != null && territoryWaypoints.Length > 0)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % territoryWaypoints.Length;
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
    private IEnumerator PerformAttackRoutine()
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

    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"⚔️ [DAMAGE] {enemyName} nhận {damage} sát thương! Máu còn: {Mathf.Max(0, currentHealth)}/{maxHealth}");

        ShowDamagePopup($"-{Mathf.CeilToInt(damage)} HP", Color.yellow);

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            PlayAnim("gethit", 0.05f);

            // Bị đánh lén khi đang tuần tra -> Phản xạ quay lại rượt đuổi người chơi ngay
            if (targetPlayer != null && (currentState == EnemyState.Patrol || currentState == EnemyState.Idle || currentState == EnemyState.Alert))
            {
                ChangeState(EnemyState.Chase);
            }
        }
    }

    void Die()
    {
        ChangeState(EnemyState.Dead);
        Debug.Log($"💀 [{enemyName}] Đã bị tiêu diệt!");

        if (enemyCollider != null)
            enemyCollider.enabled = false;

        DropLoot();
        Destroy(gameObject, 3.5f);
    }

    void DropLoot()
    {
        Vector3 dropPos = transform.position + Vector3.up * dropYOffset;

        // 1. Rơi Ống Tiêm Sinh Lực (HealthSyringe)
        if (dropItemPrefab != null)
        {
            GameObject spawnedSyringe = Instantiate(dropItemPrefab, dropPos, Quaternion.identity);
            spawnedSyringe.name = "HealthSyringe";
            spawnedSyringe.transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);
        }

        // 2. Rơi Nguyên liệu chế tạo (Mạng Nhện / Xương Dài)
        string materialName = (species == Species.Beetle) ? "spider_silk" : "long_bone";
        string matPath = $"Prefabs/Items/Item_{materialName}";
        GameObject matPrefab = Resources.Load<GameObject>(matPath);
        if (matPrefab != null)
        {
            Vector3 matDropPos = dropPos + transform.right * 0.03f;
            GameObject spawnedMat = Instantiate(matPrefab, matDropPos, Quaternion.identity);
            spawnedMat.name = $"Item_{materialName}";
            spawnedMat.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            Debug.Log($"🎁 [DROP LOOT] {enemyName} rơi nguyên liệu: {materialName}");
        }
    }

    public void ShowDamagePopup(string text, Color color)
    {
        lastDamageText = text;
        lastDamageTextTime = Time.time;
        lastDamageColor = color;
    }
    #endregion

        #region GUI Health Bar & Combat Text
    void OnGUI()
    {
        if (currentState == EnemyState.Dead) return;

        // Cập nhật tỷ lệ máu mượt mà
        displayedHealthRatio = Mathf.Lerp(displayedHealthRatio, Mathf.Clamp01(currentHealth / maxHealth), 10f * Time.deltaTime);
        Color hpColor = (displayedHealthRatio > 0.4f) ? new Color(0.95f, 0.25f, 0.2f, 1f) : new Color(1f, 0.1f, 0.1f, 1f);

        // -------------------------------------------------------------
        // A. THANH MÁU MỤC TIÊU Ở ĐỈNH MÀN HÌNH (TOP-CENTER HUD BAR)
        // -------------------------------------------------------------
        bool inCombat = (currentState == EnemyState.Chase || currentState == EnemyState.Attack || (Time.time - lastDamageTextTime < 6.0f));
        if (showTopScreenHealthBar && (alwaysShowTopBar || inCombat))
        {
            float topW = 340f;
            float topH = 20f;
            float topX = (Screen.width - topW) * 0.5f;
            float topY = 28f;

            // 1. Viền ngoài đen đậm
            GUI.color = new Color(0f, 0f, 0f, 0.95f);
            GUI.DrawTexture(new Rect(topX - 3, topY - 3, topW + 6, topH + 6), Texture2D.whiteTexture);

            // 2. Nền tối
            GUI.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            GUI.DrawTexture(new Rect(topX, topY, topW, topH), Texture2D.whiteTexture);

            // 3. Thanh máu đổ đầy
            float topFill = displayedHealthRatio * topW;
            GUI.color = hpColor;
            GUI.DrawTexture(new Rect(topX, topY, topFill, topH), Texture2D.whiteTexture);

            // 4. Nhãn tên và chỉ số máu hiển thị to rõ
            GUIStyle topStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };

            string statusText = (currentState == EnemyState.Patrol) ? "Tuần tra" : ((currentState == EnemyState.Chase) ? "Rượt đuổi" : ((currentState == EnemyState.Attack) ? "Tấn công" : "Cảnh giác"));
            string hpString = $"🐜 {enemyName.ToUpper()}  [{statusText}]  —  {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)} HP";

            topStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(topX, topY - 22, topW, 20), hpString, topStyle);
            topStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(topX - 1, topY - 23, topW, 20), hpString, topStyle);
        }

        // -------------------------------------------------------------
        // B. THANH MÁU NỔI TRÊN ĐẦU QUÁI VẬT (OVERHEAD FLOATING BAR)
        // -------------------------------------------------------------
        if (showHealthBar)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 headPos = transform.position + Vector3.up * healthBarHeightOffset;
                Vector3 screenPos = mainCamera.WorldToScreenPoint(headPos);

                // Chỉ vẽ khi nằm trong khung nhìn camera phía trước
                if (screenPos.z > 0 && screenPos.x >= -80 && screenPos.x <= Screen.width + 80)
                {
                    float width = healthBarWidth;
                    float height = healthBarHeight;
                    float x = screenPos.x - width * 0.5f;
                    float y = Screen.height - screenPos.y - height;

                    // 1. Viền ngoài đen đậm có độ dày
                    GUI.color = new Color(0f, 0f, 0f, 0.95f);
                    GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);

                    // 2. Nền thanh tối
                    GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
                    GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

                    // 3. Thanh máu màu đỏ tươi / cam rực rỡ
                    float barFill = displayedHealthRatio * width;
                    GUI.color = hpColor;
                    GUI.DrawTexture(new Rect(x, y, barFill, height), Texture2D.whiteTexture);

                    // 4. Nhãn tên và chỉ số máu hiển thị sắc nét phía trên thanh
                    GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = 11
                    };

                    labelStyle.normal.textColor = Color.black;
                    GUI.Label(new Rect(x, y - 18, width, 16), $"🐜 {enemyName}: {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", labelStyle);
                    labelStyle.normal.textColor = (currentHealth < 20f) ? Color.yellow : Color.white;
                    GUI.Label(new Rect(x - 1, y - 19, width, 16), $"🐜 {enemyName}: {Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}", labelStyle);

                    // 5. Mũi tên tam giác vàng chỉ thẳng vào đỉnh đầu quái
                    GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = 11
                    };
                    arrowStyle.normal.textColor = Color.yellow;
                    GUI.Label(new Rect(screenPos.x - 10, y + height, 20, 14), "▼", arrowStyle);

                    // 6. Hiệu ứng UI sát thương / tấn công nổi (Damage Popup)
                    if (Time.time - lastDamageTextTime < 1.35f && !string.IsNullOrEmpty(lastDamageText))
                    {
                        float elapsed = Time.time - lastDamageTextTime;
                        float floatY = y - 36f - (elapsed * 24f);
                        GUIStyle combatStyle = new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontStyle = FontStyle.Bold,
                            fontSize = 15
                        };
                        combatStyle.normal.textColor = Color.black;
                        GUI.Label(new Rect(x - 45f + 1, floatY + 1, width + 90f, 22f), lastDamageText, combatStyle);
                        combatStyle.normal.textColor = lastDamageColor;
                        GUI.Label(new Rect(x - 45f, floatY, width + 90f, 22f), lastDamageText, combatStyle);
                    }
                }
            }
        }

        GUI.color = Color.white;
    }
    #endregion

    #region Sensors (Vision & Hearing)
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
        // Bước chân nhẹ không kích động nếu quái đang ở xa lãnh thổ
        float multiplier = (soundType == SoundEmitter.SoundType.CombatAction) ? 1.0f : (soundType == SoundEmitter.SoundType.Footstep ? 0.60f : 0.8f);
        float effectiveRadius = hearingRadius * loudness * multiplier;

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

    void OnDrawGizmos()
    {
        // 1. Vẽ vòng tròn Vùng Lãnh Thổ Tuần Tra (Màu xanh lá sáng)
        Vector3 center = (territoryCenter.sqrMagnitude > 0.001f) ? territoryCenter : transform.position;

        if (useTerritoryPatrol)
        {
            Gizmos.color = new Color(0.15f, 0.85f, 0.35f, 0.45f);
            Gizmos.DrawWireSphere(center, territoryRadius);
        }

        // 2. Vẽ Lộ trình tuần tra các Waypoints (Màu xanh Cyan)
        Gizmos.color = Color.cyan;
        if (waypoints != null && waypoints.Length > 0)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null)
                {
                    Gizmos.DrawSphere(waypoints[i].position, 0.025f);
                    int next = (i + 1) % waypoints.Length;
                    if (waypoints[next] != null)
                        Gizmos.DrawLine(waypoints[i].position, waypoints[next].position);
                }
            }
        }
        else if (useTerritoryPatrol && territoryWaypoints != null && territoryWaypoints[0].sqrMagnitude > 0.001f)
        {
            for (int i = 0; i < territoryWaypoints.Length; i++)
            {
                Gizmos.DrawSphere(territoryWaypoints[i], 0.025f);
                int next = (i + 1) % territoryWaypoints.Length;
                Gizmos.DrawLine(territoryWaypoints[i], territoryWaypoints[next]);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // 1. Bán kính thính giác (Màu tím)
        Gizmos.color = new Color(0.7f, 0.2f, 0.9f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        // 2. Nón tầm nhìn FOV (Màu vàng khi bình thường, Màu đỏ khi thấy Player)
        bool playerInSight = (targetPlayer != null && CanSeeTarget(targetPlayer));
        Gizmos.color = playerInSight ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 forward = transform.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle * 0.5f, 0) * forward;

        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

        // 3. Tầm tấn công cắn cận chiến (Màu đỏ tươi)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
