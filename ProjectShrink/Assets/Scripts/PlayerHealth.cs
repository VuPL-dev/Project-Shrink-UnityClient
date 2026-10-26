using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hệ thống Máu cho nhân vật:
/// - Tự động tìm hoặc tạo Canvas GUI và hiển thị thanh máu to rõ ở góc màn hình.
/// - Đồng thời vẽ GUI cơ bản (OnGUI) đảm bảo 100% LUÔN LUÔN HIỆN DIỆN TRÊN MÀN HÌNH GAME dù Canvas có bị lỗi hay tắt layer.
/// - Phím test: H = trừ 15 máu, G = hồi 15 máu.
/// </summary>
[ExecuteAlways]
public class PlayerHealth : MonoBehaviour
{
    [Header("1. Chỉ Số Máu")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("2. Giao Diện (Tùy chọn)")]
    public Image healthFillBar;
    public TextMeshProUGUI healthText;

    [Header("3. Hiệu Ứng Bị Đánh")]
    public Animator animator;
    public float invulnerableDuration = 0.5f;

    [Header("4. Phím Test (H: Mất máu, G: Hồi máu)")]
    public bool enableTestKeys = true;

    private bool isInvulnerable = false;
    private float displayedFill = 1f;

    public bool IsDead => currentHealth <= 0f;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Cấu hình thanh Canvas Health Bar ở góc dưới bên phải
        if (healthFillBar != null)
        {
            Transform parentBg = healthFillBar.transform.parent;
            if (parentBg != null)
            {
                parentBg.gameObject.SetActive(true);
                RectTransform rt = parentBg.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = new Vector2(-30f, 30f);
                    rt.sizeDelta = new Vector2(280f, 28f);
                }
            }
            healthFillBar.gameObject.SetActive(true);
        }

        UpdateHealthUI(true);
    }

    void Update()
    {
        if (enableTestKeys)
        {
            if (Input.GetKeyDown(KeyCode.H)) TakeDamage(15f);
            if (Input.GetKeyDown(KeyCode.G)) Heal(15f);
        }

        float target = currentHealth / maxHealth;
        displayedFill = Mathf.Lerp(displayedFill, target, 12f * Time.deltaTime);

        if (healthFillBar != null && healthFillBar.gameObject.activeInHierarchy)
        {
            healthFillBar.fillAmount = displayedFill;
        }
    }

    /// <summary>
    /// THANH MÁU PLAYER: Thiết kế to rõ, hiện đại, phong cách Cyberpunk / Phiêu lưu
    /// </summary>
    void OnGUI()
    {
        GUI.depth = -100;

        // Góc dưới cùng bên phải màn hình
        float width = 280f;
        float height = 26f;
        float viewW = Screen.width;
        float viewH = Screen.height;
        float x = Mathf.Clamp(viewW - width - 30f, 20f, viewW - 20f);
        float y = Mathf.Clamp(viewH - height - 30f, 20f, viewH - 20f);

        // 1. Viền ngoài bóng bẩy
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        GUI.DrawTexture(new Rect(x - 3, y - 3, width + 6, height + 6), Texture2D.whiteTexture);

        // 2. Nền thanh tối có độ sâu
        GUI.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        // 3. Thanh máu đổi màu thông minh: Xanh lục tươi khi đầy, Vàng khi trung bình, Đỏ rực khi nguy cấp
        float targetFill = Mathf.Clamp01(displayedFill);
        Color healthColor;
        if (targetFill > 0.6f)
            healthColor = new Color(0.18f, 0.85f, 0.35f, 1f); // Xanh lục tươi
        else if (targetFill > 0.3f)
            healthColor = new Color(0.95f, 0.75f, 0.15f, 1f); // Vàng cam
        else
            healthColor = new Color(0.95f, 0.15f, 0.15f, 1f); // Đỏ báo động

        // Vẽ vạch máu
        GUI.color = healthColor;
        GUI.DrawTexture(new Rect(x, y, targetFill * width, height), Texture2D.whiteTexture);

        // 4. Khối biểu tượng huy hiệu [PLAYER] ở góc trái
        GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 13
        };
        badgeStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(x + 11, y + 1, width, height), $"❤️ PLAYER  {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}", badgeStyle);
        badgeStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 10, y, width, height), $"❤️ PLAYER  {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}", badgeStyle);

        // 5. Phần trăm máu ở bên phải
        GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
        percentStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, width - 12, height), $"{Mathf.CeilToInt(targetFill * 100f)}%", percentStyle);

        GUI.color = Color.white;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || isInvulnerable) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateHealthUI(false);

        if (IsDead)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
            if (animator != null && HasParameter("Hit")) animator.SetTrigger("Hit");
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthUI(false);
    }

    void Die()
    {
        if (animator != null && HasParameter("Die")) animator.SetTrigger("Die");

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        Debug.Log("💀 Người chơi đã hy sinh!");
    }

    void UpdateHealthUI(bool instant)
    {
        if (healthFillBar != null && instant)
        {
            displayedFill = currentHealth / maxHealth;
            healthFillBar.fillAmount = displayedFill;
        }

        if (healthText != null)
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
    }

    IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerableDuration);
        isInvulnerable = false;
    }

    bool HasParameter(string paramName)
    {
        if (animator == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    void SetupCanvasUI()
    {
        // Tìm Canvas có sẵn trong scene
        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        if (existingCanvas != null)
        {
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
