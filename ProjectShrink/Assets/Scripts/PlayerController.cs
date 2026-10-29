using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectShrink.AI;

/// <summary>
/// Bộ điều khiển nhân vật toàn diện (Full Combat, Sensors, Crafting & Ladder Climbing):
/// - Phản hồi tức thì 0.00s delay, chống lag (Zero GC Allocation).
/// - Tự động cộng +25 Sát Thương & tăng tầm đánh khi cầm [Giáo Gai Độc] hoặc +18 Sát Thương khi cầm [Búa Đá].
/// - Tích hợp Leo Thang (LadderClimbable) với phím W/S/Space.
/// - Phát sóng âm thanh bước chân & vung đòn (SoundEmitter) để kích hoạt Cảm biến Thính giác của Enemy.
/// - Cơ chế Né đòn (Dodge) bằng phím LeftShift: Khi đang né, quái vật tấn công sẽ bị Miss ([ENEMY MISS]).
/// - Giới hạn phạm vi di chuyển an toàn trong phòng (Room Boundary Clamping).
/// - Tự động gắn và kích hoạt PlayerHealth & InventoryManager.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("1. Tốc Độ Di Chuyển & Chiến Đấu")]
    public float walkSpeed = 0.36f;
    public float runSpeed = 0.72f;
    public float rotationSpeed = 35f;
    public float baseAttackDamage = 15f;

    [Header("2. Nhảy & Mặt Sàn")]
    public float jumpForce = 3.5f;
    public float gravity = -9.81f;
    public bool lockToGroundY = true;
    public float groundY = 0.08f;

    [Header("3. Giới Hạn Phòng (Không Cho Ra Khỏi Phòng)")]
    public bool limitToRoom = true;
    public float minX = 1.32f;
    public float maxX = 1.98f;
    public float minZ = -0.75f;
    public float maxZ = 1.88f;

    [Header("4. Va Chạm Vật Thể (Collision)")]
    public bool checkCollisions = true;
    public float playerRadius = 0.025f;
    public float playerHeight = 0.068f;
    public float minEnemySeparation = 0.085f;
    public LayerMask collisionMask = ~0;

    [Header("5. Tham Chiếu")]
    public Animator animator;
    public Transform cameraTransform;

    [Header("6. Né Đòn (Dodge)")]
    public bool isDodging = false;
    public float dodgeDuration = 0.45f;
    private float lastDodgeTime = -10f;

    private CharacterController characterController;
    private Vector3 currentMoveVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private bool isGrounded = true;
    private bool isAttacking = false;
    private float attackStartTime = -10f;
    private float footstepTimer = 0f;

    private static readonly RaycastHit[] sphereCastHits = new RaycastHit[8];
    private static readonly Collider[] attackOverlapHits = new Collider[8];

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int HashPunching = Animator.StringToHash("punching");
    private static readonly int HashHook = Animator.StringToHash("Hook");
    private static readonly int HashKick = Animator.StringToHash("kick");
    private static readonly int HashKick2 = Animator.StringToHash("kick_2");
    private static readonly int HashKick3 = Animator.StringToHash("kick_3");
    private static readonly int HashFlyingKickTrig = Animator.StringToHash("flyingkick");

    public static PlayerController Instance { get; private set; }

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null || Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null || Instance == null)
        {
            Instance = this;
        }

        characterController = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;

        // Tự động gắn PlayerHealth
        PlayerHealth ph = GetComponent<PlayerHealth>();
        if (ph == null) ph = gameObject.AddComponent<PlayerHealth>();
        ph.enabled = true;
        ph.showHealthUI = true;

        // Tự động gắn InventoryManager
        InventoryManager inv = GetComponent<InventoryManager>();
        if (inv == null) inv = gameObject.AddComponent<InventoryManager>();
        inv.enabled = true;

        walkSpeed = 0.36f;
        runSpeed = 0.72f;
        if (rotationSpeed < 30f) rotationSpeed = 35f;

        minX = 1.32f;
        maxX = 1.98f;
        minZ = -0.75f;
        maxZ = 1.88f;
        playerRadius = 0.025f;
        playerHeight = 0.068f;

        if (lockToGroundY)
            groundY = transform.position.y;
    }

    void Update()
    {
        // 1. Xử lý Né Đòn (Dodge / Roll) bằng phím LeftShift
        HandleDodge();

        // 2. Xử lý Tấn Công
        HandleDedicatedAttacks();

        // 3. Cơ chế Leo Thang (Ladder Climbing)
        LadderClimbable nearbyLadder = LadderClimbable.GetNearbyLadder(transform.position, 0.16f);
        bool isOnLadder = false;

        float h = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

        float v = 0f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;

        if (nearbyLadder != null)
        {
            float climbInput = v;
            if (climbInput != 0f)
            {
                isOnLadder = true;
                isGrounded = false;
                Vector3 p = transform.position;
                p.y = Mathf.Clamp(p.y + climbInput * 0.34f * Time.deltaTime, groundY, nearbyLadder.topY + 0.06f);
                transform.position = p;
            }
            else if (transform.position.y > groundY + 0.02f && transform.position.y <= nearbyLadder.topY + 0.06f)
            {
                isOnLadder = true;
                isGrounded = false;
                verticalVelocity = 0f;
            }
        }

        // Nhảy (Space)
        if (Input.GetKeyDown(KeyCode.Space) && (isGrounded || isOnLadder) && !isAttacking)
        {
            verticalVelocity = jumpForce * 0.45f;
            isGrounded = false;
            isOnLadder = false;
        }

        // Trọng lực & Giữ mặt sàn (khi không ở trên thang)
        if (!isOnLadder)
        {
            if (!isGrounded)
            {
                verticalVelocity += gravity * 0.45f * Time.deltaTime;
                Vector3 vMove = new Vector3(0, verticalVelocity * Time.deltaTime, 0);
                if (characterController != null && characterController.enabled)
                {
                    characterController.Move(vMove);
                    if (characterController.isGrounded || transform.position.y <= groundY)
                    {
                        Vector3 p = transform.position;
                        p.y = groundY;
                        transform.position = p;
                        verticalVelocity = 0f;
                        isGrounded = true;
                    }
                }
                else
                {
                    transform.position += vMove;
                    if (transform.position.y <= groundY)
                    {
                        Vector3 p = transform.position;
                        p.y = groundY;
                        transform.position = p;
                        verticalVelocity = 0f;
                        isGrounded = true;
                    }
                }
            }
            else if (lockToGroundY && (characterController == null || !characterController.enabled))
            {
                Vector3 p = transform.position;
                p.y = groundY;
                transform.position = p;
            }
        }

        // 4. Di chuyển trên mặt phẳng X-Z
        bool isMoving = (Mathf.Abs(h) > 0.05f || Mathf.Abs(v) > 0.05f);
        bool isRunning = isMoving && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightShift));
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        if (isAttacking && (Time.time - attackStartTime < 0.20f))
        {
            currentSpeed *= 0.25f;
        }

        Vector3 moveDir = Vector3.zero;
        if (isMoving && !isOnLadder)
        {
            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camFwd.y = 0; camRight.y = 0;
            camFwd.Normalize(); camRight.Normalize();

            moveDir = (camRight * h + camFwd * v).normalized;
            currentMoveVelocity = moveDir * currentSpeed;

            // Xoay mặt theo hướng chạy
            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            Vector3 step = currentMoveVelocity * Time.deltaTime;
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(step);
            }
            else
            {
                transform.position += step;
            }

            // 5. Phát sóng âm thanh bước chân (Hearing Sensor trigger)
            footstepTimer += Time.deltaTime;
            float stepInterval = isRunning ? 0.32f : 0.50f;
            if (footstepTimer >= stepInterval)
            {
                footstepTimer = 0f;
                float loudness = isRunning ? 2.5f : 1.4f;
                SoundEmitter.EmitSound(transform.position, SoundEmitter.SoundType.Footstep, loudness);
            }
        }
        else
        {
            currentMoveVelocity = Vector3.zero;
            footstepTimer = 0f;
        }

        // 6. Giới hạn phạm vi trong phòng
        if (limitToRoom)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.z = Mathf.Clamp(p.z, minZ, maxZ);
            transform.position = p;
        }

        // 7. Cập nhật Animator
        if (animator != null)
        {
            float animSpeed = 0f;
            if (isMoving) animSpeed = isRunning ? 2f : 1f;
            animator.SetFloat(HashSpeed, animSpeed);
            animator.SetBool(HashIsGrounded, isGrounded || isOnLadder);
        }
    }

    void HandleDodge()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && !isDodging && Time.time - lastDodgeTime > 0.8f)
        {
            StartCoroutine(DodgeRoutine());
        }
    }

    IEnumerator DodgeRoutine()
    {
        isDodging = true;
        lastDodgeTime = Time.time;
        Debug.Log("💨 [PLAYER DODGE] Người chơi kích hoạt NÉ ĐÒN!");

        // Lướt nhanh một đoạn nhỏ theo hướng đang nhìn hoặc đang di chuyển
        Vector3 dodgeDir = currentMoveVelocity.sqrMagnitude > 0.01f ? currentMoveVelocity.normalized : transform.forward;
        float elapsed = 0f;
        while (elapsed < dodgeDuration)
        {
            elapsed += Time.deltaTime;
            transform.position += dodgeDir * 0.75f * Time.deltaTime;
            yield return null;
        }

        isDodging = false;
    }

    void HandleDedicatedAttacks()
    {
        if (isAttacking) return;

        if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
        {
            TriggerAttack(HashPunching, 0.40f, 15f);
        }
        else if (Input.GetKeyDown(KeyCode.K) || Input.GetMouseButtonDown(1))
        {
            TriggerAttack(HashHook, 0.45f, 18f);
        }
        else if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.E))
        {
            TriggerAttack(HashKick, 0.50f, 20f);
        }
        else if (Input.GetKeyDown(KeyCode.U))
        {
            TriggerAttack(HashKick2, 0.50f, 22f);
        }
        else if (Input.GetKeyDown(KeyCode.I))
        {
            TriggerAttack(HashKick3, 0.55f, 25f);
        }
        else if (Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.F))
        {
            TriggerAttack(HashFlyingKickTrig, 0.65f, 30f);
        }
    }

    void TriggerAttack(int triggerHash, float duration, float baseDamage)
    {
        isAttacking = true;
        attackStartTime = Time.time;

        // Phát sóng âm thanh đánh nhau (Hearing Sensor)
        SoundEmitter.EmitSound(transform.position, SoundEmitter.SoundType.CombatAction, 3.2f);

        // Tính toán sát thương cộng dồn từ vũ khí chế tạo
        float finalDamage = baseDamage;
        bool spearEquipped = (InventoryManager.Instance != null && InventoryManager.Instance.isSpearEquipped);
        bool hammerEquipped = (InventoryManager.Instance != null && InventoryManager.Instance.isHammerEquipped);

        if (spearEquipped) finalDamage += InventoryManager.Instance.spearBonusDamage;
        else if (hammerEquipped) finalDamage += InventoryManager.Instance.hammerBonusDamage;

        float lookAndHitRange = spearEquipped ? 0.42f : 0.30f;
        float actualHitRange = spearEquipped ? 0.32f : 0.22f;

        // Tự động quay mặt về quái vật gần nhất
        EnemyAI nearestEnemy = FindNearestAliveEnemy(lookAndHitRange);
        if (nearestEnemy != null)
        {
            Vector3 toEnemy = nearestEnemy.transform.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toEnemy.normalized);
            }
        }

        if (animator != null)
        {
            animator.SetTrigger(triggerHash);
        }

        StopCoroutine(nameof(DealDamageAfterDelay));
        StartCoroutine(DealDamageAfterDelay(0.12f, finalDamage, actualHitRange));

        CancelInvoke(nameof(ResetAttackState));
        Invoke(nameof(ResetAttackState), duration);
    }

    EnemyAI FindNearestAliveEnemy(float maxDist)
    {
        EnemyAI best = null;
        float bestDistSq = maxDist * maxDist;
        Vector3 myPos = transform.position;

        List<EnemyAI> enemies = EnemyAI.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyAI e = enemies[i];
            if (e == null || e.currentHealth <= 0f) continue;
            float dx = e.transform.position.x - myPos.x;
            float dz = e.transform.position.z - myPos.z;
            float dSq = dx * dx + dz * dz;
            if (dSq <= bestDistSq)
            {
                bestDistSq = dSq;
                best = e;
            }
        }
        return best;
    }

    IEnumerator DealDamageAfterDelay(float delay, float damage, float hitRange)
    {
        yield return new WaitForSeconds(delay);

        EnemyAI nearest = FindNearestAliveEnemy(hitRange);
        if (nearest != null)
        {
            nearest.TakeDamage(damage);
            yield break;
        }

        Vector3 hitCenter = transform.position + transform.forward * 0.12f + Vector3.up * 0.03f;
        int hitCount = Physics.OverlapSphereNonAlloc(hitCenter, hitRange, attackOverlapHits, ~0, QueryTriggerInteraction.Collide);
        Transform myRoot = transform.root;
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = attackOverlapHits[i];
            if (col == null || col.transform.root == myRoot) continue;

            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy == null) enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                break;
            }
        }
    }

    void ResetAttackState()
    {
        isAttacking = false;
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        LadderClimbable ladder = LadderClimbable.GetNearbyLadder(transform.position, 0.16f);
        if (ladder != null && (InventoryManager.Instance == null || !InventoryManager.Instance.isInventoryOpen))
        {
            float uiScale = Mathf.Max(1f, Screen.height / 540f);
            Matrix4x4 prevM = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float vW = Screen.width / uiScale;
            float vH = Screen.height / uiScale;
            float hintW = 340f;
            float hintH = 30f;
            float hintX = (vW - hintW) * 0.5f;
            float hintY = vH - 120f;

            GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.90f);
            GUI.DrawTexture(new Rect(hintX, hintY, hintW, hintH), Texture2D.whiteTexture);
            GUI.color = new Color(0.40f, 0.90f, 0.60f, 0.95f);
            GUI.Box(new Rect(hintX, hintY, hintW, hintH), "");
            GUI.color = Color.white;

            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 13
            };
            hintStyle.normal.textColor = new Color(1f, 0.92f, 0.40f);
            GUI.Label(new Rect(hintX, hintY, hintW, hintH), "🪜 [W / Phím Lên] Leo Lên   |   [S / Phím Xuống] Leo Xuống", hintStyle);

            GUI.matrix = prevM;
        }
    }
}
