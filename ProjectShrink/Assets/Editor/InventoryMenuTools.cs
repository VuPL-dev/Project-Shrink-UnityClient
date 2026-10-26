#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Menu công cụ trong Unity Editor:
/// Chỉ cần bấm menu 'ProjectShrink -> Tạo Màn Hình Túi Đồ' là Canvas túi đồ sẽ xuất hiện ngay trong Scene!
/// </summary>
public class InventoryMenuTools
{
    [MenuItem("ProjectShrink/Tạo Màn Hình Túi Đồ (Create Inventory Canvas)")]
    public static void CreateInventoryInScene()
    {
        // 1. Kiểm tra hoặc tạo Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas_Inventory", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas_Inventory");
        }

        // 2. Kiểm tra EventSystem
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 3. Tạo Main Panel Túi Đồ
        GameObject panelObj = new GameObject("Panel_Inventory", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvas.transform, false);
        Image panelBg = panelObj.GetComponent<Image>();
        panelBg.color = new Color(0.08f, 0.10f, 0.15f, 0.95f); // Nền tối Sci-fi
        RectTransform panelRt = panelObj.GetComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(650, 520);
        panelRt.anchoredPosition = Vector2.zero;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 4. Tiêu đề
        GameObject titleObj = new GameObject("Text_Title", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(panelObj.transform, false);
        Text title = titleObj.GetComponent<Text>();
        title.font = font;
        title.fontSize = 22;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(0.2f, 0.85f, 1f, 1f);
        title.text = "🎒 TÚI ĐỒ (INVENTORY) - [PHÍM 'I' ĐỂ MỞ/ĐÓNG]";
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.9f);
        titleRt.anchorMax = new Vector2(1, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // 5. Text sức chứa
        GameObject capObj = new GameObject("Text_Capacity", typeof(RectTransform), typeof(Text));
        capObj.transform.SetParent(panelObj.transform, false);
        Text capText = capObj.GetComponent<Text>();
        capText.font = font;
        capText.fontSize = 15;
        capText.alignment = TextAnchor.MiddleLeft;
        capText.color = Color.white;
        capText.text = "Sức chứa: 0/20 slots | Trọng lượng: 0.0/50.0 kg";
        RectTransform capRt = capObj.GetComponent<RectTransform>();
        capRt.anchorMin = new Vector2(0.06f, 0.82f);
        capRt.anchorMax = new Vector2(0.94f, 0.88f);
        capRt.offsetMin = Vector2.zero;
        capRt.offsetMax = Vector2.zero;

        // 6. Grid Container chứa các ô đồ
        GameObject gridObj = new GameObject("Grid_SlotsContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObj.transform.SetParent(panelObj.transform, false);
        GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(110, 110);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        RectTransform gridRt = gridObj.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0.06f, 0.15f);
        gridRt.anchorMax = new Vector2(0.94f, 0.82f);
        gridRt.offsetMin = Vector2.zero;
        gridRt.offsetMax = Vector2.zero;

        // Tạo sẵn 5 ô demo trực quan để người dùng nhìn thấy ngay
        for (int i = 0; i < 5; i++)
        {
            GameObject slotDemo = new GameObject($"Slot_Demo_{i + 1}", typeof(RectTransform), typeof(Image));
            slotDemo.transform.SetParent(gridObj.transform, false);
            Image slotBg = slotDemo.GetComponent<Image>();
            slotBg.color = new Color(0.16f, 0.20f, 0.26f, 0.85f);
        }

        // 7. Status Text
        GameObject statusObj = new GameObject("Text_Status", typeof(RectTransform), typeof(Text));
        statusObj.transform.SetParent(panelObj.transform, false);
        Text status = statusObj.GetComponent<Text>();
        status.font = font;
        status.fontSize = 13;
        status.alignment = TextAnchor.MiddleCenter;
        status.color = Color.green;
        status.text = "Sẵn sàng đồng bộ với MongoDB.";
        RectTransform statusRt = statusObj.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.06f, 0.03f);
        statusRt.anchorMax = new Vector2(0.75f, 0.12f);
        statusRt.offsetMin = Vector2.zero;
        statusRt.offsetMax = Vector2.zero;

        // 8. Nút Đóng (Close Button)
        GameObject closeObj = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(panelObj.transform, false);
        Image closeImg = closeObj.GetComponent<Image>();
        closeImg.color = new Color(0.85f, 0.22f, 0.22f, 1f);
        RectTransform closeRt = closeObj.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.80f, 0.03f);
        closeRt.anchorMax = new Vector2(0.94f, 0.12f);
        closeRt.offsetMin = Vector2.zero;
        closeRt.offsetMax = Vector2.zero;

        GameObject closeTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        closeTextObj.transform.SetParent(closeObj.transform, false);
        Text closeText = closeTextObj.GetComponent<Text>();
        closeText.font = font;
        closeText.fontSize = 14;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.text = "Đóng";
        RectTransform ctRt = closeTextObj.GetComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;

        // 9. Gắn Controller
        InventoryUIController controller = panelObj.AddComponent<InventoryUIController>();
        controller.inventoryPanel = panelObj;
        controller.slotsContainer = gridObj.transform;
        controller.capacityText = capText;
        controller.statusText = status;

        closeObj.GetComponent<Button>().onClick.AddListener(controller.CloseInventory);

        // Đảm bảo có InventoryNetworkManager trong Scene
        if (Object.FindFirstObjectByType<InventoryNetworkManager>() == null)
        {
            GameObject netManager = new GameObject("InventoryNetworkManager", typeof(InventoryNetworkManager));
            Undo.RegisterCreatedObjectUndo(netManager, "Create InventoryNetworkManager");
        }

        Undo.RegisterCreatedObjectUndo(panelObj, "Create Panel_Inventory");
        Selection.activeGameObject = panelObj;

        Debug.Log("<color=cyan><b>[ProjectShrink]</b> Đã tạo thành công Màn hình Túi Đồ hoàn chỉnh vào Scene!</color>");
    }
}
#endif
