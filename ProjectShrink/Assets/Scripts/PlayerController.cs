using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectShrink.AI;

/// <summary>
/// Bộ điều khiển nhân vật đồng bộ 100% với Animator character_v2:
/// - Parameters: Speed, IsGrounded, Jump
/// - Attack Triggers: punching (Jab), Hook (Hook Punch), kick (Kick)
/// - Phím: J / Chuột trái (punching), K / Chuột phải (Hook), L / E (kick)
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("1. Tốc Độ Di Chuyển")]
    public float walkSpeed = 0.55f;
    public float runSpeed = 1.35f;
    public float rotationSpeed = 16f;

    [Header("2. Nhảy & Mặt Sàn")]
    public float jumpForce = 3.5f;
    public float gravity = -9.81f;
    public bool lockToGroundY = true;
    public float groundY = 0.08f;

    [Header("3. Giới Hạn Phòng (Không Cho Ra Khỏi Phòng)")]
    public bool limitToRoom = true;
    public float minX = 1.32f;
    public float maxX = 1.98f; // Thu hẹp bờ tường bên phải để không đi xuyên tường
    public float minZ = -0.75f;
    public float maxZ = 1.88f;

    [Header("4. Va Chạm Vật Thể (Collision)")]
    public bool checkCollisions = true;
    public float playerRadius = 0.08f;
    public float playerHeight = 0.25f;
    public LayerMask collisionMask = ~0; // Mặc định va chạm với tất cả Layers

    [Header("5. Tham Chiếu")]
    public Animator animator;
    public Transform cameraTransform;

    private CharacterController characterController;
    private Vector3 currentMoveVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private bool isGrounded = true;
    private bool isAttacking = false;
    private float footstepTimer = 0f;

    void Start()
    {
        // Tự động căn chỉnh vừa khít căn phòng (tường bên phải giới hạn tại X = 1.98 và Z = 1.88)
        minX = 1.32f;
        maxX = 1.98f;
        minZ = -0.75f;
        maxZ = 1.88f;
        playerRadius = 0.08f;
        playerHeight = 0.25f;

        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            // Tối ưu: Luôn phát animation mượt, không bị Unity culling làm lag/delay
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (lockToGroundY)
            groundY = transform.position.y;
    }

    void Update()
    {
        HandleDedicatedAttacks();

        // Đọc phím trực tiếp từ phần cứng (0.00s latency - không phụ thuộc InputManager smoothing)
        float h = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

        float v = 0f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;

        // Dự phòng cho Gamepad
        if (h == 0f) h = Input.GetAxisRaw("Horizontal");
        if (v == 0f) v = Input.GetAxisRaw("Vertical");

        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        if (cameraTransform != null)
        {
            camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();
        }

        Vector3 moveDir = (camRight * h + camForward * v).normalized;
        bool isMoving = moveDir.sqrMagnitude > 0.001f && !isAttacking;
        bool isRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        float speed = isRunning ? runSpeed : walkSpeed;

        if (isMoving)
        {
            // Xoay hướng mượt mà, dứt khoát nhưng không bị giật cục
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

            // Gia tốc êm ái, vào guồng mượt mà sau 0.05s
            currentMoveVelocity = Vector3.Lerp(currentMoveVelocity, moveDir * speed, 20f * Time.deltaTime);
            Vector3 moveStep = currentMoveVelocity * Time.deltaTime;

            // 1. Nếu có CharacterController: Dùng Move của CharacterController để tự cản tường
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(moveStep);
            }
            // 2. Nếu không có CharacterController: Dùng SphereCast quét va chạm để không đi xuyên vật thể
            else
            {
                if (checkCollisions)
                {
                    Vector3 rayOrigin = transform.position + Vector3.up * (playerHeight * 0.5f);
                    if (Physics.SphereCast(rayOrigin, playerRadius, moveDir, out RaycastHit hit, moveStep.magnitude + 0.05f, collisionMask, QueryTriggerInteraction.Ignore))
                    {
                        // Trượt dọc theo tường thay vì đứng sững
                        Vector3 wallNormal = hit.normal;
                        wallNormal.y = 0f;
                        wallNormal.Normalize();
                        moveDir = Vector3.ProjectOnPlane(moveDir, wallNormal).normalized;
                        moveStep = moveDir * speed * Time.deltaTime;

                        // Kiểm tra lại lần 2 hướng trượt
                        if (Physics.SphereCast(rayOrigin, playerRadius, moveDir, out _, moveStep.magnitude + 0.05f, collisionMask, QueryTriggerInteraction.Ignore))
                        {
                            moveStep = Vector3.zero;
                        }
                    }
                }
                transform.position += moveStep;
            }

            // Phát sóng âm thanh bước chân khi chạy (Cảm biến Thính giác của quái nghe được)
            if (isRunning)
            {
                footstepTimer += Time.deltaTime;
                if (footstepTimer >= 0.35f)
                {
                    footstepTimer = 0f;
                    SoundEmitter.Emit(transform.position, 5.5f, gameObject);
                }
            }
        }

        // Nhảy (Space)
        if (Input.GetButtonDown("Jump") && isGrounded && !isAttacking)
        {
            verticalVelocity = jumpForce;
            isGrounded = false;
            // Phát âm thanh tiếng nhảy
            SoundEmitter.Emit(transform.position, 7.0f, gameObject);
            if (animator != null)
            {
                animator.SetTrigger("Jump");
                animator.SetTrigger("jump");
            }
        }

        // Trọng lực & Giữ mặt sàn
        if (!isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
            Vector3 vMove = new Vector3(0, verticalVelocity * Time.deltaTime, 0);

            if (characterController != null && characterController.enabled)
            {
                characterController.Move(vMove);
                if (characterController.isGrounded || transform.position.y <= groundY)
                {
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

        // Giới hạn trong phòng (Room Boundary Clamping - Không thể ra ngoài phòng dù tường bị hở)
        if (limitToRoom)
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, minX, maxX);
            p.z = Mathf.Clamp(p.z, minZ, maxZ);
            transform.position = p;
        }

        // Cập nhật Animator Speed (Gửi cả Speed hoa và speed thường để chắc chắn 100%)
        if (animator != null)
        {
            float targetAnimSpeed = 0f;
            if (isMoving) targetAnimSpeed = isRunning ? 2f : 1f;

            animator.SetFloat("Speed", targetAnimSpeed);
            animator.SetBool("IsGrounded", isGrounded);
        }
    }

    void HandleDedicatedAttacks()
    {
        if (!isGrounded || isAttacking) return;

        // 1. ĐẤM THẲNG: J hoặc Chuột Trái -> Trigger 'punching'
        if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0))
        {
            TriggerAttack("punching", 0.5f);
        }
        // 2. ĐẤM MÓC: K hoặc Chuột Phải -> Trigger 'Hook'
        else if (Input.GetKeyDown(KeyCode.K) || Input.GetMouseButtonDown(1))
        {
            TriggerAttack("Hook", 0.6f);
        }
        // 3. ĐÁ CƠ BẢN: L hoặc E -> Trigger 'kick'
        else if (Input.GetKeyDown(KeyCode.L) || Input.GetKeyDown(KeyCode.E))
        {
            TriggerAttack("kick", 0.7f);
        }
        // 4. ĐÁ 2: U -> Trigger 'kick_2'
        else if (Input.GetKeyDown(KeyCode.U))
        {
            TriggerAttack("kick_2", 0.7f);
        }
        // 5. ĐÁ 3: I -> Trigger 'kick_3'
        else if (Input.GetKeyDown(KeyCode.I))
        {
            TriggerAttack("kick_3", 0.8f);
        }
        // 6. PHI CƯỚC (FLYING KICK): O hoặc F -> Trigger 'flyingkick' & 'Flying_kick'
        else if (Input.GetKeyDown(KeyCode.O) || Input.GetKeyDown(KeyCode.F))
        {
            TriggerAttack("flyingkick", 0.9f);
            if (animator != null) animator.SetTrigger("Flying_kick");
        }
    }

    void TriggerAttack(string triggerName, float duration)
    {
        isAttacking = true;
        if (animator != null)
        {
            animator.ResetTrigger("punching");
            animator.ResetTrigger("Hook");
            animator.ResetTrigger("kick");
            animator.ResetTrigger("kick_2");
            animator.ResetTrigger("kick_3");
            animator.ResetTrigger("flyingkick");
            animator.ResetTrigger("Flying_kick");
            animator.SetTrigger(triggerName);
        }

        // Gây sát thương đòn đánh lên quái vật phía trước
        float damage = triggerName.Contains("kick") ? 20f : 15f;
        StartCoroutine(DealDamageAfterDelay(0.2f, damage));

        CancelInvoke(nameof(ResetAttackState));
        Invoke(nameof(ResetAttackState), duration);
    }

    IEnumerator DealDamageAfterDelay(float delay, float damage)
    {
        yield return new WaitForSeconds(delay);
        Vector3 hitCenter = transform.position + transform.forward * 0.18f + Vector3.up * 0.05f;
        Collider[] hitColliders = Physics.OverlapSphere(hitCenter, 0.22f);
        foreach (var col in hitColliders)
        {
            if (col.transform.root == transform.root) continue;

            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy == null) enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                continue;
            }

            AIEntity baseAi = col.GetComponent<AIEntity>();
            if (baseAi == null) baseAi = col.GetComponentInParent<AIEntity>();
            if (baseAi != null)
            {
                baseAi.TakeDamage(damage);
            }
        }
    }

    void ResetAttackState()
    {
        isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        if (limitToRoom)
        {
            Gizmos.color = Color.green;
            Vector3 center = new Vector3((minX + maxX) * 0.5f, transform.position.y + 1f, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(Mathf.Abs(maxX - minX), 2f, Mathf.Abs(maxZ - minZ));
            Gizmos.DrawWireCube(center, size);
        }

        if (checkCollisions && characterController == null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, playerRadius);
        }
    }
}