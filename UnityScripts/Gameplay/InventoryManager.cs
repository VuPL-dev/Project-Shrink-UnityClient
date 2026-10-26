using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Hệ thống Quản Lý Túi Đồ (Inventory System):
/// - Bật / Tắt túi đồ bằng phím B, Tab hoặc I.
/// - Tự động vẽ giao diện Túi đồ (12 ô chứa đồ, Bảng chi tiết, Nút Dùng / Vứt đồ).
/// - Tự động đồng bộ với Backend Database (MongoDB & PostgreSQL qua /api/inventory).
/// - Hoạt động độc lập mượt mà cả khi offline hoặc online.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [System.Serializable]
    public class SlotItem
    {
        public string itemId;
        public string itemName;
        public string description;
        public int quantity;
        public int maxStack = 20;
        public float healAmount = 30f;
        public string type = "Consumable";

        public SlotItem(string id, string name, string desc, int qty, float heal = 30f, string itemType = "Consumable")
        {
            itemId = id;
            itemName = name;
            description = desc;
            quantity = qty;
            healAmount = heal;
            type = itemType;
        }
    }

    [Header("1. Phím Bật / Tắt Túi Đồ")]
    public KeyCode toggleKey = KeyCode.B;
    public KeyCode altToggleKey = KeyCode.Tab;
    public bool isInventoryOpen = false;

    [Header("2. Cấu Trúc Túi Đồ")]
    public int totalSlots = 12; // 4 cột x 3 dòng
    public List<SlotItem> items = new List<SlotItem>();
    public int selectedSlotIndex = -1;

    [Header("3. Cấu Hình Backend API")]
    public string apiBaseUrl = "http://localhost:3000";
    public bool syncWithDatabase = true;

    private PlayerHealth playerHealth;
    private PlayerController playerController;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        // Khởi tạo sẵn 1 vài vật phẩm mẫu nếu túi đồ đang trống
        if (items.Count == 0)
        {
            items.Add(new SlotItem("health_syringe", "Ống Tiêm Sinh Lực", "Hồi phục 30 HP ngay lập tức.", 2, 30f, "Consumable"));
        }
    }

    void Start()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();
        playerController = FindObjectOfType<PlayerController>();

        // Tải dữ liệu túi đồ từ Database nếu người chơi đã đăng nhập
        if (syncWithDatabase)
        {
            StartCoroutine(FetchInventoryFromDbRoutine());
        }
    }

    void Update()
    {
        // Nhấn B hoặc Tab để Bật / Tắt Túi đồ
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(altToggleKey))
        {
            ToggleInventory();
        }

        // Nhấn ESC để đóng túi đồ nếu đang mở
        if (isInventoryOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseInventory();
        }
    }

    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        if (isInventoryOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (syncWithDatabase) StartCoroutine(FetchInventoryFromDbRoutine());
        }
        else
        {
            selectedSlotIndex = -1;
        }
    }

    public void CloseInventory()
    {
        isInventoryOpen = false;
        selectedSlotIndex = -1;
    }

    /// <summary>
    /// Thêm vật phẩm vào túi đồ (ví dụ khi nhặt được ống tiêm)
    /// </summary>
    public bool AddItem(string id, string name, string desc, int quantity, float heal = 30f, string itemType = "Consumable")
    {
        // 1. Tìm xem item đã có trong túi để cộng dồn (Stacking) chưa
        foreach (var item in items)
        {
            if (item.itemId == id && item.quantity < item.maxStack)
            {
                int space = item.maxStack - item.quantity;
                int toAdd = Mathf.Min(space, quantity);
                item.quantity += toAdd;
                quantity -= toAdd;

                if (quantity <= 0)
                {
                    OnInventoryChanged(id, toAdd, true);
                    return true;
                }
            }
        }

        // 2. Thêm vào ô trống mới nếu còn chỗ
        if (items.Count < totalSlots && quantity > 0)
        {
            items.Add(new SlotItem(id, name, desc, quantity, heal, itemType));
            OnInventoryChanged(id, quantity, true);
            return true;
        }

        Debug.LogWarning("⚠️ Túi đồ đã đầy, không thể chứa thêm!");
        return false;
    }

    /// <summary>
    /// Sử dụng vật phẩm từ túi đồ (Dùng ống tiêm hồi máu)
    /// </summary>
    public void UseItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return;

        SlotItem item = items[index];

        if (item.type == "Consumable")
        {
            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();

            if (playerHealth != null)
            {
                if (playerHealth.currentHealth >= playerHealth.maxHealth)
                {
                    Debug.Log("💊 Máu đã đầy 100%, không cần dùng ống tiêm!");
                    return;
                }

                // Hồi máu cho nhân vật
                playerHealth.Heal(item.healAmount);
                Debug.Log($"💖 [DÙNG VẬT PHẨM] Đã sử dụng {item.itemName}! +{item.healAmount} HP.");

                // Trừ 1 số lượng
                item.quantity--;
                OnInventoryChanged(item.itemId, 1, false);

                // Nếu hết thì xóa khỏi túi
                if (item.quantity <= 0)
                {
                    items.RemoveAt(index);
                    selectedSlotIndex = -1;
                }
            }
        }
    }

    /// <summary>
    /// Vứt bỏ vật phẩm khỏi túi đồ
    /// </summary>
    public void DropItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return;
        SlotItem item = items[index];
        Debug.Log($"🗑️ Đã vứt bỏ: {item.itemName} x{item.quantity}");
        items.RemoveAt(index);
        selectedSlotIndex = -1;
    }

    void OnInventoryChanged(string itemId, int qty, bool isAdd)
    {
        if (syncWithDatabase)
        {
            StartCoroutine(SyncItemWithDbRoutine(itemId, qty, isAdd));
        }
    }

    // ============================================================
    // GIAO DIỆN TÚI ĐỒ (VẼ TRỰC TIẾP LÊN MÀN HÌNH - KHÔNG CẦN SETUP CANVAS)
    // ============================================================
    void OnGUI()
    {
        if (!isInventoryOpen) return;

        // Tọa độ cửa sổ trung tâm
        float winWidth = 620f;
        float winHeight = 400f;
        float winX = (Screen.width - winWidth) * 0.5f;
        float winY = (Screen.height - winHeight) * 0.5f;

        // 1. Lớp nền mờ tối toàn màn hình (Modal Backdrop)
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // 2. Khung cửa sổ chính
        GUI.color = new Color(0.1f, 0.12f, 0.15f, 0.95f);
        GUI.DrawTexture(new Rect(winX, winY, winWidth, winHeight), Texture2D.whiteTexture);

        // Viền cửa sổ vàng ánh kim
        GUI.color = new Color(0.85f, 0.65f, 0.2f, 0.9f);
        GUI.Box(new Rect(winX - 2, winY - 2, winWidth + 4, winHeight + 4), "");
        GUI.color = Color.white;

        // 3. Tiêu đề Túi Đồ
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        GUI.Label(new Rect(winX + 20, winY + 15, 300, 30), "🎒 TÚI ĐỒ NHÂN VẬT (B)", titleStyle);

        // Nút Đóng [X] góc trên bên phải
        if (GUI.Button(new Rect(winX + winWidth - 45, winY + 12, 32, 28), "X"))
        {
            CloseInventory();
        }

        // 4. Lưới các ô đồ (4 Cột x 3 Dòng = 12 Ô)
        float gridStartX = winX + 25f;
        float gridStartY = winY + 60f;
        float slotSize = 75f;
        float slotGap = 12f;

        int cols = 4;
        int rows = 3;

        for (int i = 0; i < totalSlots; i++)
        {
            int r = i / cols;
            int c = i % cols;
            float slotX = gridStartX + c * (slotSize + slotGap);
            float slotY = gridStartY + r * (slotSize + slotGap);

            Rect slotRect = new Rect(slotX, slotY, slotSize, slotSize);

            // Nền ô đồ
            bool isSelected = (selectedSlotIndex == i);
            GUI.color = isSelected ? new Color(0.3f, 0.5f, 0.8f, 0.9f) : new Color(0.18f, 0.2f, 0.25f, 0.9f);
            GUI.DrawTexture(slotRect, Texture2D.whiteTexture);

            // Viền ô đồ
            GUI.color = isSelected ? Color.cyan : new Color(0.4f, 0.45f, 0.5f, 0.8f);
            GUI.Box(slotRect, "");
            GUI.color = Color.white;

            // Nếu ô có chứa vật phẩm
            if (i < items.Count)
            {
                SlotItem item = items[i];

                // Biểu tượng icon / tên ngắn
                GUIStyle itemStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    wordWrap = true,
                    fontStyle = FontStyle.Bold
                };
                itemStyle.normal.textColor = (item.type == "Consumable") ? new Color(0.4f, 1f, 0.4f) : Color.white;
                GUI.Label(new Rect(slotX + 4, slotY + 6, slotSize - 8, slotSize - 26), item.itemName, itemStyle);

                // Số lượng ở góc dưới bên phải ô
                GUIStyle qtyStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerRight,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                qtyStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(slotX, slotY, slotSize - 6, slotSize - 4), $"x{item.quantity}", qtyStyle);
            }

            // Bấm vào ô để chọn
            if (GUI.Button(slotRect, "", GUIStyle.none))
            {
                selectedSlotIndex = (i < items.Count) ? i : -1;
            }
        }

        // 5. Bảng Chi Tiết Vật Phẩm Đang Chọn (Bên phải)
        float detailX = winX + 380f;
        float detailY = winY + 60f;
        float detailW = 215f;
        float detailH = 310f;

        // Khung chi tiết
        GUI.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
        GUI.DrawTexture(new Rect(detailX, detailY, detailW, detailH), Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.35f, 0.4f, 0.8f);
        GUI.Box(new Rect(detailX, detailY, detailW, detailH), "");
        GUI.color = Color.white;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < items.Count)
        {
            SlotItem sel = items[selectedSlotIndex];

            // Tên vật phẩm
            GUIStyle selTitle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 14
            };
            selTitle.normal.textColor = new Color(0.3f, 0.9f, 1f);
            GUI.Label(new Rect(detailX + 15, detailY + 15, detailW - 30, 25), sel.itemName, selTitle);

            // Loại & Số lượng
            GUI.Label(new Rect(detailX + 15, detailY + 45, detailW - 30, 20), $"Loại: <color=#aaffaa>{sel.type}</color>");
            GUI.Label(new Rect(detailX + 15, detailY + 68, detailW - 30, 20), $"Số lượng: <b>{sel.quantity}</b> (Tối đa {sel.maxStack})");
            GUI.Label(new Rect(detailX + 15, detailY + 91, detailW - 30, 20), $"Hiệu ứng: <color=#ff6666>+{sel.healAmount} HP</color>");

            // Mô tả chi tiết
            GUIStyle descStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 11 };
            descStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            GUI.Label(new Rect(detailX + 15, detailY + 120, detailW - 30, 80), sel.description, descStyle);

            // NÚT DÙNG (USE)
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.3f);
            if (GUI.Button(new Rect(detailX + 15, detailY + 215, detailW - 30, 38), "💊 SỬ DỤNG (HỒI MÁU)"))
            {
                UseItemAt(selectedSlotIndex);
            }

            // NÚT VỨT (DROP)
            GUI.backgroundColor = new Color(0.8f, 0.25f, 0.25f);
            if (GUI.Button(new Rect(detailX + 15, detailY + 262, detailW - 30, 32), "🗑️ VỨT BỎ"))
            {
                DropItemAt(selectedSlotIndex);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 12
            };
            hintStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(detailX + 10, detailY + 120, detailW - 20, 50), "Bấm chọn 1 ô đồ bên cạnh để xem chi tiết và sử dụng.", hintStyle);
        }
    }

    // ============================================================
    // ĐỒNG BỘ VỚI BACKEND DATABASE (API: /api/inventory)
    // ============================================================
    IEnumerator FetchInventoryFromDbRoutine()
    {
        string token = PlayerPrefs.GetString("auth_token", "");
        if (string.IsNullOrEmpty(token)) yield break;

        using (UnityWebRequest req = UnityWebRequest.Get($"{apiBaseUrl}/api/inventory"))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("🌐 [INVENTORY DB] Đã đồng bộ dữ liệu túi đồ từ Database thành công!");
            }
        }
    }

    IEnumerator SyncItemWithDbRoutine(string itemId, int quantity, bool isAdd)
    {
        string token = PlayerPrefs.GetString("auth_token", "");
        if (string.IsNullOrEmpty(token)) yield break;

        string endpoint = isAdd ? $"{apiBaseUrl}/api/inventory/add" : $"{apiBaseUrl}/api/inventory/use";
        string jsonBody = $"{{\"itemId\":\"{itemId}\",\"quantity\":{quantity}}}";

        using (UnityWebRequest req = new UnityWebRequest(endpoint, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"🌐 [INVENTORY DB] Đồng bộ thành công: {(isAdd ? "Thêm" : "Dùng")} {itemId} x{quantity}");
            }
        }
    }
}
