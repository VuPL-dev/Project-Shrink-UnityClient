using System.Collections;
using UnityEngine;

/// <summary>
/// Trí tuệ nhân tạo (AI) cho Quái Vật Côn Trùng (Nightmare Beetle):
/// - Tự động phát hiện và đuổi theo nhân vật (Player) khi vào tầm nhìn.
/// - Tấn công người chơi khi áp sát và gây sát thương máu (PlayerHealth).
/// - Có đầy đủ trạng thái: Đi/Chạy, Tấn công (atack1), Bị đánh (gethit), Chết (death).
/// - KHI CHẾT: Tự động rơi ra Ống Tiêm Hồi Máu (Health Syringe) để người chơi nhặt!
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState { Idle, Chase, Attack, Dead }

    [Header("1. Chỉ Số Quái Vật")]
    public float maxHealth = 50f;
    public float currentHealth = 50f;
    public float moveSpeed = 0.45f;
    public float rotationSpeed = 8f;

    [Header("2. Phạm Vi Chiến Đấu")]
    [Tooltip("Khoảng cách bắt đầu phát hiện người chơi")]
    public float detectionRange = 1.8f;
    [Tooltip("Khoảng cách áp sát để cắn/tấn công")]
    public float attackRange = 0.25f;
    public float attackDamage = 15f;
    public float attackCooldown = 1.3f;

    [Header("3. Rơi Đồ Khi Chết (Drop Item)")]
    [Tooltip("Prefab ống tiêm hồi máu sẽ rơi ra khi quái bị tiêu diệt")]
    public GameObject dropItemPrefab;
    public float dropYOffset = 0.04f;

    [Header("4. Tham Chiếu")]
    public Animator animator;
    public Transform targetPlayer;

    private EnemyState currentState = EnemyState.Idle;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    private Collider enemyCollider;

    void Start()
    {
        currentHealth = maxHealth;
        enemyCollider = GetComponent<Collider>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Tự động tìm nhân vật trong Scene nếu chưa gán
        if (targetPlayer == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null)
                targetPlayer = player.transform;
        }

        FixMissingMaterials();
        PlayAnim("idle1", 0.2f);
    }

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

    void Update()
    {
        if (currentState == EnemyState.Dead) return;

        // Nếu mất dấu nhân vật thì thử tìm lại
        if (targetPlayer == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            if (player != null) targetPlayer = player.transform;
            return;
        }

        float distance = Vector3.Distance(transform.position, targetPlayer.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                if (distance <= detectionRange)
                {
                    currentState = EnemyState.Chase;
                    PlayAnim("walk", 0.15f);
                }
                break;

            case EnemyState.Chase:
                if (distance > detectionRange * 1.3f)
                {
                    // Người chơi chạy ra quá xa -> quay lại đứng rình
                    currentState = EnemyState.Idle;
                    PlayAnim("idle1", 0.2f);
                }
                else if (distance <= attackRange)
                {
                    // Đã áp sát -> chuyển sang tấn công
                    currentState = EnemyState.Attack;
                }
                else
                {
                    // Di chuyển đuổi theo nhân vật
                    Vector3 dir = (targetPlayer.position - transform.position);
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
                        transform.position += dir.normalized * moveSpeed * Time.deltaTime;
                    }
                }
                break;

            case EnemyState.Attack:
                if (distance > attackRange * 1.25f && !isAttacking)
                {
                    currentState = EnemyState.Chase;
                    PlayAnim("walk", 0.15f);
                }
                else
                {
                    // Luôn quay mặt vào người chơi khi đánh
                    Vector3 lookDir = (targetPlayer.position - transform.position);
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), rotationSpeed * 2f * Time.deltaTime);
                    }

                    if (Time.time >= lastAttackTime + attackCooldown && !isAttacking)
                    {
                        StartCoroutine(PerformAttackRoutine());
                    }
                }
                break;
        }
    }

    IEnumerator PerformAttackRoutine()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        PlayAnim("atack1", 0.08f);

        // Chờ đúng nhịp vung càng/cắn trúng (khoảng 0.4s)
        yield return new WaitForSeconds(0.4f);

        if (currentState != EnemyState.Dead && targetPlayer != null)
        {
            float dist = Vector3.Distance(transform.position, targetPlayer.position);
            if (dist <= attackRange * 1.35f)
            {
                PlayerHealth playerHealth = targetPlayer.GetComponent<PlayerHealth>();
                if (playerHealth == null) playerHealth = targetPlayer.GetComponentInParent<PlayerHealth>();

                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                    Debug.Log($"💥 [QUÁI VẬT] Côn trùng cắn trúng người chơi! -{attackDamage} HP");
                }
            }
        }

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;

        if (currentState != EnemyState.Dead)
        {
            PlayAnim("idle1", 0.15f);
        }
    }

    /// <summary>
    /// Nhận sát thương khi người chơi đấm/đá trúng
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damage;
        Debug.Log($"⚔️ [PLAYER HIT] Đánh trúng quái vật! Sát thương: {damage} | Máu quái còn: {Mathf.Max(0, currentHealth)}/{maxHealth}");

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
        currentState = EnemyState.Dead;
        PlayAnim("death", 0.1f);

        if (enemyCollider != null)
            enemyCollider.enabled = false;

        Debug.Log("💀 [QUÁI VẬT] Quái vật côn trùng đã bị tiêu diệt!");

        // RƠI RA ỐNG TIÊM HỒI MÁU (HEALTH SYRINGE)
        if (dropItemPrefab != null)
        {
            Vector3 dropPos = transform.position + Vector3.up * dropYOffset;
            GameObject dropped = Instantiate(dropItemPrefab, dropPos, Quaternion.identity);
            dropped.name = "Health_Syringe_Drop";
            Debug.Log("🎁 [DROP ITEM] Đã rơi ra Ống Tiêm Hồi Máu ngay tại vị trí quái chết!");
        }

        // Tự biến mất sau 4 giây
        Destroy(gameObject, 4f);
    }

    void PlayAnim(string stateName, float crossFade = 0.1f)
    {
        if (animator == null) return;
        animator.CrossFade(stateName, crossFade);
    }

    void OnDrawGizmosSelected()
    {
        // Tầm phát hiện (Màu vàng)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Tầm tấn công (Màu đỏ)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
