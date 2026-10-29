using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShrink.AI;

/// <summary>
/// Trí tuệ Nhân tạo Enemy (Kiến Khổng Lồ / Bọ Cánh Cứng / Mutant Rat) theo chuẩn Đồ Án FA26SE278:
/// - FSM 5 trạng thái: Idle, Patrol, Alert (Điều tra âm thanh), Chase (Rượt đuổi), Attack (Tấn công), Dead.
/// - Cảm biến Thị giác (Vision Sensor): Nón nhìn FOV (viewAngle = 85°, viewRadius = 2.2m) + Raycast chặn nhìn xuyên tường (obstacleMask).
/// - Cảm biến Thính giác (Hearing Sensor): Lắng nghe SoundEmitter khi Player chạy/vung vũ khí (hearingRadius = 2.5m).
/// - Combat 2 chiều: Tấn công gây sát thương lên PlayerHealth, nhận sát thương (TakeDamage), xử lý Né Tránh Miss đòn ([ENEMY MISS]).
/// - Thanh máu nổi (HealthBar UI) vẽ trực tiếp trên đầu quái vật mượt mà (Zero GC Allocation).
/// - Tự động khóa mục tiêu người chơi (EnsureTargetPlayer) vào player_v2.
/// - Chống quái vật dính chồng lên nhau (minEnemySeparation) & Danh sách ActiveEnemies toàn cục.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyAI : MonoBehaviour
{
    public enum Species { Beetle = 0, Ant = 1 }
    public enum EnemyState { Idle, Patrol, Alert, Chase, Attack, Dead }

    public static readonly List<EnemyAI> ActiveEnemies = new List<EnemyAI>();

    [Header("0. Thông Tin Quái Vật")]
    public Species species = Species.Ant;
    public string enemyName = "Kiến Khổng Lồ";

    [Header("1. Chỉ Số Chiến Đấu (Combat Stats)")]
    public float maxHealth = 45f;
    public float currentHealth = 45f;
    [Tooltip("Tốc độ bò tuần tra")]
    public float patrolSpeed = 0.18f;
    [Tooltip("Tốc độ bò rượt đuổi người chơi")]
    public float chaseSpeed = 0.45f;
    public float rotationSpeed = 8f;
    public float attackDamage = 15f;
    [Tooltip("Khoảng cách chạm sát vào người chơi để cắn (mét)")]
    public float attackRange = 0.22f;
    public float attackCooldown = 1.4f;
    [Tooltip("Khoảng cách tối thiểu chống đè quái vật lên nhau")]
    public float minEnemySeparation = 0.085f;

    // Tương thích ngược với các Editor Script cũ
    public float moveSpeed { get => patrolSpeed; set => patrolSpeed = value; }
    public float detectionRange { get => viewRadius; set => viewRadius = value; }

    [Header("2. Cảm Biến Thị Giác (Vision Sensor)")]
    [Tooltip("Bán kính tầm nhìn hình nón (mét)")]
    public float viewRadius = 2.2f;
    [Range(0, 360)]
    [Tooltip("Góc quét hình nón FOV (độ)")]
    public float viewAngle = 85f;
    [Tooltip("Layer vật cản che khuất tầm nhìn (Tường, Cửa, Bàn Ghế)")]
    public LayerMask obstacleMask;

    [Header("3. Cảm Biến Thính Giác (Hearing Sensor)")]
    [Tooltip("Bán kính tai nghe tiếng chân chạy / đánh nhau")]
    public float hearingRadius = 2.5f;
    [Tooltip("Thời gian cảnh giác điều tra âm thanh (giây)")]
    public float alertInvestigateDuration = 3.0f;

    [Header("4. Tuần Tra (Patrol)")]
    public Transform[] waypoints;
    public float waypointTolerance = 0.12f;
    public float waitAtWaypointTime = 1.8f;
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
        if (maxHealth <= 0f) maxHealth = 45f;
        currentHealth = maxHealth;
        spawnPosition = transform.position;
        enemyCollider = GetComponent<Collider>();
        navAgent = GetComponent<NavMeshAgent>();

        if (eyeTransform == null) eyeTransform = transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        mainCamera = Camera.main;
    }

    void OnEnable()
    {
        if (!ActiveEnemies.Contains(this))
            ActiveEnemies.Add(this);

        // Đăng ký nhận sóng âm thanh toàn cục (Hearing Sensor)
        SoundEmitter.OnSoundEmitted += HandleSoundHeard;
    }

    void OnDisable()
    {
        ActiveEnemies.Remove(this);
        SoundEmitter.OnSoundEmitted -= HandleSoundHeard;
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

        // Tự động tìm Prefab HealthSyringe rơi đồ nếu chưa gán
        if (dropItemPrefab == null)
        {
            dropItemPrefab = Resources.Load<GameObject>("HealthSyringe");
            if (dropItemPrefab == null)
            {
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

        float initialZ = Mathf.Clamp(spawnPosition.z - patrolDistance, -0.5f, 1.6f);
        currentPatrolTarget = new Vector3(spawnPosition.x, spawnPosition.y, initialZ);

        ChangeState(EnemyState.Idle);
    }

    void Update()
    {
        if (currentState == EnemyState.Dead) return;
        EnsureTargetPlayer();

        // 1. Phân tách quái vật chống chồng lấn (Multi-Enemy Separation)
        ApplySeparation();

        // 2. Chạy FSM
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Alert:
                UpdateAlert();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
        }

        // 3. Quét Cảm biến Thị giác FOV liên tục khi chưa Chase
        if (currentState != EnemyState.Chase && currentState != EnemyState.Attack)
        {
            if (targetPlayer != null && CanSeeTarget(targetPlayer))
            {
                Debug.Log($"👁️ [{enemyName}] Phát hiện Người chơi qua Cảm biến Thị giác FOV!");
                ChangeState(EnemyState.Chase);
            }
        }
    }

    void ApplySeparation()
    {
        if (minEnemySeparation <= 0f) return;
        Vector3 myPos = transform.position;
        for (int i = 0; i < ActiveEnemies.Count; i++)
        {
            EnemyAI other = ActiveEnemies[i];
            if (other == null || other == this || other.currentState == EnemyState.Dead) continue;

            Vector3 diff = myPos - other.transform.position;
            diff.y = 0f;
            float distSq = diff.sqrMagnitude;
            if (distSq < minEnemySeparation * minEnemySeparation && distSq > 0.00001f)
            {
                float dist = Mathf.Sqrt(distSq);
                Vector3 push = (diff / dist) * (minEnemySeparation - dist) * 2.5f * Time.deltaTime;
                transform.position += push;
            }
        }
    }

    #region FSM States

    void UpdateIdle()
    {
        waypointTimer += Time.deltaTime;
        if (waypointTimer >= waitAtWaypointTime)
        {
            waypointTimer = 0f;
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdatePatrol()
    {
        Vector3 targetPos = GetCurrentPatrolDestination();
        MoveTowards(targetPos, patrolSpeed);

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                      new Vector3(targetPos.x, 0, targetPos.z));
        if (dist <= waypointTolerance)
        {
            AdvanceToNextPatrolPoint();
            ChangeState(EnemyState.Idle);
        }
    }

    void UpdateAlert()
    {
        MoveTowards(soundInvestigationTarget, patrolSpeed * 1.25f);
        alertTimer += Time.deltaTime;

        float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                      new Vector3(soundInvestigationTarget.x, 0, soundInvestigationTarget.z));
        if (dist <= waypointTolerance || alertTimer >= alertInvestigateDuration)
        {
            Debug.Log($"🔎 [{enemyName}] Không thấy ai tại điểm nghi vấn, quay lại tuần tra.");
            ChangeState(EnemyState.Patrol);
        }
    }

    void UpdateChase()
    {
        if (targetPlayer == null)
        {
            ChangeState(EnemyState.Patrol);
            return;
        }

        float dist = Vector3.Distance(transform.position, targetPlayer.position);
        if (dist <= attackRange)
        {
            ChangeState(EnemyState.Attack);
            return;
        }

        // Nếu người chơi chạy quá xa khỏi tầm nhìn
        if (dist > viewRadius * 1.5f && !CanSeeTarget(targetPlayer))
        {
            Debug.Log($"❓ [{enemyName}] Mất dấu Người chơi, chuyển sang cảnh giác!");
            soundInvestigationTarget = targetPlayer.position;
            ChangeState(EnemyState.Alert);
            return;
        }

        MoveTowards(targetPlayer.position, chaseSpeed);
    }

    void UpdateAttack()
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

        if (Time.time - lastAttackTime >= attackCooldown && !isAttacking)
        {
            StartCoroutine(PerformAttackRoutine());
        }
    }

    IEnumerator PerformAttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        if (animator != null)
        {
            animator.SetTrigger("attack");
        }

        yield return new WaitForSeconds(0.35f);

        if (targetPlayer != null && currentState != EnemyState.Dead)
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.position);
            if (dist <= attackRange * 1.45f)
            {
                PlayerController pc = targetPlayer.GetComponent<PlayerController>();
                PlayerHealth ph = targetPlayer.GetComponent<PlayerHealth>();

                // Kiểm tra né đòn Miss (Dodge / Evade)
                if (pc != null && pc.isDodging)
                {
                    Debug.Log($"💨 [ENEMY MISS] {enemyName} cắn hụt vì Người chơi đã NÉ ĐÒN!");
                }
                else if (ph != null)
                {
                    Debug.Log($"💥 [{enemyName}] Cắn trúng Người chơi gây {attackDamage} sát thương!");
                    ph.TakeDamage(attackDamage);
                }
            }
        }

        yield return new WaitForSeconds(0.45f);
        isAttacking = false;
    }

    void ChangeState(EnemyState newState)
    {
        currentState = newState;
        waypointTimer = 0f;
        alertTimer = 0f;

        if (animator != null)
        {
            animator.SetBool("isMoving", newState == EnemyState.Patrol || newState == EnemyState.Chase || newState == EnemyState.Alert);
            animator.SetBool("isChasing", newState == EnemyState.Chase);
            animator.SetBool("isAlert", newState == EnemyState.Alert);
        }
    }

    #endregion

    #region Vision & Hearing Sensors

    /// <summary>
    /// Cảm biến Thị giác FOV (Vision Sensor Cone)
    /// </summary>
    public bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;

        Vector3 origin = eyeTransform != null ? eyeTransform.position : transform.position + Vector3.up * 0.05f;
        Vector3 targetPos = target.position + Vector3.up * 0.05f;
        Vector3 dirToTarget = (targetPos - origin);
        float distToTarget = dirToTarget.magnitude;

        if (distToTarget > viewRadius) return false;

        float angle = Vector3.Angle(transform.forward, dirToTarget.normalized);
        if (angle > viewAngle * 0.5f) return false;

        // Quét Raycast chặn nhìn xuyên tường
        if (Physics.Raycast(origin, dirToTarget.normalized, out RaycastHit hit, distToTarget, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != target && !hit.transform.IsChildOf(target))
            {
                return false; // Bị tường che khuất
            }
        }

        return true;
    }

    /// <summary>
    /// Cảm biến Thính giác (Hearing Sensor)
    /// </summary>
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

    #region Movement Helpers

    void MoveTowards(Vector3 targetPos, float speed)
    {
        RotateTowards(targetPos);
        Vector3 moveDir = (targetPos - transform.position);
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Vector3 step = moveDir.normalized * speed * Time.deltaTime;
            transform.position += step;
        }
    }

    void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    Vector3 GetCurrentPatrolDestination()
    {
        if (waypoints != null && waypoints.Length > 0 && waypoints[currentWaypointIndex] != null)
        {
            return waypoints[currentWaypointIndex].position;
        }
        return currentPatrolTarget;
    }

    void AdvanceToNextPatrolPoint()
    {
        if (waypoints != null && waypoints.Length > 1)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
        else
        {
            patrolDirection *= -1;
            float targetZ = spawnPosition.z + (patrolDistance * patrolDirection);
            targetZ = Mathf.Clamp(targetZ, -0.6f, 1.7f);
            currentPatrolTarget = new Vector3(spawnPosition.x, spawnPosition.y, targetZ);
        }
    }

    #endregion

    #region Combat & Damage

    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"🩸 [{enemyName}] Nhận {damage} sát thương! Máu còn: {Mathf.Max(0, currentHealth)}/{maxHealth}");

        if (animator != null)
        {
            animator.SetTrigger("takeDamage");
        }

        // Tự động quay mặt lại cắn trả khi bị đánh lén
        if (targetPlayer != null && currentState != EnemyState.Attack)
        {
            ChangeState(EnemyState.Chase);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        ChangeState(EnemyState.Dead);
        ActiveEnemies.Remove(this);
        Debug.Log($"💀 [{enemyName}] Đã bị tiêu diệt!");

        if (enemyCollider != null) enemyCollider.enabled = false;
        if (navAgent != null) navAgent.enabled = false;

        if (animator != null)
        {
            animator.SetBool("isDead", true);
            animator.SetTrigger("die");
        }

        // Rơi vật phẩm khi quái chết
        Vector3 dropPos = transform.position + Vector3.up * dropYOffset;
        if (dropItemPrefab != null)
        {
            GameObject dropped = Instantiate(dropItemPrefab, dropPos, Quaternion.identity);
            dropped.name = "HealthSyringe_Dropped";
            Debug.Log($"🎁 Rơi vật phẩm tại: {dropPos}");
        }
        else
        {
            string dropId = (species == Species.Ant) ? "long_bone" : "spider_silk";
            ItemPickup.CreateWorldDropById(dropId, dropPos);
            Debug.Log($"🎁 Quái vật tiêu diệt rơi nguyên liệu: {dropId} tại {dropPos}");
        }

        Destroy(gameObject, 3.5f);
    }

    #endregion

    #region GUI & Visuals

    void OnGUI()
    {
        if (currentState == EnemyState.Dead || mainCamera == null) return;

        Vector3 worldPos = transform.position + Vector3.up * 0.18f;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0) return;

        float barWidth = 70f;
        float barHeight = 8f;
        float x = screenPos.x - barWidth * 0.5f;
        float y = Screen.height - screenPos.y - barHeight;

        displayedHealthRatio = Mathf.Lerp(displayedHealthRatio, Mathf.Clamp01(currentHealth / maxHealth), 10f * Time.deltaTime);

        // Viền đen
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x - 1, y - 1, barWidth + 2, barHeight + 2), Texture2D.whiteTexture);

        // Nền đỏ thẫm
        GUI.color = new Color(0.35f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);

        // Vạch máu (Đỏ tươi)
        GUI.color = new Color(0.95f, 0.20f, 0.20f, 1f);
        GUI.DrawTexture(new Rect(x, y, barWidth * displayedHealthRatio, barHeight), Texture2D.whiteTexture);

        // Tên quái vật
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 10
        };
        nameStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(x - 20, y - 14, barWidth + 40, 14), enemyName, nameStyle);

        GUI.color = Color.white;
    }

    void FixMissingMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    Material mat = new Material(shader) { color = species == Species.Ant ? new Color(0.6f, 0.15f, 0.1f) : new Color(0.2f, 0.5f, 0.2f) };
                    r.material = mat;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Vẽ nón tầm nhìn FOV
        Gizmos.color = Color.yellow;
        Vector3 origin = eyeTransform != null ? eyeTransform.position : transform.position;
        Gizmos.DrawWireSphere(origin, viewRadius);

        Vector3 leftDir = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(origin, leftDir * viewRadius);
        Gizmos.DrawRay(origin, rightDir * viewRadius);

        // Vẽ bán kính thính giác
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, hearingRadius);
    }

    #endregion
}
