using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Hệ thống Quản Lý Túi Đồ & Bàn Chế Tạo kiểu Minecraft (Inventory & Crafting System):
/// - Phím B / Tab: Bật / Tắt Túi Đồ.
/// - Phím C: Bật / Tắt Bàn Chế Tạo (Lưới 3x3).
/// - Góc bên phải: SÁCH CÔNG THỨC (Minecraft Recipe Book) hiển thị mẫu 3x3 và nút [⚡ Xếp Tự Động].
/// - 5 Công thức chế tạo:
///   1. Giáo Gai Độc: Gai Độc + Mạng Nhện + Xương Dài (+25 DMG, tầm đánh xa).
///   2. Đuốc Phát Sáng: Than + Cây Gậy (Phát sáng ấm áp quanh người chơi).
///   3. Thang Leo: 3 Cây Gậy / Dây Thừng (Đặt xuống đất, lại gần bấm W / Space leo lên cao).
///   4. Balo 24 Ô: Dây Thừng + Vải + Gỗ (Tăng sức chứa túi đồ từ 12 ô lên 24 ô).
///   5. Búa Đá Chiến: Đá + Dây Thừng + Cây Gậy (+18 DMG).
/// - Cung cấp sẵn nguyên liệu chế tạo ở đầu game để test thuận tiện.
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
        public int maxStack = 64;
        public float healAmount = 30f;
        public string type = "Consumable"; // "Consumable", "Material", "Weapon", "Tool"

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

    public class RecipeDefinition
    {
        public string resultId;
        public int resultQuantity;
        public string recipeName;
        public string statsDesc;
        public string shortDesc;
        public string[] pattern; // 9 phần tử đại diện cho 3x3
        public Dictionary<string, int> requiredItems;

        public RecipeDefinition(string resId, int resQty, string name, string stats, string desc, string[] pat, Dictionary<string, int> req)
        {
            resultId = resId;
            resultQuantity = resQty;
            recipeName = name;
            statsDesc = stats;
            shortDesc = desc;
            pattern = pat;
            requiredItems = req;
        }
    }

    public struct CraftResult
    {
        public string itemId;
        public int quantity;
    }

    [Header("1. Phím Bật / Tắt Túi Đồ & Chế Tạo")]
    public KeyCode toggleKey = KeyCode.B;
    public KeyCode altToggleKey = KeyCode.Tab;
    public KeyCode craftingKey = KeyCode.C;
    public bool isInventoryOpen = false;
    public bool isCraftingOpen = false;

    [Header("2. Cấu Trúc Túi Đồ")]
    public int totalSlots = 12; // Mặc định 12 ô, khi trang bị Balo tăng lên 24 ô
    public List<SlotItem> items = new List<SlotItem>();
    public int selectedSlotIndex = -1;

    [Header("3. Trạng Thái Trang Bị")]
    public bool isSpearEquipped = false;
    public float spearBonusDamage = 25f;
    [Range(0.25f, 2.5f)]
    public float spearSizeMultiplier = 1.0f;
    [Header("Tinh Chỉnh Gắn Tay Giáo Gai Độc")]
    public Vector3 spearForwardOffset = new Vector3(0.0002f, -0.0005f, 0.0008f);
    public Vector3 spearRotation = new Vector3(65f, 0f, 0f);

    public bool isHammerEquipped = false;
    public float hammerBonusDamage = 18f;
    public Vector3 hammerLocalPosition = new Vector3(-0.002f, 0.035f, 0.005f);
    public Vector3 hammerLocalRotation = new Vector3(15f, 0f, -10f);

    public bool isTorchEquipped = false;
    public Vector3 torchLocalPosition = new Vector3(0.002f, 0.035f, 0.005f);
    public Vector3 torchLocalRotation = new Vector3(15f, 0f, 10f);

    public bool isBackpackEquipped = false;
    [Range(0.25f, 2.5f)]
    public float backpackSizeMultiplier = 1.0f;
    public Vector3 backpackLocalPosition = new Vector3(0f, 0.02f, -0.15f);
    public Vector3 backpackLocalRotation = new Vector3(0f, 180f, 0f);

    [Header("4. Cấu Hình Backend API")]
    public string apiBaseUrl = "http://localhost:3000";
    public bool syncWithDatabase = true;

    private PlayerHealth playerHealth;
    private PlayerController playerController;

    private GameObject equippedSpearObject;
    private GameObject equippedHammerObject;
    private GameObject equippedTorchObject;
    private GameObject equippedBackpackObject;
    private Light torchLight;

    private Transform cachedRightHandBone;
    private Transform cachedRightHandPalmBone;
    private Transform cachedLeftHandBone;
    private Transform cachedLeftHandPalmBone;
    private Transform cachedSpineBone;
    private string lastSyncedSignature = "";

    // Lưới chế tạo 3x3 (index = hàng * 3 + cột). Rỗng = ô trống.
    private readonly string[] craftGrid = new string[9];
    private readonly Rect[] craftCellRects = new Rect[9];
    private Rect craftOutRect;
    private Vector2 inventoryScrollPos = Vector2.zero;
    private Vector2 recipeScrollPos = Vector2.zero;

    // Trạng thái kéo thả
    private string dragItemId = null;
    private int dragFromInv = -1;
    private int dragFromCell = -1;

    private static Texture2D cachedSyringeIcon;

    // Danh sách 5 Công Thức Chế Tạo
    public static readonly List<RecipeDefinition> AllRecipes = new List<RecipeDefinition>
    {
        // 1. Giáo Gai Độc
        new RecipeDefinition(
            "poison_thorn_spear", 1, "Giáo Gai Độc", "+25 Sát Thương, Tăng Tầm Đánh",
            "Cán Xương Dài buộc Mạng Nhện và mũi Gai Độc cực bén.",
            new string[] {
                null, "poison_thorn", null,
                null, "spider_silk", null,
                null, "long_bone", null
            },
            new Dictionary<string, int> { { "poison_thorn", 1 }, { "spider_silk", 1 }, { "long_bone", 1 } }
        ),

        // 2. Đuốc Phát Sáng
        new RecipeDefinition(
            "torch", 4, "Đuốc Phát Sáng", "Chiếu Sáng Ấm Áp Quanh Người",
            "Than Đen kết hợp Cây Gậy tạo thành đuốc soi sáng.",
            new string[] {
                null, "coal", null,
                null, "stick", null,
                null, null, null
            },
            new Dictionary<string, int> { { "coal", 1 }, { "stick", 1 } }
        ),

        // 3. Thang Leo
        new RecipeDefinition(
            "ladder", 2, "Thang Leo", "Đặt Xuống Đất, Leo Lên Cao",
            "Gậy và Dây Thừng bện thành thang leo lên các kệ, đồ vật cao.",
            new string[] {
                null, "stick", null,
                null, "rope", null,
                null, "stick", null
            },
            new Dictionary<string, int> { { "stick", 2 }, { "rope", 1 } }
        ),

        // 4. Balo 24 Ô
        new RecipeDefinition(
            "backpack", 1, "Balo 24 Ô", "Mở Rộng Túi Đồ Từ 12 Lên 24 Ô",
            "Dây Thừng thắt các tấm Vải bền tạo thành Balo phiêu lưu.",
            new string[] {
                null, "rope", null,
                "cloth", "wood", "cloth",
                "cloth", "cloth", "cloth"
            },
            new Dictionary<string, int> { { "rope", 1 }, { "wood", 1 }, { "cloth", 5 } }
        ),

        // 5. Búa Đá Chiến
        new RecipeDefinition(
            "stone_hammer", 1, "Búa Đá Chiến", "+18 Sát Thương Đập Mạnh",
            "Khối Đá mài sắc buộc chặt vào Cây Gậy bằng Dây Thừng.",
            new string[] {
                null, "stone", null,
                null, "rope", null,
                null, "stick", null
            },
            new Dictionary<string, int> { { "stone", 1 }, { "rope", 1 }, { "stick", 1 } }
        )
    };

    // Cache toàn bộ GUIStyle để chống tạo rác bộ nhớ (Zero GC Allocation)
    private static GUIStyle s_titleStyle;
    private static GUIStyle s_tabBtnStyle;
    private static GUIStyle s_closeBtnStyle;
    private static GUIStyle s_slotItemStyle;
    private static GUIStyle s_slotQtyStyle;
    private static GUIStyle s_selTitleStyle;
    private static GUIStyle s_statStyle;
    private static GUIStyle s_descStyle;
    private static GUIStyle s_actionBtnStyle;
    private static GUIStyle s_hintStyle;
    private static GUIStyle s_hudQtyStyle;
    private static GUIStyle s_hudNameStyle;
    private static GUIStyle s_hudKeyStyle;
    private static GUIStyle s_craftHeaderStyle;
    private static GUIStyle s_craftStatusStyle;
    private static GUIStyle s_recipeCardStyle;
    private static GUIStyle s_recipeBtnStyle;

    private static void EnsureGuiStylesInitialized()
    {
        if (s_titleStyle != null) return;

        s_titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 20 };
        s_titleStyle.normal.textColor = new Color(1f, 0.88f, 0.32f);

        s_tabBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 14, alignment = TextAnchor.MiddleCenter };
        s_tabBtnStyle.normal.textColor = Color.white;

        s_closeBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 18 };

        s_slotItemStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 11, fontStyle = FontStyle.Bold };
        s_slotItemStyle.normal.textColor = new Color(0.50f, 1f, 0.72f);

        s_slotQtyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 13, fontStyle = FontStyle.Bold };
        s_slotQtyStyle.normal.textColor = Color.yellow;

        s_selTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold, fontSize = 17, wordWrap = true };
        s_selTitleStyle.normal.textColor = new Color(0.38f, 1f, 0.72f);

        s_statStyle = new GUIStyle(GUI.skin.label) { richText = true, fontStyle = FontStyle.Bold, fontSize = 14 };
        s_statStyle.normal.textColor = Color.white;

        s_descStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13, fontStyle = FontStyle.Normal };
        s_descStyle.normal.textColor = new Color(0.92f, 0.95f, 0.98f);

        s_actionBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 14, alignment = TextAnchor.MiddleCenter };
        s_actionBtnStyle.normal.textColor = Color.white;

        s_hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold, fontSize = 14 };
        s_hintStyle.normal.textColor = new Color(0.82f, 0.88f, 0.94f);

        s_hudQtyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, fontSize = 15 };
        s_hudQtyStyle.normal.textColor = Color.yellow;

        s_hudNameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
        s_hudNameStyle.normal.textColor = new Color(0.38f, 1f, 0.68f);

        s_hudKeyStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
        s_hudKeyStyle.normal.textColor = new Color(0.92f, 0.95f, 0.98f);

        s_craftHeaderStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
        s_craftHeaderStyle.normal.textColor = new Color(1f, 0.90f, 0.38f);

        s_craftStatusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 12, wordWrap = true, richText = true };
        s_craftStatusStyle.normal.textColor = new Color(0.80f, 0.92f, 0.85f);

        s_recipeCardStyle = new GUIStyle(GUI.skin.box);
        s_recipeBtnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 12, alignment = TextAnchor.MiddleCenter };
    }

    public class ServerItemInfo
    {
        public string itemId;
        public string name;
        public string description;
        public string type;
        public float heal;
        public float damage;
    }

    public class ServerEnemyInfo
    {
        public string enemyId;
        public float hp;
        public float damage;
        public float speed;
        public float attackRange;
        public float detectionRange;
        public string dropItemId;
        public float dropRate;
    }

    public static readonly Dictionary<string, ServerItemInfo> ServerItemCache = new Dictionary<string, ServerItemInfo>();
    public static readonly Dictionary<string, ServerEnemyInfo> ServerEnemyCache = new Dictionary<string, ServerEnemyInfo>();

    // =========================================================================
    // ĐỊNH NGHĨA VẬT PHẨM & ICON
    // =========================================================================
    public static void GetItemDefinition(string id, out string name, out string desc, out float heal, out string type)
    {
        heal = 0f;
        if (!string.IsNullOrEmpty(id) && ServerItemCache.TryGetValue(id, out var sItem))
        {
            name = sItem.name;
            desc = sItem.description;
            heal = sItem.heal;
            type = sItem.type;
            return;
        }
        switch (id)
        {
            case "spider_silk":
                name = "Mạng Nhện"; type = "Material";
                desc = "Tơ nhện siêu bền kết tinh từ Bọ Cánh Cứng Độc. Nguyên liệu làm Giáo Gai Độc trên Bàn Chế Tạo (Phím C).";
                break;
            case "poison_thorn":
                name = "Gai Độc"; type = "Material";
                desc = "Chiếc gai nhọn chứa nọc độc cực mạnh. Dùng làm mũi Giáo Gai Độc (Phím C).";
                break;
            case "long_bone":
                name = "Xương Dài"; type = "Material";
                desc = "Khúc xương cứng cáp rơi ra từ Kiến Khổng Lồ. Dùng làm cán Giáo Gai Độc (Phím C).";
                break;
            case "poison_thorn_spear":
                name = "Giáo Gai Độc"; type = "Weapon";
                desc = "Vũ khí chế tạo từ Xương Dài, Mạng Nhện và Gai Độc. Tăng +25 Sát Thương mỗi đòn và mở rộng tầm đánh!";
                break;
            case "wood":
                name = "Khúc Gỗ"; type = "Material";
                desc = "Khúc gỗ tự nhiên chắc chắn. Dùng để chế tạo Balo 24 Ô và các công cụ khác.";
                break;
            case "stick":
                name = "Cây Gậy"; type = "Material";
                desc = "Cành cây thon dài dẻo dai. Dùng để chế tạo Đuốc, Thang Leo, Búa Đá.";
                break;
            case "rope":
                name = "Dây Thừng"; type = "Material";
                desc = "Bó dây thừng bện xoắn bền bỉ. Dùng để buộc Thang Leo, Balo 24 Ô, Búa Đá.";
                break;
            case "cloth":
                name = "Cuộn Vải"; type = "Material";
                desc = "Súc vải lanh mềm nhẹ nhưng bền dai. Nguyên liệu chính may Balo 24 Ô.";
                break;
            case "stone":
                name = "Khối Đá"; type = "Material";
                desc = "Khối đá mài góc cạnh cứng rắn. Nguyên liệu chế tạo Búa Đá Chiến (+18 DMG).";
                break;
            case "coal":
                name = "Than Đen"; type = "Material";
                desc = "Mẩu than cháy âm ỉ tỏa nhiệt. Dùng với Cây Gậy để chế tạo Đuốc Phát Sáng.";
                break;
            case "torch":
                name = "Đuốc Phát Sáng"; type = "Tool";
                desc = "Đuốc cầm tay tỏa ánh sáng ấm áp, xua tan bóng tối quanh nhân vật khi khám phá phòng.";
                break;
            case "ladder":
                name = "Thang Leo"; type = "Tool";
                desc = "Thang gỗ chắc chắn. Bấm Sử Dụng để đặt xuống đất, tiến lại gần bấm W / Space để leo lên cao!";
                break;
            case "backpack":
                name = "Balo 24 Ô"; type = "Tool";
                desc = "Balo da thám hiểm tiện lợi. Khi trang bị sẽ tăng dung lượng Túi Đồ từ 12 ô lên 24 ô!";
                break;
            case "stone_hammer":
                name = "Búa Đá Chiến"; type = "Weapon";
                desc = "Búa đá nặng gắn chặt vào cán gỗ. Tăng +18 Sát Thương mỗi đòn đánh uy lực!";
                break;
            default:
                name = "Ống Tiêm Sinh Lực"; type = "Consumable"; heal = 30f;
                desc = "Ống tiêm chứa dung dịch nano sinh học, hồi phục 30 HP tức thì cho cơ thể thu nhỏ.";
                break;
        }
    }

    private static readonly Dictionary<string, Texture2D> s_loadedIcons = new Dictionary<string, Texture2D>();

    public static Texture2D GetItemIconTexture(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return GetSyringeIconTexture();

        if (s_loadedIcons.TryGetValue(itemId, out var cachedTex) && cachedTex != null)
            return cachedTex;

        Texture2D resTex = Resources.Load<Texture2D>("ItemIcons/" + itemId);
        if (resTex != null)
        {
            s_loadedIcons[itemId] = resTex;
            return resTex;
        }

        if (itemId == "health_syringe") return GetSyringeIconTexture();
        Texture2D baked = ItemModelFactory.GetIcon(itemId);
        if (baked != null) return baked;
        return ItemModelFactory.GetFallbackIcon(itemId);
    }

    public static Texture2D GetSyringeIconTexture()
    {
        if (cachedSyringeIcon != null) return cachedSyringeIcon;

        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float cos45 = 0.70710678f;
        float sin45 = 0.70710678f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color col = new Color(0f, 0f, 0f, 0f);
                float dx = x - cx;
                float dy = y - cy;
                float distCenter = Mathf.Sqrt(dx * dx + dy * dy);

                if (distCenter < 56f)
                {
                    float glow = Mathf.Clamp01(1f - (distCenter / 56f));
                    col = new Color(0.06f, 0.25f, 0.18f, glow * 0.55f);
                }

                float u = dx * cos45 + dy * sin45;
                float v = -dx * sin45 + dy * cos45;
                float absV = Mathf.Abs(v);

                if (u >= 26f && u <= 50f)
                {
                    float halfWidth = Mathf.Lerp(1.6f, 0.4f, (u - 26f) / 24f);
                    if (absV <= halfWidth) col = new Color(0.85f, 0.95f, 1f, 1f);
                }
                if (u >= 19f && u < 26f && absV <= 4.8f) col = new Color(0.65f, 0.75f, 0.82f, 1f);
                if (u >= -20f && u < 19f && absV <= 13f)
                {
                    col = new Color(0.15f, 0.28f, 0.32f, 0.92f);
                    if (u >= -13f && u <= 18f && absV <= 10.2f)
                    {
                        float shade = 0.85f + 0.15f * Mathf.Cos((v / 10.2f) * 1.5f);
                        col = new Color(0.12f * shade, 0.95f * shade, 0.45f * shade, 1f);
                        if (v >= -6.5f && v <= -3.2f) col = Color.Lerp(col, new Color(0.75f, 1f, 0.85f, 1f), 0.65f);
                    }
                    if (u >= -17.5f && u < -13f && absV <= 10.5f) col = new Color(0.22f, 0.26f, 0.30f, 1f);
                    if (absV >= 10.5f) col = new Color(0.82f, 0.95f, 1f, 0.95f);
                }
                if (u >= -23.5f && u < -20f && absV <= 19f) col = new Color(0.72f, 0.80f, 0.86f, 1f);
                if (u >= -39f && u < -23.5f && absV <= 3.6f) col = new Color(0.62f, 0.70f, 0.78f, 1f);
                if (u >= -44f && u < -39f && absV <= 11.5f) col = new Color(0.20f, 0.88f, 0.48f, 1f);

                float mx = x - 27f;
                float my = y - 101f;
                if ((Mathf.Abs(mx) <= 3.2f && Mathf.Abs(my) <= 9.5f) || (Mathf.Abs(mx) <= 9.5f && Mathf.Abs(my) <= 3.2f))
                    col = new Color(0.25f, 1f, 0.55f, 1f);

                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply();
        cachedSyringeIcon = tex;
        return cachedSyringeIcon;
    }

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================
    void Awake()
    {
        Instance = this;
        isInventoryOpen = false;
        isCraftingOpen = false;
        selectedSlotIndex = -1;
        totalSlots = isBackpackEquipped ? 24 : 12;
        items.Clear();
        for (int i = 0; i < craftGrid.Length; i++) craftGrid[i] = null;
        ClearDrag();
    }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (equippedSpearObject != null) Destroy(equippedSpearObject);
        if (equippedHammerObject != null) Destroy(equippedHammerObject);
        if (equippedTorchObject != null) Destroy(equippedTorchObject);
        if (equippedBackpackObject != null) Destroy(equippedBackpackObject);
    }

    void Start()
    {
        playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? FindAnyObjectByType<PlayerHealth>();
        playerController = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>() ?? PlayerController.Instance ?? FindAnyObjectByType<PlayerController>();

        // Render sẵn Icon 2D từ chính mô hình 3D của vật phẩm (chỉ chạy 1 lần)
        ItemModelFactory.BakeAllIcons();

        // Cấp phát nguyên liệu chế tạo khởi đầu cho người chơi để test thuận tiện
        GiveStarterMaterialsForTesting();

        // Tải dữ liệu game từ server (items, recipes, chỉ số nhân vật/quái)
        // Không làm ảnh hưởng gameplay nếu server offline - dùng hard-code fallback
        if (syncWithDatabase)
        {
            StartCoroutine(LoadGameDataFromServer(false));
            StartCoroutine(LiveSyncWithBackendRoutine());
        }
    }

    IEnumerator LiveSyncWithBackendRoutine()
    {
        var wait = new WaitForSecondsRealtime(4.0f);
        while (true)
        {
            yield return wait;
            if (syncWithDatabase)
            {
                yield return StartCoroutine(LoadGameDataFromServer(true));
            }
        }
    }

    /// <summary>
    /// Cung cấp vật liệu để chế tạo ở đầu game phục vụ test theo đúng yêu cầu người dùng
    /// </summary>
    public void GiveStarterMaterialsForTesting()
    {
        AddItemById("wood", 10);
        AddItemById("stick", 12);
        AddItemById("coal", 8);
        AddItemById("rope", 8);
        AddItemById("cloth", 8);
        AddItemById("stone", 8);
        AddItemById("spider_silk", 6);
        AddItemById("poison_thorn", 6);
        AddItemById("long_bone", 6);
        AddItemById("health_syringe", 4);
    }

    void Update()
    {
        // Nhấn B, Tab để Bật / Tắt Túi Đồ
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(altToggleKey) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (isInventoryOpen && !isCraftingOpen) CloseAllWindows();
            else OpenInventoryTab();
        }

        // Nhấn C để Bật / Tắt Bàn Chế Tạo 3x3
        if (Input.GetKeyDown(craftingKey) || Input.GetKeyDown(KeyCode.C))
        {
            if (isInventoryOpen && isCraftingOpen) CloseAllWindows();
            else OpenCraftingTab();
        }

        // Phím H: Sử dụng nhanh Ống Tiêm
        if (!isInventoryOpen && items.Count > 0 && Input.GetKeyDown(KeyCode.H))
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemId == "health_syringe")
                {
                    UseItemAt(i);
                    break;
                }
            }
        }

        if (isInventoryOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseAllWindows();
        }

        // Phím F5: Đồng bộ tức thì dữ liệu từ Admin Web & Backend Server
        if (Input.GetKeyDown(KeyCode.F5))
        {
            StartCoroutine(LoadGameDataFromServer(false, true));
        }
    }

    public void EnsureBonesCached()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>() ?? PlayerController.Instance ?? FindAnyObjectByType<PlayerController>();
        if (playerController == null) return;

        if (cachedRightHandBone != null && cachedLeftHandBone != null && cachedSpineBone != null) return;

        Animator anim = playerController.GetComponentInChildren<Animator>();
        if (anim == null) anim = playerController.GetComponent<Animator>();
        if (anim == null && PlayerController.Instance != null) anim = PlayerController.Instance.GetComponentInChildren<Animator>();

        if (anim != null && anim.isHuman)
        {
            if (cachedRightHandBone == null) cachedRightHandBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (cachedRightHandPalmBone == null)
            {
                cachedRightHandPalmBone = anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                if (cachedRightHandPalmBone == null) cachedRightHandPalmBone = anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            }
            if (cachedLeftHandBone == null) cachedLeftHandBone = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (cachedLeftHandPalmBone == null)
            {
                cachedLeftHandPalmBone = anim.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                if (cachedLeftHandPalmBone == null) cachedLeftHandPalmBone = anim.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            }
            if (cachedSpineBone == null)
            {
                cachedSpineBone = anim.GetBoneTransform(HumanBodyBones.Chest);
                if (cachedSpineBone == null) cachedSpineBone = anim.GetBoneTransform(HumanBodyBones.Spine);
            }
        }

        Transform searchRoot = playerController.transform.root != null ? playerController.transform.root : playerController.transform;
        Transform[] allBones = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allBones.Length; i++)
        {
            string n = allBones[i].name.ToLowerInvariant();
            if (cachedRightHandBone == null && (n.EndsWith("righthand") || n.Contains("wrist_r") || n.Contains("wrist.r") || n.Contains("hand_r") || n.Contains("r_hand")))
            {
                cachedRightHandBone = allBones[i];
                Debug.Log($"✅ [InventoryManager] Đã gắn kết xương tay phải: {cachedRightHandBone.name}");
            }
            if (cachedRightHandPalmBone == null && (n.Contains("righthandmiddle1") || n.Contains("righthandindex1") || n.Contains("f_middle.01.r")))
            {
                cachedRightHandPalmBone = allBones[i];
            }
            if (cachedLeftHandBone == null && (n.EndsWith("lefthand") || n.Contains("wrist_l") || n.Contains("wrist.l") || n.Contains("hand_l") || n.Contains("l_hand")))
            {
                cachedLeftHandBone = allBones[i];
                Debug.Log($"✅ [InventoryManager] Đã gắn kết xương tay trái: {cachedLeftHandBone.name}");
            }
            if (cachedLeftHandPalmBone == null && (n.Contains("lefthandmiddle1") || n.Contains("lefthandindex1") || n.Contains("f_middle.01.l")))
            {
                cachedLeftHandPalmBone = allBones[i];
            }
            if (cachedSpineBone == null && (n.Contains("chest") || n.Contains("spine2") || n.Contains("spine1") || n.Contains("spine")))
            {
                cachedSpineBone = allBones[i];
                Debug.Log($"✅ [InventoryManager] Đã gắn kết xương lưng/ngực: {cachedSpineBone.name}");
            }
        }
    }

    void LateUpdate()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>() ?? PlayerController.Instance ?? FindAnyObjectByType<PlayerController>();
        if (playerController == null) return;

        EnsureBonesCached();

        // 1. Cập nhật Giáo Gai Độc nằm sát trong lòng bàn tay phải và thu nhỏ vừa vặn với nhân vật
        if (isSpearEquipped && equippedSpearObject != null && equippedSpearObject.activeSelf)
        {
            Transform anchor = cachedRightHandBone != null ? cachedRightHandBone : playerController.transform;
            Vector3 palmPos = (cachedRightHandBone != null && cachedRightHandPalmBone != null)
                ? Vector3.Lerp(cachedRightHandBone.position, cachedRightHandPalmBone.position, 0.65f)
                : anchor.position;

            Vector3 handOffset = playerController.transform.forward * spearForwardOffset.z
                               + playerController.transform.right * spearForwardOffset.x
                               + Vector3.up * spearForwardOffset.y;
            equippedSpearObject.transform.position = palmPos + handOffset;
            equippedSpearObject.transform.rotation = playerController.transform.rotation * Quaternion.Euler(spearRotation);

            Transform spearModel = equippedSpearObject.transform.Find("SpearModel");
            if (spearModel != null)
            {
                spearModel.localScale = new Vector3(0.042f, 0.056f, 0.042f) * spearSizeMultiplier;
            }
        }

        // 2. Cập nhật Búa Đá trên lòng bàn tay phải
        if (isHammerEquipped && equippedHammerObject != null && equippedHammerObject.activeSelf)
        {
            Transform anchor = cachedRightHandBone != null ? cachedRightHandBone : playerController.transform;
            Vector3 palmPos = (cachedRightHandBone != null && cachedRightHandPalmBone != null)
                ? Vector3.Lerp(cachedRightHandBone.position, cachedRightHandPalmBone.position, 0.65f)
                : anchor.position;
            equippedHammerObject.transform.position = palmPos + playerController.transform.forward * 0.001f;
            equippedHammerObject.transform.rotation = playerController.transform.rotation * Quaternion.Euler(60f, 0f, 0f);
        }

        // 3. Cập nhật Đuốc Phát Sáng trên lòng bàn tay trái
        if (isTorchEquipped && equippedTorchObject != null && equippedTorchObject.activeSelf)
        {
            Transform anchor = cachedLeftHandBone != null ? cachedLeftHandBone : playerController.transform;
            Vector3 palmPos = (cachedLeftHandBone != null && cachedLeftHandPalmBone != null)
                ? Vector3.Lerp(cachedLeftHandBone.position, cachedLeftHandPalmBone.position, 0.65f)
                : anchor.position;
            equippedTorchObject.transform.position = palmPos + playerController.transform.forward * 0.001f + Vector3.up * 0.002f;
            equippedTorchObject.transform.rotation = playerController.transform.rotation * Quaternion.Euler(20f, 0f, 0f);
            if (torchLight != null)
            {
                torchLight.intensity = 2.0f + Mathf.Sin(Time.time * 14f) * 0.18f;
            }
        }

        // 4. Cập nhật Balo trên lưng nhân vật (Spine) với tỷ lệ thu nhỏ vừa khít lưng
        if (isBackpackEquipped && equippedBackpackObject != null && equippedBackpackObject.activeSelf)
        {
            Transform anchor = cachedSpineBone != null ? cachedSpineBone : playerController.transform;
            equippedBackpackObject.transform.position = anchor.position - playerController.transform.forward * 0.0042f + Vector3.up * 0.0012f;
            equippedBackpackObject.transform.rotation = playerController.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

            Transform bpModel = equippedBackpackObject.transform.Find("BackpackModel");
            if (bpModel != null)
            {
                bpModel.localScale = Vector3.one * (0.020f * backpackSizeMultiplier);
            }
        }
    }

    public void OpenInventoryTab()
    {
        if (isCraftingOpen) ReturnAllCraftingItems();
        ClearDrag();
        isInventoryOpen = true;
        isCraftingOpen = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (selectedSlotIndex < 0 && items.Count > 0) selectedSlotIndex = 0;
    }

    public void OpenCraftingTab()
    {
        ClearDrag();
        isInventoryOpen = true;
        isCraftingOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ToggleInventory()
    {
        if (isInventoryOpen) CloseAllWindows();
        else OpenInventoryTab();
    }

    public void CloseInventory()
    {
        CloseAllWindows();
    }

    public void CloseAllWindows()
    {
        ReturnAllCraftingItems();
        ClearDrag();
        isInventoryOpen = false;
        isCraftingOpen = false;
        selectedSlotIndex = -1;
    }

    private void ClearDrag()
    {
        dragItemId = null;
        dragFromInv = -1;
        dragFromCell = -1;
    }

    // =========================================================================
    // TÚI ĐỒ (METHODS)
    // =========================================================================
    public int GetItemCount(string itemId)
    {
        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].itemId == itemId) total += items[i].quantity;
        }
        return total;
    }

    public bool ConsumeItem(string itemId, int amount = 1)
    {
        if (GetItemCount(itemId) < amount) return false;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].itemId != itemId) continue;

            int take = Mathf.Min(items[i].quantity, amount);
            items[i].quantity -= take;
            amount -= take;
            OnInventoryChanged(itemId, take, false);

            if (items[i].quantity <= 0) items.RemoveAt(i);
            if (amount <= 0) break;
        }

        if (selectedSlotIndex >= items.Count) selectedSlotIndex = items.Count - 1;
        return true;
    }

    public bool AddItem(string id, string name, string desc, int quantity, float heal = 30f, string itemType = "Consumable")
    {
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
                    if (selectedSlotIndex < 0) selectedSlotIndex = 0;
                    OnInventoryChanged(id, toAdd, true);
                    return true;
                }
            }
        }

        if (items.Count < totalSlots && quantity > 0)
        {
            items.Add(new SlotItem(id, name, desc, quantity, heal, itemType));
            if (selectedSlotIndex < 0) selectedSlotIndex = 0;
            OnInventoryChanged(id, quantity, true);
            return true;
        }

        return false;
    }

    public bool AddItemById(string id, int quantity)
    {
        string name, desc, type;
        float heal;
        GetItemDefinition(id, out name, out desc, out heal, out type);
        return AddItem(id, name, desc, quantity, heal, type);
    }

    public void UseItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return;

        SlotItem item = items[index];

        if (item.itemId == "poison_thorn_spear")
        {
            EquipSpear(!isSpearEquipped);
            ItemPickup.ShowToast(isSpearEquipped
                ? "⚔️ Đã trang bị [Giáo Gai Độc] lên tay! (+25 Sát thương & Tăng tầm đánh)"
                : "🛡️ Đã cất [Giáo Gai Độc] vào Túi Đồ.");
            return;
        }

        if (item.itemId == "stone_hammer")
        {
            EquipHammer(!isHammerEquipped);
            ItemPickup.ShowToast(isHammerEquipped
                ? "🔨 Đã trang bị [Búa Đá Chiến] lên tay! (+18 Sát thương)"
                : "🛡️ Đã cất [Búa Đá Chiến] vào Túi Đồ.");
            return;
        }

        if (item.itemId == "torch")
        {
            EquipTorch(!isTorchEquipped);
            ItemPickup.ShowToast(isTorchEquipped
                ? "🏮 Đã cầm [Đuốc Phát Sáng]! Ánh lửa xua tan bóng tối quanh nhân vật."
                : "🏮 Đã tắt đuốc phát sáng.");
            return;
        }

        if (item.itemId == "backpack")
        {
            EquipBackpack(!isBackpackEquipped);
            ItemPickup.ShowToast(isBackpackEquipped
                ? "🎒 Đã trang bị [Balo 24 Ô]! Dung lượng túi đồ đã tăng lên 24 Ô!"
                : "🎒 Đã tháo Balo. Túi đồ trở về 12 Ô.");
            return;
        }

        if (item.itemId == "ladder")
        {
            PlaceLadderInFrontOfPlayer();
            item.quantity--;
            OnInventoryChanged(item.itemId, 1, false);
            if (item.quantity <= 0)
            {
                items.RemoveAt(index);
                selectedSlotIndex = -1;
            }
            return;
        }

        if (item.type == "Material")
        {
            string matId = item.itemId;
            OpenCraftingTab();
            if (TryAutoPlace(matId))
            {
                ItemPickup.ShowToast($"Đã đặt 1 [{item.itemName}] vào Bàn Chế Tạo!");
            }
            return;
        }

        if (item.type == "Consumable" || item.itemId == "health_syringe")
        {
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                if (playerHealth.currentHealth >= playerHealth.maxHealth)
                {
                    ItemPickup.ShowToast("Lượng máu của bạn đã đầy (100 HP), chưa cần hồi phục!");
                    return;
                }

                playerHealth.Heal(item.healAmount);
                item.quantity--;
                OnInventoryChanged(item.itemId, 1, false);

                if (item.quantity <= 0)
                {
                    items.RemoveAt(index);
                    selectedSlotIndex = -1;
                }
            }
        }
    }

    public void DropItemAt(int index)
    {
        if (index < 0 || index >= items.Count) return;
        SlotItem item = items[index];
        if (item.itemId == "poison_thorn_spear" && isSpearEquipped) EquipSpear(false);
        if (item.itemId == "stone_hammer" && isHammerEquipped) EquipHammer(false);
        if (item.itemId == "torch" && isTorchEquipped) EquipTorch(false);
        if (item.itemId == "backpack" && isBackpackEquipped) EquipBackpack(false);

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

    // =========================================================================
    // BÀN CHẾ TẠO (CÔNG THỨC & AUTO-FILL NHƯ MINECRAFT)
    // =========================================================================
    public CraftResult EvaluateCraftRecipe()
    {
        int count = 0;
        for (int i = 0; i < craftGrid.Length; i++)
        {
            if (!string.IsNullOrEmpty(craftGrid[i])) count++;
        }
        if (count == 0) return default;

        // 1. Giáo Gai Độc: 1 cột (Gai Độc -> Mạng Nhện -> Xương Dài)
        if (count == 3)
        {
            for (int c = 0; c < 3; c++)
            {
                if (craftGrid[c] == "poison_thorn" && craftGrid[3 + c] == "spider_silk" && craftGrid[6 + c] == "long_bone")
                    return new CraftResult { itemId = "poison_thorn_spear", quantity = 1 };
            }
        }

        // 2. Búa Đá: 1 cột (Đá -> Dây Thừng -> Cây Gậy)
        if (count == 3)
        {
            for (int c = 0; c < 3; c++)
            {
                if (craftGrid[c] == "stone" && craftGrid[3 + c] == "rope" && craftGrid[6 + c] == "stick")
                    return new CraftResult { itemId = "stone_hammer", quantity = 1 };
            }
        }

        // 3. Đuốc Phát Sáng: Than trên, Cây Gậy dưới (trong bất kỳ cột nào, 2 ô)
        if (count == 2)
        {
            for (int c = 0; c < 3; c++)
            {
                if (craftGrid[c] == "coal" && craftGrid[3 + c] == "stick")
                    return new CraftResult { itemId = "torch", quantity = 4 };
                if (craftGrid[3 + c] == "coal" && craftGrid[6 + c] == "stick")
                    return new CraftResult { itemId = "torch", quantity = 4 };
            }
        }

        // 4. Thang Leo: Cột (Cây Gậy, Dây Thừng hoặc Cây Gậy, Cây Gậy) hoặc chữ H (7 gậy)
        if (count == 3)
        {
            for (int c = 0; c < 3; c++)
            {
                if (craftGrid[c] == "stick" && (craftGrid[3 + c] == "rope" || craftGrid[3 + c] == "stick") && craftGrid[6 + c] == "stick")
                    return new CraftResult { itemId = "ladder", quantity = 2 };
            }
        }
        if (count == 7)
        {
            bool isH = craftGrid[0] == "stick" && craftGrid[3] == "stick" && craftGrid[6] == "stick" &&
                       craftGrid[2] == "stick" && craftGrid[5] == "stick" && craftGrid[8] == "stick" &&
                       (craftGrid[4] == "stick" || craftGrid[4] == "rope") &&
                       string.IsNullOrEmpty(craftGrid[1]) && string.IsNullOrEmpty(craftGrid[7]);
            if (isH) return new CraftResult { itemId = "ladder", quantity = 4 };
        }

        // 5. Balo 24 Ô: Dây thừng hàng trên, Vải bao quanh (ít nhất 4 Vải + 1 Dây Thừng)
        if (count >= 5 && count <= 9)
        {
            bool hasRopeTop = craftGrid[0] == "rope" || craftGrid[1] == "rope" || craftGrid[2] == "rope";
            bool hasClothMid = (craftGrid[3] == "cloth" || craftGrid[5] == "cloth");
            bool hasClothBottom = (craftGrid[6] == "cloth" || craftGrid[7] == "cloth" || craftGrid[8] == "cloth");
            if (hasRopeTop && hasClothMid && hasClothBottom)
            {
                return new CraftResult { itemId = "backpack", quantity = 1 };
            }
        }

        // 6. Khớp công thức động từ Backend / Admin Web (AllRecipes)
        Dictionary<string, int> gridCounts = new Dictionary<string, int>();
        for (int i = 0; i < craftGrid.Length; i++)
        {
            string gid = craftGrid[i];
            if (string.IsNullOrEmpty(gid)) continue;
            if (!gridCounts.ContainsKey(gid)) gridCounts[gid] = 0;
            gridCounts[gid]++;
        }

        for (int r = 0; r < AllRecipes.Count; r++)
        {
            RecipeDefinition rec = AllRecipes[r];
            if (rec == null || rec.requiredItems == null || rec.requiredItems.Count == 0) continue;
            if (rec.requiredItems.Count != gridCounts.Count) continue;

            bool match = true;
            foreach (var kv in rec.requiredItems)
            {
                if (!gridCounts.TryGetValue(kv.Key, out int placed) || placed != kv.Value)
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return new CraftResult { itemId = rec.resultId, quantity = Mathf.Max(1, rec.resultQuantity) };
            }
        }

        return default;
    }

    /// <summary>
    /// Tự động xếp nguyên liệu vào lưới 3x3 khi người chơi bấm vào công thức trong Sách Công Thức (kiểu Minecraft)
    /// </summary>
    public bool AutoFillRecipe(RecipeDefinition recipe)
    {
        // 1. Kiểm tra nguyên liệu trong Túi Đồ
        foreach (var kvp in recipe.requiredItems)
        {
            int current = GetItemCount(kvp.Key);
            if (current < kvp.Value)
            {
                string rName, rDesc, rType; float rH;
                GetItemDefinition(kvp.Key, out rName, out rDesc, out rH, out rType);
                ItemPickup.ShowToast($"⚠️ Thiếu nguyên liệu: Cần x{kvp.Value} {rName}!");
                return false;
            }
        }

        // 2. Trả toàn bộ vật phẩm đang nằm trên bàn chế tạo về lại túi đồ
        ReturnAllCraftingItems();

        // 3. Rút nguyên liệu từ túi đồ và đặt chuẩn xác vào lưới 3x3
        for (int i = 0; i < 9; i++)
        {
            string reqId = recipe.pattern[i];
            if (!string.IsNullOrEmpty(reqId))
            {
                if (ConsumeItem(reqId, 1))
                {
                    craftGrid[i] = reqId;
                }
            }
        }

        ItemPickup.ShowToast($"⚡ Đã tự động xếp công thức [{recipe.recipeName}]!");
        return true;
    }

    public bool TryAutoPlace(string itemId)
    {
        int targetCell = -1;
        if (itemId == "poison_thorn") targetCell = 1;
        else if (itemId == "spider_silk") targetCell = 4;
        else if (itemId == "long_bone") targetCell = 7;
        else if (itemId == "coal") targetCell = 1;
        else if (itemId == "stick") targetCell = 4;
        else if (itemId == "stone") targetCell = 1;
        else if (itemId == "rope") targetCell = 4;
        else if (itemId == "cloth") targetCell = 7;

        if (targetCell >= 0 && string.IsNullOrEmpty(craftGrid[targetCell]))
        {
            if (ConsumeItem(itemId, 1))
            {
                craftGrid[targetCell] = itemId;
                return true;
            }
        }

        for (int i = 0; i < craftGrid.Length; i++)
        {
            if (string.IsNullOrEmpty(craftGrid[i]))
            {
                if (ConsumeItem(itemId, 1))
                {
                    craftGrid[i] = itemId;
                    return true;
                }
                return false;
            }
        }
        return false;
    }

    private void ReturnCellToInventory(int cell)
    {
        if (cell < 0 || cell >= craftGrid.Length) return;
        string id = craftGrid[cell];
        if (string.IsNullOrEmpty(id)) return;
        craftGrid[cell] = null;
        AddItemById(id, 1);
    }

    public void ReturnAllCraftingItems()
    {
        for (int i = 0; i < craftGrid.Length; i++) ReturnCellToInventory(i);
    }

    public void CraftResultItem()
    {
        CraftResult res = EvaluateCraftRecipe();
        if (string.IsNullOrEmpty(res.itemId)) return;

        if (!AddItemById(res.itemId, res.quantity))
        {
            ItemPickup.ShowToast("⚠️ Túi đồ đã đầy, không thể nhận vật phẩm chế tạo!");
            return;
        }

        for (int i = 0; i < craftGrid.Length; i++) craftGrid[i] = null;

        string rn, rd, rt; float rh;
        GetItemDefinition(res.itemId, out rn, out rd, out rh, out rt);

        if (res.itemId == "poison_thorn_spear")
        {
            EquipSpear(true);
            ItemPickup.ShowToast($"⚔️ CHẾ TẠO THÀNH CÔNG [{rn.ToUpper()}]! Đã trang bị lên tay (+25 DMG)!");
        }
        else if (res.itemId == "stone_hammer")
        {
            EquipHammer(true);
            ItemPickup.ShowToast($"🔨 CHẾ TẠO THÀNH CÔNG [{rn.ToUpper()}]! Đã trang bị lên tay (+18 DMG)!");
        }
        else if (res.itemId == "backpack")
        {
            EquipBackpack(true);
            ItemPickup.ShowToast($"🎒 CHẾ TẠO THÀNH CÔNG [{rn.ToUpper()}]! Dung lượng túi đồ đã tăng lên 24 Ô!");
        }
        else if (res.itemId == "torch")
        {
            ItemPickup.ShowToast($"🏮 CHẾ TẠO THÀNH CÔNG [{rn.ToUpper()}] (x{res.quantity})! Có thể trang bị để soi sáng.");
        }
        else if (res.itemId == "ladder")
        {
            ItemPickup.ShowToast($"🪜 CHẾ TẠO THÀNH CÔNG [{rn.ToUpper()}] (x{res.quantity})! Đặt xuống đất và bấm [W] để leo lên cao!");
        }
    }

    private void HandleCraftDrop(Vector2 mp)
    {
        string id = dragItemId;
        int fromCell = dragFromCell;
        ClearDrag();
        if (string.IsNullOrEmpty(id)) return;

        int target = -1;
        for (int c = 0; c < craftCellRects.Length; c++)
        {
            if (craftCellRects[c].Contains(mp))
            {
                target = c;
                break;
            }
        }

        if (fromCell >= 0)
        {
            if (target >= 0 && target != fromCell)
            {
                string other = craftGrid[target];
                craftGrid[target] = id;
                craftGrid[fromCell] = string.IsNullOrEmpty(other) ? null : other;
            }
            else
            {
                ReturnCellToInventory(fromCell);
            }
        }
        else if (target >= 0)
        {
            if (string.IsNullOrEmpty(craftGrid[target]))
            {
                if (ConsumeItem(id, 1)) craftGrid[target] = id;
            }
            else
            {
                ItemPickup.ShowToast("Ô chế tạo này đã có vật phẩm! Bấm vào ô để lấy ra.");
            }
        }
    }

    // =========================================================================
    // TRANG BỊ CÁC VẬT PHẨM (MÔ HÌNH 3D)
    // =========================================================================
    public void EquipSpear(bool equip)
    {
        isSpearEquipped = equip;
        if (equip && isHammerEquipped) EquipHammer(false);

        if (!equip)
        {
            if (equippedSpearObject != null) equippedSpearObject.SetActive(false);
            return;
        }

        if (equippedSpearObject != null)
        {
            equippedSpearObject.SetActive(true);
            return;
        }

        EnsureBonesCached();
        equippedSpearObject = new GameObject("Equipped_PoisonThornSpear");
        GameObject model = ItemModelFactory.BuildSpear();
        model.name = "SpearModel";
        model.transform.SetParent(equippedSpearObject.transform, false);

        // Kích thước giáo thu nhỏ chuẩn tỷ lệ với nhân vật tí hon (cao ~6.8cm)
        float spearBaseScale = 0.042f;
        float spearLengthScale = 0.056f;
        model.transform.localScale = new Vector3(spearBaseScale, spearLengthScale, spearBaseScale) * spearSizeMultiplier;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
    }

    public void EquipHammer(bool equip)
    {
        isHammerEquipped = equip;
        if (equip && isSpearEquipped) EquipSpear(false);

        if (!equip)
        {
            if (equippedHammerObject != null) equippedHammerObject.SetActive(false);
            return;
        }

        if (equippedHammerObject != null)
        {
            equippedHammerObject.SetActive(true);
            return;
        }

        EnsureBonesCached();
        equippedHammerObject = new GameObject("Equipped_StoneHammer");
        GameObject model = ItemModelFactory.BuildStoneHammer();
        model.name = "HammerModel";
        model.transform.SetParent(equippedHammerObject.transform, false);
        model.transform.localScale = Vector3.one * 0.034f;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
    }

    public void EquipTorch(bool equip)
    {
        isTorchEquipped = equip;
        if (!equip)
        {
            if (equippedTorchObject != null) equippedTorchObject.SetActive(false);
            return;
        }

        if (equippedTorchObject != null)
        {
            equippedTorchObject.SetActive(true);
            return;
        }

        EnsureBonesCached();
        equippedTorchObject = new GameObject("Equipped_Torch");
        GameObject model = ItemModelFactory.BuildTorch();
        model.name = "TorchModel";
        model.transform.SetParent(equippedTorchObject.transform, false);
        model.transform.localScale = Vector3.one * 0.032f;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        GameObject lightGo = new GameObject("TorchPointLight");
        lightGo.transform.SetParent(equippedTorchObject.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.025f, 0f);
        torchLight = lightGo.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = new Color(1f, 0.72f, 0.35f);
        torchLight.range = 1.6f;
        torchLight.intensity = 2.0f;
        torchLight.shadows = LightShadows.None;
    }

    public void EquipBackpack(bool equip)
    {
        isBackpackEquipped = equip;
        totalSlots = equip ? 24 : 12;

        if (!equip)
        {
            if (equippedBackpackObject != null) equippedBackpackObject.SetActive(false);
            return;
        }

        if (equippedBackpackObject != null)
        {
            equippedBackpackObject.SetActive(true);
            return;
        }

        EnsureBonesCached();
        equippedBackpackObject = new GameObject("Equipped_Backpack");
        GameObject model = ItemModelFactory.BuildBackpack();
        model.name = "BackpackModel";
        model.transform.SetParent(equippedBackpackObject.transform, false);
        model.transform.localScale = Vector3.one * (0.020f * backpackSizeMultiplier);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
    }

    public void PlaceLadderInFrontOfPlayer()
    {
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        Vector3 spawnPos = transform.position + transform.forward * 0.18f;
        spawnPos.y = playerController != null ? playerController.groundY : 0.08f;

        GameObject ladderGo = new GameObject("Placed_Ladder");
        ladderGo.transform.position = spawnPos;
        ladderGo.transform.rotation = Quaternion.LookRotation(transform.forward);

        GameObject model = ItemModelFactory.BuildLadder();
        model.name = "LadderModel";
        model.transform.SetParent(ladderGo.transform, false);
        model.transform.localScale = Vector3.one * 0.38f;
        model.transform.localPosition = new Vector3(0f, 0.38f * 0.45f, 0f);

        LadderClimbable climbable = ladderGo.AddComponent<LadderClimbable>();
        climbable.ladderHeight = 0.38f;
        climbable.climbRadius = 0.16f;
        climbable.bottomY = spawnPos.y;
        climbable.topY = spawnPos.y + 0.38f;

        BoxCollider col = ladderGo.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.20f, 0.40f, 0.20f);
        col.center = new Vector3(0f, 0.20f, 0f);

        ItemPickup.ShowToast("🪜 ĐÃ ĐẶT THANG LEO! Lại gần thang bấm [W / Phím Lên] để leo lên cao!");
    }

    // =========================================================================
    // GIAO DIỆN
    // =========================================================================
    void OnGUI()
    {
        // HUD buttons and inventory always receive GUI events

        EnsureGuiStylesInitialized();

        float uiScale = Mathf.Max(1f, Screen.height / 540f);
        float virtualW = Screen.width / uiScale;
        float virtualH = Screen.height / uiScale;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

        if (!isInventoryOpen)
        {
            DrawOwnedItemHudSlot(virtualH);
            GUI.matrix = prevMatrix;
            return;
        }

        float winWidth = 950f;
        float winHeight = 512f;
        float winX = (virtualW - winWidth) * 0.5f;
        float winY = (virtualH - winHeight) * 0.5f;

        GUI.color = new Color(0f, 0f, 0f, 0.74f);
        GUI.DrawTexture(new Rect(0, 0, virtualW, virtualH), Texture2D.whiteTexture);

        GUI.color = new Color(0.08f, 0.11f, 0.15f, 0.98f);
        GUI.DrawTexture(new Rect(winX, winY, winWidth, winHeight), Texture2D.whiteTexture);

        GUI.color = new Color(0.95f, 0.76f, 0.25f, 0.98f);
        GUI.Box(new Rect(winX - 3, winY - 3, winWidth + 6, winHeight + 6), "");
        GUI.color = Color.white;

        Color prevBg = GUI.backgroundColor;

        GUI.backgroundColor = !isCraftingOpen ? new Color(0.16f, 0.78f, 0.46f, 1f) : new Color(0.22f, 0.27f, 0.35f, 1f);
        if (GUI.Button(new Rect(winX + 24f, winY + 12f, 260f, 38f), "🎒 TÚI ĐỒ NHÂN VẬT (Phím B)", s_tabBtnStyle))
        {
            OpenInventoryTab();
        }

        GUI.backgroundColor = isCraftingOpen ? new Color(0.92f, 0.65f, 0.14f, 1f) : new Color(0.22f, 0.27f, 0.35f, 1f);
        if (GUI.Button(new Rect(winX + 296f, winY + 12f, 320f, 38f), "🔨 BÀN CHẾ TẠO 3x3 (Phím C)", s_tabBtnStyle))
        {
            OpenCraftingTab();
        }

        GUI.backgroundColor = new Color(0.85f, 0.22f, 0.22f, 1f);
        if (GUI.Button(new Rect(winX + winWidth - 54f, winY + 12f, 42f, 38f), "✖", s_closeBtnStyle))
        {
            CloseAllWindows();
        }
        GUI.backgroundColor = prevBg;

        if (!isInventoryOpen)
        {
            GUI.matrix = prevMatrix;
            return;
        }

        if (!isCraftingOpen)
        {
            DrawInventoryTabContent(winX, winY, winWidth);
        }
        else
        {
            DrawCraftingTabContent(winX, winY, winWidth, uiScale);
        }

        GUI.matrix = prevMatrix;
    }

    private static void DrawSlotBox(Rect r, bool highlight)
    {
        GUI.color = highlight ? new Color(0.20f, 0.42f, 0.36f, 0.98f) : new Color(0.09f, 0.10f, 0.13f, 0.98f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = new Color(0.03f, 0.03f, 0.04f, 1f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);

        GUI.color = highlight ? new Color(0.45f, 1f, 0.78f, 1f) : new Color(0.38f, 0.42f, 0.50f, 1f);
        GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawInventoryTabContent(float winX, float winY, float winWidth)
    {
        if (selectedSlotIndex < 0 && items.Count > 0) selectedSlotIndex = 0;
        if (selectedSlotIndex >= items.Count) selectedSlotIndex = items.Count - 1;

        float gridStartX = winX + 24f;
        float gridStartY = winY + 62f;
        float slotSize = totalSlots > 12 ? 82f : 98f;
        float slotGap = 8f;
        int cols = totalSlots > 12 ? 6 : 4;

        for (int i = 0; i < totalSlots; i++)
        {
            int r = i / cols;
            int c = i % cols;
            float slotX = gridStartX + c * (slotSize + slotGap);
            float slotY = gridStartY + r * (slotSize + slotGap);
            Rect slotRect = new Rect(slotX, slotY, slotSize, slotSize);

            DrawSlotBox(slotRect, selectedSlotIndex == i);

            if (i < items.Count)
            {
                SlotItem item = items[i];
                Texture2D icon = GetItemIconTexture(item.itemId);
                if (icon != null)
                {
                    float iconSize = slotSize * 0.70f;
                    GUI.DrawTexture(new Rect(slotX + (slotSize - iconSize) * 0.5f, slotY + 4f, iconSize, iconSize), icon, ScaleMode.ScaleToFit, true);
                }

                GUI.Label(new Rect(slotX + 2f, slotY + slotSize - 22f, slotSize - 4f, 20f), item.itemName, s_slotItemStyle);
                GUI.Label(new Rect(slotX + slotSize - 40f, slotY + 2f, 38f, 20f), $"x{item.quantity}", s_slotQtyStyle);
            }

            if (GUI.Button(slotRect, "", GUIStyle.none))
            {
                selectedSlotIndex = (i < items.Count) ? i : -1;
            }
        }

        float detailX = winX + winWidth - 365f;
        float detailY = winY + 62f;
        float detailW = 345f;
        float detailH = 430f;

        GUI.color = new Color(0.13f, 0.16f, 0.21f, 0.98f);
        GUI.DrawTexture(new Rect(detailX, detailY, detailW, detailH), Texture2D.whiteTexture);
        GUI.color = new Color(0.32f, 0.55f, 0.50f, 0.9f);
        GUI.Box(new Rect(detailX, detailY, detailW, detailH), "");
        GUI.color = Color.white;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < items.Count)
        {
            SlotItem sel = items[selectedSlotIndex];
            Texture2D selIcon = GetItemIconTexture(sel.itemId);

            Rect previewBox = new Rect(detailX + 14f, detailY + 14f, 86f, 86f);
            DrawSlotBox(previewBox, true);
            if (selIcon != null)
            {
                GUI.DrawTexture(new Rect(previewBox.x + 4f, previewBox.y + 4f, 78f, 78f), selIcon, ScaleMode.ScaleToFit, true);
            }

            GUI.Label(new Rect(detailX + 110f, detailY + 14f, detailW - 120f, 40f), sel.itemName, s_selTitleStyle);
            GUI.Label(new Rect(detailX + 110f, detailY + 54f, detailW - 120f, 22f), $"Số lượng: <color=#ffdd44>x{sel.quantity}</color>", s_statStyle);

            string effectLine = sel.type == "Weapon"
                ? (sel.itemId == "poison_thorn_spear" ? "Sát thương: <color=#55ff88>+25 DMG & Tầm đánh xa</color>" : "Sát thương: <color=#55ff88>+18 DMG</color>")
                : (sel.type == "Tool"
                    ? "<color=#55ddff>Công cụ tiện ích</color>"
                    : (sel.type == "Material" ? "Loại: <color=#ffcc44>Nguyên liệu Chế Tạo</color>" : $"Hồi phục: <color=#55ff88>+{sel.healAmount} HP</color>"));
            GUI.Label(new Rect(detailX + 110f, detailY + 76f, detailW - 120f, 22f), effectLine, s_statStyle);
            GUI.Label(new Rect(detailX + 14f, detailY + 110f, detailW - 28f, 130f), sel.description, s_descStyle);

            Color prevB = GUI.backgroundColor;
            string actionLabel = "SỬ DỤNG";
            if (sel.itemId == "poison_thorn_spear") actionLabel = isSpearEquipped ? "🛡️ CẤT GIÁO GAI ĐỘC" : "⚔️ TRANG BỊ GIÁO (+25 DMG)";
            else if (sel.itemId == "stone_hammer") actionLabel = isHammerEquipped ? "🛡️ CẤT BÚA ĐÁ" : "🔨 TRANG BỊ BÚA (+18 DMG)";
            else if (sel.itemId == "torch") actionLabel = isTorchEquipped ? "🏮 TẮT ĐUỐC PHÁT SÁNG" : "🏮 CẦM ĐUỐC PHÁT SÁNG";
            else if (sel.itemId == "backpack") actionLabel = isBackpackEquipped ? "🎒 THÁO BALO (VỀ 12 Ô)" : "🎒 TRANG BỊ BALO (24 Ô)";
            else if (sel.itemId == "ladder") actionLabel = "🪜 ĐẶT THANG LEO XUỐNG ĐẤT";
            else if (sel.type == "Material") actionLabel = "🔨 ĐƯA VÀO BÀN CHẾ TẠO";
            else if (sel.type == "Consumable") actionLabel = "💊 SỬ DỤNG (+30 HP)";

            GUI.backgroundColor = new Color(0.16f, 0.82f, 0.38f);
            bool useClicked = GUI.Button(new Rect(detailX + 14f, detailY + 280f, detailW - 28f, 48f), actionLabel, s_actionBtnStyle);
            GUI.backgroundColor = new Color(0.84f, 0.25f, 0.25f);
            bool dropClicked = GUI.Button(new Rect(detailX + 14f, detailY + 342f, detailW - 28f, 42f), "🗑️ VỨT BỎ VẬT PHẨM", s_actionBtnStyle);
            GUI.backgroundColor = prevB;

            if (useClicked) UseItemAt(selectedSlotIndex);
            else if (dropClicked) DropItemAt(selectedSlotIndex);
        }
        else
        {
            string emptyMsg = items.Count == 0
                ? "Túi đồ hiện đang trống.\n\nHãy bấm phím C để mở Bàn Chế Tạo hoặc lại gần vật phẩm rơi ra bấm [Phím E] để nhặt!"
                : "Bấm chọn 1 ô đồ bên trái để xem chi tiết, trang bị hoặc sử dụng.";
            GUI.Label(new Rect(detailX + 18f, detailY + 140f, detailW - 36f, 150f), emptyMsg, s_hintStyle);
        }
    }

    // =========================================================================
    // GIAO DIỆN BÀN CHẾ TẠO & SÁCH CÔNG THỨC KIỂU MINECRAFT
    // =========================================================================
    private void DrawCraftingTabContent(float winX, float winY, float winWidth, float uiScale)
    {
        Event e = Event.current;
        Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / uiScale;
        bool leftDown = e.type == EventType.MouseDown && e.button == 0;
        bool rightDown = e.type == EventType.MouseDown && e.button == 1;
        bool leftUp = e.type == EventType.MouseUp && e.button == 0;

        if (e.type == EventType.Repaint && dragItemId != null && !Input.GetMouseButton(0))
        {
            ClearDrag();
        }

        CraftResult craftResult = EvaluateCraftRecipe();

        // -------------------------------------------------------------
        // 1. PANEL TRÁI: TÚI ĐỒ NHÂN VẬT (Kéo thả hoặc Chuột phải đặt nhanh)
        // -------------------------------------------------------------
        float leftX = winX + 16f;
        float leftY = winY + 62f;
        float leftW = 295f;
        float leftH = 432f;
        Rect leftRect = new Rect(leftX, leftY, leftW, leftH);

        GUI.color = new Color(0.12f, 0.15f, 0.20f, 0.98f);
        GUI.DrawTexture(leftRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.45f, 0.58f, 0.68f, 0.9f);
        GUI.Box(leftRect, "");
        GUI.color = Color.white;

        GUI.Label(new Rect(leftX + 8f, leftY + 6f, leftW - 16f, 22f), "TÚI ĐỒ NHÂN VẬT", s_craftHeaderStyle);
        GUI.Label(new Rect(leftX + 8f, leftY + 28f, leftW - 16f, 18f), "Chuột trái: Kéo thả  |  Chuột phải: Đặt nhanh", s_craftStatusStyle);

        float slotS = 58f;
        float gap = 6f;
        float gridX = leftX + 10f;
        float gridY = leftY + 50f;
        int pressedInv = -1;
        int rightPressedInv = -1;
        SlotItem hoveredItem = null;

        for (int i = 0; i < totalSlots; i++)
        {
            int r = i / 4;
            int c = i % 4;
            Rect rect = new Rect(gridX + c * (slotS + gap), gridY + r * (slotS + gap), slotS, slotS);
            DrawSlotBox(rect, false);

            if (i < items.Count)
            {
                SlotItem it = items[i];
                bool beingDragged = dragFromInv == i && dragItemId != null;
                GUI.color = beingDragged ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                Texture2D icon = GetItemIconTexture(it.itemId);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, slotS - 8f, slotS - 8f), icon, ScaleMode.ScaleToFit, true);
                }
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x, rect.yMax - 18f, rect.width - 4f, 16f), $"{it.quantity}", s_slotQtyStyle);

                if (rect.Contains(mp))
                {
                    hoveredItem = it;
                    if (leftDown) pressedInv = i;
                    if (rightDown) rightPressedInv = i;
                }
            }
        }

        // Nút dọn / lấy lại toàn bộ đồ trên bàn chế tạo về túi
        Color prevB = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.75f, 0.40f, 0.20f);
        if (GUI.Button(new Rect(leftX + 10f, leftY + leftH - 42f, leftW - 20f, 32f), "🗑️ LẤY LẠI TẤT CẢ TỪ BÀN CHẾ TẠO", s_recipeBtnStyle))
        {
            ReturnAllCraftingItems();
        }
        GUI.backgroundColor = prevB;

        // -------------------------------------------------------------
        // 2. PANEL GIỮA: BÀN CHẾ TẠO 3x3 (Lưới kéo thả + Ô Kết Quả)
        // -------------------------------------------------------------
        float midX = winX + 322f;
        float midY = winY + 62f;
        float midW = 285f;
        float midH = 432f;
        Rect midRect = new Rect(midX, midY, midW, midH);

        GUI.color = new Color(0.11f, 0.14f, 0.19f, 0.98f);
        GUI.DrawTexture(midRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.76f, 0.25f, 0.92f);
        GUI.Box(midRect, "");
        GUI.color = Color.white;

        GUI.Label(new Rect(midX + 8f, midY + 6f, midW - 16f, 24f), "BÀN CHẾ TẠO 3x3", s_craftHeaderStyle);

        float cell = 64f;
        float cgap = 6f;
        float cgX = midX + (midW - (3f * cell + 2f * cgap)) * 0.5f;
        float cgY = midY + 36f;
        int pressedCell = -1;

        for (int idx = 0; idx < 9; idx++)
        {
            int r = idx / 3;
            int c = idx % 3;
            Rect rect = new Rect(cgX + c * (cell + cgap), cgY + r * (cell + cgap), cell, cell);
            craftCellRects[idx] = rect;

            bool hoverTarget = dragItemId != null && rect.Contains(mp);
            DrawSlotBox(rect, hoverTarget);

            string cid = craftGrid[idx];
            if (!string.IsNullOrEmpty(cid))
            {
                bool beingDragged = dragFromCell == idx && dragItemId != null;
                GUI.color = beingDragged ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                Texture2D icon = GetItemIconTexture(cid);
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, cell - 8f, cell - 8f), icon, ScaleMode.ScaleToFit, true);
                }
                GUI.color = Color.white;

                if (leftDown && rect.Contains(mp)) pressedCell = idx;
            }
        }

        // Mũi tên chỉ hướng xuống ô kết quả
        float arrowY = cgY + 3f * cell + 2f * cgap + 8f;
        float arrowW = 34f;
        float arrowH = 18f;
        float arrowX = midX + (midW - arrowW) * 0.5f;
        GUI.color = !string.IsNullOrEmpty(craftResult.itemId) ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(0.50f, 0.55f, 0.60f, 0.8f);
        GUI.Label(new Rect(arrowX, arrowY, arrowW, arrowH), "▼", s_craftHeaderStyle);
        GUI.color = Color.white;

        // Ô Kết Quả
        float outS = 78f;
        craftOutRect = new Rect(midX + (midW - outS) * 0.5f, arrowY + 22f, outS, outS);
        DrawSlotBox(craftOutRect, !string.IsNullOrEmpty(craftResult.itemId));

        if (!string.IsNullOrEmpty(craftResult.itemId))
        {
            Texture2D resIcon = GetItemIconTexture(craftResult.itemId);
            if (resIcon != null)
            {
                GUI.DrawTexture(new Rect(craftOutRect.x + 5f, craftOutRect.y + 5f, outS - 10f, outS - 10f), resIcon, ScaleMode.ScaleToFit, true);
            }

            if (craftResult.quantity > 1)
            {
                GUI.Label(new Rect(craftOutRect.xMax - 36f, craftOutRect.yMax - 22f, 32f, 20f), $"x{craftResult.quantity}", s_slotQtyStyle);
            }

            string rn, rd, rt; float rh;
            GetItemDefinition(craftResult.itemId, out rn, out rd, out rh, out rt);
            GUI.Label(new Rect(midX + 8f, craftOutRect.yMax + 4f, midW - 16f, 38f), $"<color=#ffe066>{rn}</color>\n<color=#8aff9c>Bấm vào ô để chế tạo!</color>", s_craftStatusStyle);
        }
        else
        {
            GUI.Label(new Rect(midX + 8f, craftOutRect.yMax + 4f, midW - 16f, 38f), "<color=#99a8b5>Xếp nguyên liệu hoặc chọn\nSách Công Thức bên phải</color>", s_craftStatusStyle);
        }

        // -------------------------------------------------------------
        // 3. PANEL PHẢI: SÁCH CÔNG THỨC (MINECRAFT RECIPE BOOK)
        // -------------------------------------------------------------
        float rightX = winX + 618f;
        float rightY = winY + 62f;
        float rightW = winWidth - (rightX - winX) - 16f;
        float rightH = 432f;
        Rect rightRect = new Rect(rightX, rightY, rightW, rightH);

        GUI.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);
        GUI.DrawTexture(rightRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.38f, 0.70f, 0.55f, 0.95f);
        GUI.Box(rightRect, "");
        GUI.color = Color.white;

        GUI.Label(new Rect(rightX + 8f, rightY + 6f, rightW - 16f, 22f), "📖 SÁCH CÔNG THỨC", s_craftHeaderStyle);
        GUI.Label(new Rect(rightX + 8f, rightY + 28f, rightW - 16f, 18f), "Bấm [⚡ Xếp] để tự động đặt nguyên liệu", s_craftStatusStyle);

        float cardW = rightW - 20f;
        float cardH = 68f;
        float cardStartX = rightX + 10f;
        float cardStartY = rightY + 50f;

        for (int i = 0; i < AllRecipes.Count; i++)
        {
            RecipeDefinition recipe = AllRecipes[i];
            float cy = cardStartY + i * (cardH + 6f);
            Rect cardRect = new Rect(cardStartX, cy, cardW, cardH);

            // Nền thẻ công thức
            GUI.color = new Color(0.16f, 0.20f, 0.26f, 0.98f);
            GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.30f, 0.45f, 0.40f, 0.85f);
            GUI.Box(cardRect, "");
            GUI.color = Color.white;

            // Icon sản phẩm của công thức
            Texture2D rIcon = GetItemIconTexture(recipe.resultId);
            Rect rIconRect = new Rect(cardRect.x + 6f, cardRect.y + 6f, 44f, 44f);
            DrawSlotBox(rIconRect, false);
            if (rIcon != null)
            {
                GUI.DrawTexture(new Rect(rIconRect.x + 3f, rIconRect.y + 3f, 38f, 38f), rIcon, ScaleMode.ScaleToFit, true);
            }
            if (recipe.resultQuantity > 1)
            {
                GUI.Label(new Rect(rIconRect.xMax - 26f, rIconRect.yMax - 18f, 24f, 16f), $"x{recipe.resultQuantity}", s_slotQtyStyle);
            }

            // Tên và thông số
            GUI.Label(new Rect(cardRect.x + 56f, cardRect.y + 4f, cardW - 146f, 20f), $"<color=#ffe066>{recipe.recipeName}</color>", s_statStyle);
            GUI.Label(new Rect(cardRect.x + 56f, cardRect.y + 24f, cardW - 146f, 36f), $"<size=11>{recipe.statsDesc}</size>", s_descStyle);

            // Xem trước mini 3x3 (10x10 px mỗi ô)
            float miniBoxX = cardRect.xMax - 86f;
            float miniBoxY = cardRect.y + 6f;
            float miniCell = 9f;
            for (int mr = 0; mr < 3; mr++)
            {
                for (int mc = 0; mc < 3; mc++)
                {
                    int mIdx = mr * 3 + mc;
                    Rect mRect = new Rect(miniBoxX + mc * (miniCell + 2f), miniBoxY + mr * (miniCell + 2f), miniCell, miniCell);
                    bool hasMat = !string.IsNullOrEmpty(recipe.pattern[mIdx]);
                    GUI.color = hasMat ? new Color(0.40f, 0.92f, 0.55f, 0.95f) : new Color(0.15f, 0.18f, 0.22f, 0.8f);
                    GUI.DrawTexture(mRect, Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;

            // Nút [⚡ Xếp]
            prevB = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.18f, 0.78f, 0.42f);
            bool autoFillClicked = GUI.Button(new Rect(cardRect.xMax - 88f, cardRect.y + 40f, 82f, 22f), "⚡ Xếp", s_recipeBtnStyle);
            GUI.backgroundColor = prevB;

            if (autoFillClicked)
            {
                AutoFillRecipe(recipe);
            }
        }

        // -------------------------------------------------------------
        // XỬ LÝ SỰ KIỆN KÉO THẢ & CHUỘT
        // -------------------------------------------------------------
        if (pressedInv >= 0 && dragItemId == null)
        {
            dragItemId = items[pressedInv].itemId;
            dragFromInv = pressedInv;
            dragFromCell = -1;
            selectedSlotIndex = pressedInv;
            e.Use();
        }
        else if (rightPressedInv >= 0 && dragItemId == null)
        {
            string quickId = items[rightPressedInv].itemId;
            if (!TryAutoPlace(quickId))
            {
                ItemPickup.ShowToast("Không còn ô trống trên Bàn Chế Tạo!");
            }
            e.Use();
        }
        else if (pressedCell >= 0 && dragItemId == null)
        {
            dragItemId = craftGrid[pressedCell];
            dragFromCell = pressedCell;
            dragFromInv = -1;
            e.Use();
        }
        else if (leftDown && !string.IsNullOrEmpty(craftResult.itemId) && craftOutRect.Contains(mp))
        {
            CraftResultItem();
            e.Use();
        }
        else if (leftUp && dragItemId != null)
        {
            HandleCraftDrop(mp);
            e.Use();
        }

        // Vẽ Icon vật phẩm bám theo con trỏ chuột khi đang kéo
        if (dragItemId != null && e.type == EventType.Repaint)
        {
            Texture2D dragIcon = GetItemIconTexture(dragItemId);
            if (dragIcon != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.95f);
                GUI.DrawTexture(new Rect(mp.x - 26f, mp.y - 26f, 52f, 52f), dragIcon, ScaleMode.ScaleToFit, true);
                GUI.color = Color.white;
            }
        }
    }

    void DrawOwnedItemHudSlot(float virtualH)
    {
        float boxX = 18f;
        float boxY = virtualH - 112f;
        float boxW = 380f;
        float boxH = 94f;

        GUI.color = new Color(0.22f, 0.92f, 0.58f, 0.95f);
        GUI.DrawTexture(new Rect(boxX - 2f, boxY - 2f, boxW + 4f, boxH + 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.06f, 0.10f, 0.14f, 0.94f);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

        SlotItem displayItem = items.Count > 0 ? items[0] : null;
        string iconId = isSpearEquipped ? "poison_thorn_spear" : (isHammerEquipped ? "stone_hammer" : (isTorchEquipped ? "torch" : (displayItem != null ? displayItem.itemId : "health_syringe")));
        Texture2D iconTex = GetItemIconTexture(iconId);

        Rect iconBox = new Rect(boxX + 8f, boxY + 9f, 76f, 76f);
        GUI.color = new Color(0.12f, 0.24f, 0.27f, 0.98f);
        GUI.DrawTexture(iconBox, Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (iconTex != null)
        {
            GUI.DrawTexture(new Rect(iconBox.x + 4f, iconBox.y + 4f, 68f, 68f), iconTex, ScaleMode.ScaleToFit, true);
        }

        string titleTxt = isSpearEquipped ? "⚔️ Đang cầm: Giáo Gai Độc (+25 DMG)"
            : (isHammerEquipped ? "🔨 Đang cầm: Búa Đá Chiến (+18 DMG)"
                : (isTorchEquipped ? "🏮 Đang cầm: Đuốc Phát Sáng"
                    : (displayItem != null ? $"{displayItem.itemName} (Tổng {items.Count} món)" : "Túi đồ rỗng")));
        GUI.Label(new Rect(boxX + 92f, boxY + 8f, boxW - 98f, 24f), titleTxt, s_hudNameStyle);

        Rect btnBagRect = new Rect(boxX + 92f, boxY + 32f, 136f, 26f);
        Rect btnCraftRect = new Rect(boxX + 234f, boxY + 32f, 140f, 26f);

        Color prevGuiBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.18f, 0.72f, 0.44f, 1f);
        if (GUI.Button(btnBagRect, "🎒 Túi Đồ [Phím B]"))
        {
            OpenInventoryTab();
        }

        GUI.backgroundColor = new Color(0.92f, 0.65f, 0.14f, 1f);
        if (GUI.Button(btnCraftRect, "🔨 Chế Tạo [Phím C]"))
        {
            OpenCraftingTab();
        }
        GUI.backgroundColor = prevGuiBg;
        GUI.Label(new Rect(boxX + 92f, boxY + 58f, boxW - 98f, 22f), "💊 [Phím H] Dùng nhanh Ống Tiêm (+30 HP)", s_hudKeyStyle);
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
        }
    }

    // ============================================================
    // TẢI CẤU HÌNH & VẬT PHẨM TỪ BACKEND (API: /api/v1/game/...)
    // ============================================================
    public IEnumerator LoadGameDataFromServer(bool isBackgroundPoll = false, bool forceToast = false)
    {
        string combinedJson = "";
        bool anySuccess = false;

        // 1. Tải Base Attributes (Chỉ số nhân vật, quái vật & cấu hình chung)
        string attrsUrl = $"{apiBaseUrl}/api/v1/game/attributes";
        using (UnityWebRequest req = UnityWebRequest.Get(attrsUrl))
        {
            req.timeout = 4;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = req.downloadHandler.text;
                    combinedJson += json;
                    anySuccess = true;
                    ParseAndApplyGameAttributes(json);
                    if (!isBackgroundPoll)
                        Debug.Log("✅ [InventoryManager] Đã tải Game Attributes từ Backend Server!");
                }
                catch (System.Exception ex)
                {
                    if (!isBackgroundPoll)
                        Debug.LogWarning("[InventoryManager] Lỗi xử lý Attributes JSON: " + ex.Message);
                }
            }
            else if (!isBackgroundPoll)
            {
                Debug.Log("[InventoryManager] Backend offline hoặc không phản hồi. Sử dụng chỉ số mặc định.");
            }
        }

        // 2. Tải Item Definitions (Vật phẩm & Thuộc tính)
        string itemsUrl = $"{apiBaseUrl}/api/v1/game/items";
        using (UnityWebRequest req = UnityWebRequest.Get(itemsUrl))
        {
            req.timeout = 4;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = req.downloadHandler.text;
                    combinedJson += "|" + json;
                    anySuccess = true;
                    ParseAndApplyItems(json);
                    if (!isBackgroundPoll)
                        Debug.Log("✅ [InventoryManager] Đã tải Item Definitions từ Backend Server!");
                }
                catch (System.Exception ex)
                {
                    if (!isBackgroundPoll)
                        Debug.LogWarning("[InventoryManager] Lỗi xử lý Items JSON: " + ex.Message);
                }
            }
            else if (!isBackgroundPoll)
            {
                Debug.Log("[InventoryManager] Backend offline. Sử dụng định nghĩa vật phẩm mặc định.");
            }
        }

        // 3. Tải Crafting Recipes (Công thức chế tạo từ Database MongoDB)
        string recipesUrl = $"{apiBaseUrl}/api/v1/game/recipes";
        using (UnityWebRequest req = UnityWebRequest.Get(recipesUrl))
        {
            req.timeout = 4;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = req.downloadHandler.text;
                    combinedJson += "|" + json;
                    anySuccess = true;
                    ParseAndApplyRecipes(json, !isBackgroundPoll);
                    if (!isBackgroundPoll)
                        Debug.Log("✅ [InventoryManager] Đã tải Crafting Recipes từ Backend Server (MongoDB)!");
                }
                catch (System.Exception ex)
                {
                    if (!isBackgroundPoll)
                        Debug.LogWarning("[InventoryManager] Lỗi xử lý Recipes JSON: " + ex.Message);
                }
            }
            else if (!isBackgroundPoll)
            {
                Debug.Log("[InventoryManager] Backend offline. Sử dụng danh sách công thức mặc định.");
            }
        }

        if (anySuccess)
        {
            bool changed = !string.IsNullOrEmpty(lastSyncedSignature) && lastSyncedSignature != combinedJson;
            lastSyncedSignature = combinedJson;
            if (forceToast || changed)
            {
                ItemPickup.ShowToast("🔄 Đã áp dụng thông số Game mới nhất từ Admin Web & Backend!");
                Debug.Log("🔄 [InventoryManager] Đã cập nhật trực tiếp thông số Game từ Admin Web!");
            }
        }
    }

    void ParseAndApplyGameAttributes(string json)
    {
        BackendGameAttrsEnvelope env = JsonUtility.FromJson<BackendGameAttrsEnvelope>(json);
        if (env == null || env.data == null) return;

        float enemyDmgMult = 1.0f;
        if (env.data.globalConfig != null)
        {
            var g = env.data.globalConfig;
            if (g.gameSpeed > 0.1f)
            {
                Time.timeScale = Mathf.Clamp(g.gameSpeed, 0.2f, 3.0f);
            }
            if (g.enemyDamageMultiplier > 0.01f)
            {
                enemyDmgMult = g.enemyDamageMultiplier;
            }
        }

        if (env.data.character != null)
        {
            var c = env.data.character;
            if (playerController == null)
                playerController = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();

            if (playerController != null)
            {
                if (c.walkSpeed > 0f) playerController.walkSpeed = c.walkSpeed;
                if (c.runSpeed > 0f) playerController.runSpeed = c.runSpeed;
                if (c.jumpForce > 0f) playerController.jumpForce = c.jumpForce;
                if (c.attackDamage > 0f) playerController.baseAttackDamage = c.attackDamage;
            }

            if (playerHealth == null)
                playerHealth = GetComponent<PlayerHealth>() ?? GetComponentInParent<PlayerHealth>() ?? FindAnyObjectByType<PlayerHealth>();

            float targetMaxHp = c.maxHP > 0f
                ? c.maxHP
                : (env.data.globalConfig != null && env.data.globalConfig.playerHealthBase > 0f ? env.data.globalConfig.playerHealthBase : 0f);

            if (playerHealth != null && targetMaxHp > 0f)
            {
                if (Mathf.Abs(playerHealth.maxHealth - targetMaxHp) > 0.01f)
                {
                    playerHealth.maxHealth = targetMaxHp;
                    playerHealth.currentHealth = targetMaxHp;
                }
            }
        }

        if (env.data.enemies != null && env.data.enemies.enemies != null)
        {
            foreach (var e in env.data.enemies.enemies)
            {
                if (!string.IsNullOrEmpty(e.enemyId))
                {
                    ServerEnemyCache[e.enemyId] = new ServerEnemyInfo
                    {
                        enemyId = e.enemyId,
                        hp = e.hp,
                        damage = e.damage * enemyDmgMult,
                        speed = e.speed,
                        attackRange = e.attackRange,
                        detectionRange = e.detectionRange,
                        dropItemId = e.dropItemId,
                        dropRate = e.dropRate
                    };
                }
            }

            foreach (var enemy in EnemyAI.ActiveEnemies)
            {
                if (enemy != null) enemy.ApplyServerConfig();
            }
        }
    }

    private static void RebuildRecipeGridPattern(RecipeDefinition recipe)
    {
        if (recipe == null || recipe.requiredItems == null) return;
        int currentCount = 0;
        Dictionary<string, int> patCounts = new Dictionary<string, int>();
        if (recipe.pattern != null && recipe.pattern.Length == 9)
        {
            for (int i = 0; i < 9; i++)
            {
                string id = recipe.pattern[i];
                if (string.IsNullOrEmpty(id)) continue;
                currentCount++;
                if (!patCounts.ContainsKey(id)) patCounts[id] = 0;
                patCounts[id]++;
            }
        }

        bool matchesAlready = patCounts.Count == recipe.requiredItems.Count;
        if (matchesAlready)
        {
            foreach (var kv in recipe.requiredItems)
            {
                if (!patCounts.TryGetValue(kv.Key, out int c) || c != kv.Value)
                {
                    matchesAlready = false;
                    break;
                }
            }
        }
        if (matchesAlready) return;

        string[] newPat = new string[9];
        int[] preferSlots = new int[] { 1, 4, 7, 0, 2, 3, 5, 6, 8 };
        int slotIdx = 0;
        foreach (var kv in recipe.requiredItems)
        {
            for (int q = 0; q < kv.Value && slotIdx < 9; q++)
            {
                newPat[preferSlots[slotIdx++]] = kv.Key;
            }
        }
        recipe.pattern = newPat;
    }

    void ParseAndApplyItems(string json)
    {
        BackendItemsEnvelope env = JsonUtility.FromJson<BackendItemsEnvelope>(json);
        if (env == null || env.data == null) return;

        foreach (var item in env.data)
        {
            if (string.IsNullOrEmpty(item.itemId)) continue;
            float healVal = item.attributes != null ? item.attributes.hpRestore : 0f;
            float dmgVal = item.attributes != null ? item.attributes.damage : 0f;

            ServerItemCache[item.itemId] = new ServerItemInfo
            {
                itemId = item.itemId,
                name = item.name,
                description = item.description,
                type = item.type,
                heal = healVal,
                damage = dmgVal
            };

            if (item.itemId == "poison_thorn_spear" && dmgVal > 0f)
            {
                spearBonusDamage = dmgVal;
            }
            else if (item.itemId == "stone_hammer" && dmgVal > 0f)
            {
                hammerBonusDamage = dmgVal;
            }

            // Cập nhật trực tiếp vào các vật phẩm đang có trong Túi Đồ của người chơi
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemId == item.itemId)
                {
                    if (!string.IsNullOrEmpty(item.name)) items[i].itemName = item.name;
                    if (!string.IsNullOrEmpty(item.description)) items[i].description = item.description;
                    if (healVal > 0f) items[i].healAmount = healVal;
                }
            }

            // Nếu vũ khí có craftingRecipe sửa từ trang CraftingResources trên AdminWeb, đồng bộ luôn vào AllRecipes
            if (item.craftingRecipe != null && item.craftingRecipe.Count > 0)
            {
                var localRecipe = AllRecipes.Find(r => r.resultId == item.itemId);
                if (localRecipe != null)
                {
                    if (!string.IsNullOrEmpty(item.name)) localRecipe.recipeName = item.name;
                    if (!string.IsNullOrEmpty(item.description)) localRecipe.shortDesc = item.description;
                    if (dmgVal > 0f) localRecipe.statsDesc = $"+{dmgVal} Sát Thương";
                    localRecipe.requiredItems.Clear();
                    foreach (var cr in item.craftingRecipe)
                    {
                        if (!string.IsNullOrEmpty(cr.materialItemId) && cr.quantityRequired > 0)
                        {
                            localRecipe.requiredItems[cr.materialItemId] = cr.quantityRequired;
                        }
                    }
                    RebuildRecipeGridPattern(localRecipe);
                }
            }
        }
    }

    void ParseAndApplyRecipes(string json, bool logSuccess = true)
    {
        BackendRecipesEnvelope env = JsonUtility.FromJson<BackendRecipesEnvelope>(json);
        if (env == null || env.data == null) return;

        int updatedCount = 0;
        foreach (var serverRecipe in env.data)
        {
            if (string.IsNullOrEmpty(serverRecipe.outputItemId)) continue;
            var localRecipe = AllRecipes.Find(r => r.resultId == serverRecipe.outputItemId);
            if (localRecipe != null)
            {
                if (!string.IsNullOrEmpty(serverRecipe.name)) localRecipe.recipeName = serverRecipe.name;
                if (!string.IsNullOrEmpty(serverRecipe.description)) localRecipe.shortDesc = serverRecipe.description;
                if (serverRecipe.outputQuantity > 0) localRecipe.resultQuantity = serverRecipe.outputQuantity;
                if (serverRecipe.ingredients != null && serverRecipe.ingredients.Count > 0)
                {
                    localRecipe.requiredItems.Clear();
                    foreach (var ing in serverRecipe.ingredients)
                    {
                        if (!string.IsNullOrEmpty(ing.itemId) && ing.quantity > 0)
                        {
                            localRecipe.requiredItems[ing.itemId] = ing.quantity;
                        }
                    }
                    RebuildRecipeGridPattern(localRecipe);
                }
                updatedCount++;
            }
        }
        if (logSuccess)
        {
            Debug.Log($"✅ [InventoryManager] Đồng bộ thành công {updatedCount}/{env.data.Count} công thức chế tạo từ Database MongoDB!");
        }
    }

    [System.Serializable]
    public class BackendGameAttrsEnvelope
    {
        public bool success;
        public string message;
        public BackendGameAttrsData data;
    }

    [System.Serializable]
    public class BackendGameAttrsData
    {
        public CharacterAttrsDto character;
        public EnemiesListWrapper enemies;
        public GlobalGameConfigDto globalConfig;
    }

    [System.Serializable]
    public class GlobalGameConfigDto
    {
        public float gameSpeed;
        public float enemyDamageMultiplier;
        public float playerHealthBase;
        public float shrinkDuration;
        public int maxPlayersPerRoom;
        public bool maintenanceMode;
        public string welcomeMessage;
    }

    [System.Serializable]
    public class CharacterAttrsDto
    {
        public float walkSpeed;
        public float runSpeed;
        public float maxHP;
        public float attackDamage;
        public float jumpForce;
    }

    [System.Serializable]
    public class EnemiesListWrapper
    {
        public List<EnemyAttrDto> enemies;
    }

    [System.Serializable]
    public class EnemyAttrDto
    {
        public string enemyId;
        public float hp;
        public float damage;
        public float speed;
        public float attackRange;
        public float detectionRange;
        public string dropItemId;
        public float dropRate;
    }

    [System.Serializable]
    public class ItemCraftRecipeEntryDto
    {
        public string materialItemId;
        public int quantityRequired;
    }

    [System.Serializable]
    public class BackendItemDto
    {
        public string itemId;
        public string name;
        public string description;
        public string type;
        public float weight;
        public int maxStack;
        public bool usable;
        public ItemAttrDto attributes;
        public List<ItemCraftRecipeEntryDto> craftingRecipe;
    }

    [System.Serializable]
    public class ItemAttrDto
    {
        public float damage;
        public float hpRestore;
        public float durability;
    }

    [System.Serializable]
    public class BackendItemsEnvelope
    {
        public bool success;
        public string message;
        public List<BackendItemDto> data;
    }

    [System.Serializable]
    public class BackendRecipeIngredientDto
    {
        public string itemId;
        public int quantity;
    }

    [System.Serializable]
    public class BackendRecipeDto
    {
        public string recipeId;
        public string name;
        public string description;
        public string outputItemId;
        public int outputQuantity;
        public List<BackendRecipeIngredientDto> ingredients;
    }

    [System.Serializable]
    public class BackendRecipesEnvelope
    {
        public bool success;
        public string message;
        public List<BackendRecipeDto> data;
    }
}
