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

        // Tự động tắt thanh Canvas thô/trùng lặp để chỉ giữ lại 1 thanh máu duy nhất, đẹp và gọn
        if (healthFillBar != null)
        {
            Transform parentBg = healthFillBar.transform.parent;
            if (parentBg != null && parentBg.name.Contains("Health"))
                parentBg.gameObject.SetActive(false);
            else
                healthFillBar.gameObject.SetActive(false);
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
    /// THANH MÁU DUY NHẤT: Thiết kế gọn gàng, tinh tế, chuẩn phong cách game phiêu lưu
    /// </summary>
    void OnGUI()
    {
        // Vị trí góc trên bên trái, gọn gàng và sang trọng
        float x = 25f;
        float y = 25f;
        float width = 230f;
        float height = 20f;

        // 1. Viền ngoài đen mờ
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);

        // 2. Nền trong tối
        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        // 3. Thanh máu màu đỏ tươi (mịn màng theo thời gian)
        float currentBarWidth = Mathf.Clamp01(displayedFill) * width;
        GUI.color = new Color(0.85f, 0.12f, 0.12f, 1f);
        GUI.DrawTexture(new Rect(x, y, currentBarWidth, height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 4. Chữ số hiển thị máu ở giữa thanh
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
        textStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y - 1, width, height), $"HP: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}", textStyle);
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
