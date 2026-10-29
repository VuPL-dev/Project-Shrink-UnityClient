using UnityEngine;

/// <summary>
/// Hệ thống Nhặt Vật Phẩm trên bản đồ (Hỗ trợ Ống Máu, Mạng Nhện, Gai Độc, Xương Dài - Tối ưu 0 GC Allocation):
/// - Tự động nhận diện vật phẩm gần người chơi nhất khi lại gần.
/// - Hiển thị bảng tùy chọn trực quan [✔ NHẶT (Phím E / F)] hoặc [✖ BỎ QUA (Phím Q)] kèm Icon 2D sắc nét.
/// - Tự động dựng mô hình 3D trực quan cho các nguyên liệu chế tạo rơi ra từ quái vật.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    public enum ItemType
    {
        Health,
        Ammo,
        Key,
        SpiderSilk,
        PoisonThorn,
        LongBone
    }

    [Header("1. Loại Vật Phẩm & Chỉ Số")]
    public ItemType itemType = ItemType.Health;
    public string customItemId = "";
    public float healAmount = 30f;

    [Header("2. Tùy Chọn Tương Tác Nhặt Đồ")]
    public bool requireConfirmation = true;
    public float pickupRange = 0.45f;

    [Header("3. Hiệu Ứng Trôi & Xoay (Floating / Bobbing)")]
    public bool enableFloating = true;
    public float floatAmplitude = 0.008f;
    public float floatSpeed = 3f;
    public float rotationSpeed = 75f;
    public bool preserveCustomScale = false;
    public Vector3 targetItemScale = new Vector3(0.035f, 0.035f, 0.035f);

    public static ItemPickup ActivePromptItem { get; private set; }
    public static Rect CurrentPromptRect { get; private set; }

    private static string lastPickupToastMessage = "";
    private static float lastPickupToastTime = -10f;

    private Vector3 basePosition;
    private bool isPickedUp = false;
    private bool declinedDuringCurrentApproach = false;
    private Transform playerTransform;
    private Camera cachedMainCamera;

    // Cache GUIStyle chống tạo rác bộ nhớ mỗi frame
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_subStyle;
    private static GUIStyle s_btnStyle;
    private static GUIStyle s_toastStyle;

    private static void EnsureStylesInitialized()
    {
        if (s_titleStyle != null) return;

        s_titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 17
        };
        s_titleStyle.normal.textColor = new Color(0.35f, 1f, 0.68f, 1f);

        s_subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 15
        };
        s_subStyle.normal.textColor = new Color(0.92f, 0.95f, 0.98f, 1f);

        s_btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        s_btnStyle.normal.textColor = Color.white;
        s_btnStyle.hover.textColor = Color.yellow;

        s_toastStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 15
        };
        s_toastStyle.normal.textColor = new Color(0.42f, 1f, 0.68f, 1f);
    }

    void Start()
    {
        if (!preserveCustomScale && itemType == ItemType.Health)
        {
            transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);
        }
        floatAmplitude = 0.008f;
        basePosition = transform.position;
        cachedMainCamera = Camera.main;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        FindPlayerTransform();
    }

    void FindPlayerTransform()
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy) return;

        GameObject p2 = GameObject.Find("player_v2");
        if (p2 != null && p2.activeInHierarchy)
        {
            playerTransform = p2.transform;
            return;
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null && pc.gameObject.activeInHierarchy)
        {
            playerTransform = pc.transform;
            return;
        }

        PlayerHealth ph = FindAnyObjectByType<PlayerHealth>();
        if (ph != null && ph.gameObject.activeInHierarchy)
        {
            playerTransform = ph.transform;
            return;
        }
    }

    void Update()
    {
        if (isPickedUp) return;

        if (enableFloating)
        {
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);
            float newY = basePosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        if (playerTransform == null)
        {
            FindPlayerTransform();
            if (playerTransform == null) return;
        }

        float dx = transform.position.x - playerTransform.position.x;
        float dz = transform.position.z - playerTransform.position.z;
        float horizontalDistSq = dx * dx + dz * dz;
        float rangeSq = pickupRange * pickupRange;

        if (horizontalDistSq <= rangeSq)
        {
            if (!requireConfirmation)
            {
                ConfirmPickup();
                return;
            }

            if (!declinedDuringCurrentApproach)
            {
                // Tự động ưu tiên hiển thị hộp thoại của vật phẩm nằm gần nhân vật hơn
                bool canTakePrompt = (ActivePromptItem == null || ActivePromptItem == this || ActivePromptItem.isPickedUp);
                if (!canTakePrompt && ActivePromptItem != null)
                {
                    float otherDx = ActivePromptItem.transform.position.x - playerTransform.position.x;
                    float otherDz = ActivePromptItem.transform.position.z - playerTransform.position.z;
                    float otherDistSq = otherDx * otherDx + otherDz * otherDz;
                    if (horizontalDistSq + 0.0004f < otherDistSq)
                    {
                        canTakePrompt = true;
                    }
                }

                if (canTakePrompt)
                {
                    ActivePromptItem = this;

                    // Hỗ trợ phím E hoặc F để nhặt
                    if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
                    {
                        ConfirmPickup();
                    }
                    else if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.N))
                    {
                        DeclinePickup();
                    }
                }
            }
        }
        else if (horizontalDistSq > rangeSq * 1.44f)
        {
            declinedDuringCurrentApproach = false;
            if (ActivePromptItem == this)
            {
                ActivePromptItem = null;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;
        if (other.CompareTag("Player") || other.name.ToLower().Contains("player"))
        {
            playerTransform = other.transform;
            if (!requireConfirmation)
            {
                ConfirmPickup();
            }
            else
            {
                ActivePromptItem = this;
            }
        }
    }

    public string GetItemId()
    {
        if (!string.IsNullOrEmpty(customItemId)) return customItemId;
        switch (itemType)
        {
            case ItemType.SpiderSilk: return "spider_silk";
            case ItemType.PoisonThorn: return "poison_thorn";
            case ItemType.LongBone: return "long_bone";
            default: return "health_syringe";
        }
    }

    public string GetDisplayTitle()
    {
        if (!string.IsNullOrEmpty(customItemId))
        {
            string n, d, t; float h;
            InventoryManager.GetItemDefinition(customItemId, out n, out d, out h, out t);
            return $"{n.ToUpper()} ({t})";
        }
        return GetDisplayTitleLegacy();
    }

    private string GetDisplayTitleLegacy()
    {
        switch (itemType)
        {
            case ItemType.SpiderSilk: return "MẠNG NHỆN (Nguyên Liệu Chế Tạo)";
            case ItemType.PoisonThorn: return "GAI ĐỘC (Nguyên Liệu Chế Tạo)";
            case ItemType.LongBone: return "XƯƠNG DÀI (Nguyên Liệu Chế Tạo)";
            default: return "ỐNG TIÊM SINH LỰC (+30 HP)";
        }
    }

    public string GetItemShortName()
    {
        if (!string.IsNullOrEmpty(customItemId))
        {
            string n, d, t; float h;
            InventoryManager.GetItemDefinition(customItemId, out n, out d, out h, out t);
            return n;
        }
        return GetItemShortNameLegacy();
    }

    private string GetItemShortNameLegacy()
    {
        switch (itemType)
        {
            case ItemType.SpiderSilk: return "Mạng Nhện";
            case ItemType.PoisonThorn: return "Gai Độc";
            case ItemType.LongBone: return "Xương Dài";
            default: return "Ống Tiêm Sinh Lực";
        }
    }

    public string GetItemDescription()
    {
        if (!string.IsNullOrEmpty(customItemId))
        {
            string n, d, t; float h;
            InventoryManager.GetItemDefinition(customItemId, out n, out d, out h, out t);
            return d;
        }
        return GetItemDescriptionLegacy();
    }

    private string GetItemDescriptionLegacy()
    {
        switch (itemType)
        {
            case ItemType.SpiderSilk:
                return "Tơ nhện siêu bền kết tinh từ Bọ Cánh Cứng Độc. Dùng làm dây buộc đầu giáo trên bàn Chế Tạo (Phím C).";
            case ItemType.PoisonThorn:
                return "Chiếc gai nhọn chứa nọc độc thần kinh cực mạnh. Nguyên liệu làm mũi Giáo Gai Độc (Phím C).";
            case ItemType.LongBone:
                return "Khúc xương cứng cáp rơi ra từ Kiến Khổng Lồ. Dùng làm cán cầm chắc chắn cho Giáo Gai Độc (Phím C).";
            default:
                return "Ống tiêm chứa dung dịch nano sinh học, hồi phục 30 HP tức thì cho cơ thể thu nhỏ.";
        }
    }

    public void ConfirmPickup()
    {
        if (isPickedUp) return;

        PlayerHealth playerHealth = playerTransform != null ? playerTransform.GetComponent<PlayerHealth>() : FindAnyObjectByType<PlayerHealth>();

        string id = GetItemId();
        string itemName = GetItemShortName();
        string desc = GetItemDescription();
        string category = (itemType == ItemType.Health) ? "Consumable" : "Material";
        float heal = (itemType == ItemType.Health) ? healAmount : 0f;

        if (InventoryManager.Instance != null)
        {
            bool added = InventoryManager.Instance.AddItem(id, itemName, desc, 1, heal, category);
            if (added)
            {
                HideAndScheduleDestroy();
                if (itemType == ItemType.Health)
                {
                    ShowToast("🎒 Đã nhặt [Ống Tiêm Sinh Lực] vào Túi đồ! (Bấm B để mở Túi, H để dùng nhanh)");
                }
                else
                {
                    ShowToast($"🎒 Đã nhặt nguyên liệu [{itemName}]! (Bấm Phím C để mở Bàn Chế Tạo Giáo Gai Độc)");
                }
                return;
            }
        }

        if (itemType == ItemType.Health && playerHealth != null)
        {
            HideAndScheduleDestroy();
            playerHealth.Heal(healAmount);
            ShowToast($"💊 Đã sử dụng ngay Ống Tiêm Sinh Lực! (+{Mathf.CeilToInt(healAmount)} HP)");
        }
        else
        {
            HideAndScheduleDestroy();
        }
    }

    private void HideAndScheduleDestroy()
    {
        isPickedUp = true;
        if (ActivePromptItem == this) ActivePromptItem = null;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, 2.8f);
    }

    public void DeclinePickup()
    {
        declinedDuringCurrentApproach = true;
        if (ActivePromptItem == this)
        {
            ActivePromptItem = null;
        }
        ShowToast($"Đã bỏ qua {GetItemShortName()}. (Đi ra xa rồi quay lại nếu muốn nhặt)");
    }

    public static void ShowToast(string msg)
    {
        lastPickupToastMessage = msg;
        lastPickupToastTime = Time.time;
    }

    void OnDestroy()
    {
        if (ActivePromptItem == this)
        {
            ActivePromptItem = null;
        }
    }

    void OnGUI()
    {
        bool showToast = (Time.time - lastPickupToastTime) < 2.8f && !string.IsNullOrEmpty(lastPickupToastMessage);
        bool showPrompt = (ActivePromptItem == this && !isPickedUp);

        if (!showToast && !showPrompt) return;

        EnsureStylesInitialized();

        float uiScale = Mathf.Max(1f, Screen.height / 540f);
        float virtualW = Screen.width / uiScale;
        float virtualH = Screen.height / uiScale;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        if (showToast && Event.current.type == EventType.Repaint)
        {
            DrawPickupToast(virtualW);
        }

        if (!showPrompt)
        {
            GUI.matrix = prevMatrix;
            return;
        }

        if (cachedMainCamera == null) cachedMainCamera = Camera.main;
        float boxW = 445f;
        float boxH = 152f;
        float boxX = (virtualW - boxW) * 0.5f;
        float boxY = virtualH * 0.54f;

        if (cachedMainCamera != null)
        {
            Vector3 screenPos = cachedMainCamera.WorldToScreenPoint(transform.position + Vector3.up * 0.10f);
            if (screenPos.z > 0f)
            {
                boxX = Mathf.Clamp((screenPos.x / uiScale) - boxW * 0.5f, 16f, virtualW - boxW - 16f);
                boxY = Mathf.Clamp(((Screen.height - screenPos.y) / uiScale) - boxH - 14f, 64f, virtualH - boxH - 24f);
            }
        }

        CurrentPromptRect = new Rect(boxX * uiScale, boxY * uiScale, boxW * uiScale, boxH * uiScale);
        Rect virtualPromptRect = new Rect(boxX, boxY, boxW, boxH);

        Color borderCol = itemType == ItemType.Health
            ? new Color(0.18f, 0.92f, 0.58f, 0.96f)
            : new Color(0.95f, 0.78f, 0.25f, 0.96f);

        GUI.color = borderCol;
        GUI.DrawTexture(new Rect(boxX - 2, boxY - 2, boxW + 4, boxH + 4), Texture2D.whiteTexture);

        GUI.color = new Color(0.06f, 0.09f, 0.13f, 0.96f);
        GUI.DrawTexture(virtualPromptRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        Texture2D iconTex = InventoryManager.GetItemIconTexture(GetItemId());
        if (iconTex != null)
        {
            GUI.DrawTexture(new Rect(boxX + 14f, boxY + 12f, 64f, 64f), iconTex, ScaleMode.ScaleToFit, true);
        }

        s_titleStyle.normal.textColor = itemType == ItemType.Health
            ? new Color(0.35f, 1f, 0.68f, 1f)
            : new Color(1f, 0.88f, 0.35f, 1f);

        GUI.Label(new Rect(boxX + 86f, boxY + 14f, boxW - 96f, 28f), GetDisplayTitle(), s_titleStyle);
        GUI.Label(new Rect(boxX + 86f, boxY + 44f, boxW - 96f, 26f), "Bạn có muốn nhặt vào Túi Đồ không?", s_subStyle);

        float btnW = 196f;
        float btnH = 50f;
        float btnY = boxY + 86f;
        Rect yesRect = new Rect(boxX + 16f, btnY, btnW, btnH);
        Rect noRect = new Rect(boxX + boxW - btnW - 16f, btnY, btnW, btnH);

        Color prevBg = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.14f, 0.82f, 0.40f, 1f);
        if (GUI.Button(yesRect, "✔ NHẶT [Phím E / F]", s_btnStyle))
        {
            ConfirmPickup();
        }

        GUI.backgroundColor = new Color(0.80f, 0.24f, 0.24f, 1f);
        if (GUI.Button(noRect, "✖ KHÔNG [Phím Q]", s_btnStyle))
        {
            DeclinePickup();
        }

        GUI.backgroundColor = prevBg;
        GUI.matrix = prevMatrix;
    }

    void DrawPickupToast(float virtualW)
    {
        float w = 640f;
        float h = 40f;
        float x = (virtualW - w) * 0.5f;
        float y = 64f;

        GUI.color = new Color(0.05f, 0.14f, 0.09f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, w, h), lastPickupToastMessage, s_toastStyle);
    }

    // =========================================================================
    // TẠO VẬT PHẨM RƠI RA TỪ QUÁI VẬT (DÙNG ASSET 3D MỚI TỪ ItemModelFactory)
    // =========================================================================
    public static GameObject CreateWorldDropById(string itemId, Vector3 worldPos)
    {
        GameObject root = new GameObject($"Dropped_{itemId}");
        root.transform.position = worldPos;
        root.transform.localScale = Vector3.one;

        GameObject visual = ItemModelFactory.Build(itemId);
        if (visual != null)
        {
            Vector3 restEuler;
            float worldSize;
            ItemModelFactory.GetWorldPose(itemId, out restEuler, out worldSize);

            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localRotation = Quaternion.Euler(restEuler);
            visual.transform.localScale = Vector3.one * worldSize;
            visual.transform.localPosition = new Vector3(0f, worldSize * 0.28f, 0f);
        }

        SphereCollider sc = root.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.12f;

        ItemPickup pickup = root.AddComponent<ItemPickup>();
        pickup.customItemId = itemId;
        pickup.preserveCustomScale = true;
        pickup.targetItemScale = Vector3.one;
        pickup.pickupRange = 0.45f;
        pickup.enableFloating = true;
        pickup.floatAmplitude = 0.008f;
        pickup.rotationSpeed = 70f;
        pickup.requireConfirmation = true;
        return root;
    }

    public static GameObject CreateWorldDropItem(ItemType type, Vector3 worldPos)
    {
        string modelId = ItemModelFactory.IdSpiderSilk;
        if (type == ItemType.PoisonThorn) modelId = ItemModelFactory.IdPoisonThorn;
        else if (type == ItemType.LongBone) modelId = ItemModelFactory.IdLongBone;

        GameObject root = new GameObject($"Dropped_{type}");
        root.transform.position = worldPos;
        root.transform.localScale = Vector3.one;

        GameObject visual = ItemModelFactory.Build(modelId);
        if (visual != null)
        {
            Vector3 restEuler;
            float worldSize;
            ItemModelFactory.GetWorldPose(modelId, out restEuler, out worldSize);

            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localRotation = Quaternion.Euler(restEuler);
            visual.transform.localScale = Vector3.one * worldSize;
            visual.transform.localPosition = new Vector3(0f, worldSize * 0.28f, 0f);
        }

        SphereCollider sc = root.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.12f;

        ItemPickup pickup = root.AddComponent<ItemPickup>();
        pickup.itemType = type;
        pickup.preserveCustomScale = true;
        pickup.targetItemScale = Vector3.one;
        pickup.pickupRange = 0.45f;
        pickup.enableFloating = true;
        pickup.floatAmplitude = 0.008f;
        pickup.rotationSpeed = 70f;
        pickup.requireConfirmation = true;

        return root;
    }
}
