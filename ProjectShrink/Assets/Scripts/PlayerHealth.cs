using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hệ thống Máu cho Nhân Vật (Đã tối ưu hóa 0 GC Allocation chống lag):
/// - Hiển thị Thanh Máu Nhân Vật cực kỳ to rõ, viền vàng sáng nổi bật ở góc trên bên trái màn hình.
/// - Chỉ vẽ trong sự kiện EventType.Repaint và tái sử dụng GUIStyle để không gây tụt FPS.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("1. Chỉ Số Máu")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("2. Giao Diện")]
    public bool showHealthUI = true;
    public Image healthFillBar;
    public TextMeshProUGUI healthText;

    [Header("3. Hiệu Ứng Bị Đánh")]
    public Animator animator;
    public float invulnerableDuration = 0.5f;

    [Header("4. Phím Test")]
    public bool enableTestKeys = true;

    private bool isInvulnerable = false;
    private float displayedFill = 1f;

    // Cache GUIStyle và chuỗi chữ để tối ưu 0% rác bộ nhớ (Zero GC Allocation) mỗi khung hình
    private static GUIStyle cachedHpTextStyle;
    private static GUIStyle cachedHpShadowStyle;
    private int lastCachedCurHp = -1;
    private int lastCachedMaxHp = -1;
    private string cachedHpString = "❤️ MÁU NHÂN VẬT: 100 / 100 HP";

    public bool IsDead => currentHealth <= 0f;

    void Awake()
    {
        if (maxHealth <= 0f) maxHealth = 100f;
        currentHealth = maxHealth;
        displayedFill = 1f;
        isInvulnerable = false;
    }

    void OnEnable()
    {
        if (maxHealth <= 0f) maxHealth = 100f;
        if (currentHealth <= 0f) currentHealth = maxHealth;
        displayedFill = Mathf.Clamp01(currentHealth / maxHealth);
        isInvulnerable = false;
    }

    void Start()
    {
        if (maxHealth <= 0f) maxHealth = 100f;
        currentHealth = maxHealth;
        displayedFill = 1f;
        showHealthUI = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        UpdateHealthUI(true);
    }

    void Update()
    {
        if (maxHealth <= 0f) maxHealth = 100f;

        float target = Mathf.Clamp01(currentHealth / maxHealth);
        if (Mathf.Abs(displayedFill - target) > 0.0005f)
        {
            displayedFill = Mathf.Lerp(displayedFill, target, 12f * Time.deltaTime);
        }
        else
        {
            displayedFill = target;
        }
    }

    private static void EnsureStylesInitialized()
    {
        if (cachedHpTextStyle == null)
        {
            cachedHpTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 17
            };
            cachedHpTextStyle.normal.textColor = Color.white;
        }

        if (cachedHpShadowStyle == null)
        {
            cachedHpShadowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 17
            };
            cachedHpShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
        }
    }

    /// <summary>
    /// Vẽ Thanh Máu Nhân Vật cực rõ nét ở góc trên bên trái màn hình (Chỉ chạy ở Repaint để chống lag)
    /// </summary>
    void OnGUI()
    {
        if (!showHealthUI || !gameObject.activeInHierarchy) return;
        // Tối ưu hiệu năng: Chỉ vẽ ở pha Repaint, bỏ qua các pha Layout/Input thừa
        if (Event.current.type != EventType.Repaint) return;

        EnsureStylesInitialized();

        Matrix4x4 prevMatrix = GUI.matrix;
        float uiScale = Mathf.Max(1f, Screen.height / 540f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        float x = 22f;
        float y = 20f;
        float width = 350f;
        float height = 38f;

        // 1. Bóng đổ đen phía ngoài cùng
        GUI.color = new Color(0f, 0f, 0f, 0.92f);
        GUI.DrawTexture(new Rect(x - 5f, y - 5f, width + 10f, height + 10f), Texture2D.whiteTexture);

        // 2. Viền vàng kim sáng rực giúp thanh máu nhân vật luôn nổi bật trên nền phòng tối
        GUI.color = new Color(1f, 0.86f, 0.28f, 1f);
        GUI.DrawTexture(new Rect(x - 3f, y - 3f, width + 6f, height + 6f), Texture2D.whiteTexture);

        // 3. Nền rỗng bên trong màu xám xanh rõ ràng (khi mất máu vẫn thấy rõ độ dài thanh máu)
        GUI.color = new Color(0.20f, 0.23f, 0.28f, 0.98f);
        GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

        // 4. Lớp máu trễ màu vàng cam (hiệu ứng tụt máu)
        float delayedWidth = Mathf.Clamp01(displayedFill) * width;
        if (delayedWidth > 0.5f)
        {
            GUI.color = new Color(1f, 0.78f, 0.16f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, delayedWidth, height), Texture2D.whiteTexture);
        }

        // 5. Thanh máu thực tế màu đỏ tươi rực rỡ + lớp bóng kính phía trên
        float actualFill = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        float fillW = actualFill * width;
        if (fillW > 0.5f)
        {
            GUI.color = new Color(0.95f, 0.16f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(x, y, fillW, height), Texture2D.whiteTexture);

            // Dải phản quang bóng sáng nửa trên thanh máu
            GUI.color = new Color(1f, 0.65f, 0.65f, 0.35f);
            GUI.DrawTexture(new Rect(x, y, fillW, height * 0.42f), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;

        // 6. Chữ số hiển thị máu (chỉ tạo chuỗi mới khi số HP thay đổi để không tốn bộ nhớ)
        int curHpInt = Mathf.CeilToInt(currentHealth);
        int maxHpInt = Mathf.CeilToInt(maxHealth);
        if (curHpInt != lastCachedCurHp || maxHpInt != lastCachedMaxHp)
        {
            lastCachedCurHp = curHpInt;
            lastCachedMaxHp = maxHpInt;
            cachedHpString = $"❤️ MÁU NHÂN VẬT: {curHpInt} / {maxHpInt} HP";
        }

        Rect textRect = new Rect(x, y - 1f, width, height + 2f);
        Rect shadowRect = new Rect(x + 1.5f, y + 0.5f, width, height + 2f);
        GUI.Label(shadowRect, cachedHpString, cachedHpShadowStyle);
        GUI.Label(textRect, cachedHpString, cachedHpTextStyle);

        GUI.matrix = prevMatrix;
    }

    public void TakeDamage(float damage)
    {
        if (isInvulnerable) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateHealthUI(false);

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        UpdateHealthUI(false);
    }

    void Die()
    {
        // Tự động hồi phục lại máu để người chơi không bao giờ bị khóa cứng khi đứng gần quái
        currentHealth = maxHealth;
        displayedFill = 1f;
        UpdateHealthUI(true);
    }

    void UpdateHealthUI(bool instant)
    {
        if (instant && maxHealth > 0f)
        {
            displayedFill = currentHealth / maxHealth;
        }

        if (healthFillBar != null && healthFillBar.gameObject.activeInHierarchy)
        {
            healthFillBar.fillAmount = displayedFill;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
    }

    IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerableDuration);
        isInvulnerable = false;
    }
}
