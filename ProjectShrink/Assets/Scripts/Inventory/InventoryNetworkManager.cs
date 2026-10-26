using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Quản lý gọi API đồng bộ Túi đồ (Inventory) với Backend MongoDB.
/// Tự động gửi JWT Bearer Token lấy từ AuthManager.
/// </summary>
public class InventoryNetworkManager : MonoBehaviour
{
    public static InventoryNetworkManager Instance { get; private set; }

    // Dữ liệu túi đồ hiện tại lưu trong RAM
    public InventoryData CurrentInventory { get; private set; }

    // Events thông báo cho UI và Gameplay khi túi đồ thay đổi
    public event Action<InventoryData> OnInventoryChanged;
    public event Action<string, int>   OnItemUsed; // (itemId, quantity)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Lấy token xác thực từ AuthManager hoặc PlayerPrefs
    /// </summary>
    private string GetToken()
    {
        if (AuthManager.Instance != null && !string.IsNullOrEmpty(AuthManager.Instance.CurrentToken))
        {
            return AuthManager.Instance.CurrentToken;
        }
        return PlayerPrefs.GetString("USER_JWT_TOKEN", "");
    }

    /// <summary>
    /// Đồng bộ tải toàn bộ túi đồ từ MongoDB qua GET /api/inventory
    /// </summary>
    public void FetchInventory(Action<InventoryData> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(GetRequest<ApiResponse<InventoryData>>(ApiConfig.INVENTORY, (response) =>
        {
            if (response != null && response.success && response.data != null)
            {
                CurrentInventory = response.data;
                OnInventoryChanged?.Invoke(CurrentInventory);
                onSuccess?.Invoke(CurrentInventory);
                Debug.Log($"[InventorySync] Tải túi đồ thành công: {CurrentInventory.items.Count} món đồ.");
            }
            else
            {
                string msg = response != null ? response.message : "Dữ liệu trả về rỗng.";
                onError?.Invoke(msg);
            }
        }, onError));
    }

    /// <summary>
    /// Thêm item vào túi đồ (Nhặt đồ từ quái hoặc sàn nhà) qua POST /api/inventory/add
    /// </summary>
    public void AddItem(string itemId, int quantity = 1, Action<InventoryData> onSuccess = null, Action<string> onError = null)
    {
        InventoryActionRequest req = new InventoryActionRequest { itemId = itemId, quantity = quantity };
        StartCoroutine(PostRequest<ApiResponse<InventoryData>>(ApiConfig.INVENTORY_ADD, req, (response) =>
        {
            if (response != null && response.success && response.data != null)
            {
                CurrentInventory = response.data;
                OnInventoryChanged?.Invoke(CurrentInventory);
                onSuccess?.Invoke(CurrentInventory);
                Debug.Log($"[InventorySync] Đã thêm vào túi: {itemId} x{quantity}");
            }
            else
            {
                string msg = response != null ? response.message : "Lỗi thêm đồ.";
                onError?.Invoke(msg);
            }
        }, onError));
    }

    /// <summary>
    /// Sử dụng item trong túi đồ (ví dụ: cắn bình máu, dùng thẻ từ) qua POST /api/inventory/use
    /// </summary>
    public void UseItem(string itemId, int quantity = 1, Action<InventoryData> onSuccess = null, Action<string> onError = null)
    {
        InventoryActionRequest req = new InventoryActionRequest { itemId = itemId, quantity = quantity };
        StartCoroutine(PostRequest<ApiResponse<InventoryData>>(ApiConfig.INVENTORY_USE, req, (response) =>
        {
            if (response != null && response.success && response.data != null)
            {
                CurrentInventory = response.data;
                OnInventoryChanged?.Invoke(CurrentInventory);
                OnItemUsed?.Invoke(itemId, quantity);
                onSuccess?.Invoke(CurrentInventory);
                Debug.Log($"[InventorySync] Sử dụng thành công: {itemId} x{quantity}");
            }
            else
            {
                string msg = response != null ? response.message : "Không thể sử dụng item.";
                onError?.Invoke(msg);
            }
        }, onError));
    }

    /// <summary>
    /// Xóa / vứt item khỏi túi đồ qua POST /api/inventory/remove
    /// </summary>
    public void RemoveItem(string itemId, int quantity = 1, Action<InventoryData> onSuccess = null, Action<string> onError = null)
    {
        InventoryActionRequest req = new InventoryActionRequest { itemId = itemId, quantity = quantity };
        StartCoroutine(PostRequest<ApiResponse<InventoryData>>(ApiConfig.INVENTORY_REMOVE, req, (response) =>
        {
            if (response != null && response.success && response.data != null)
            {
                CurrentInventory = response.data;
                OnInventoryChanged?.Invoke(CurrentInventory);
                onSuccess?.Invoke(CurrentInventory);
            }
            else
            {
                string msg = response != null ? response.message : "Lỗi xóa item.";
                onError?.Invoke(msg);
            }
        }, onError));
    }

    // ==========================================
    // HTTP UTILITIES (UnityWebRequest)
    // ==========================================

    private IEnumerator GetRequest<T>(string url, Action<T> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            string token = GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
            {
                onError?.Invoke($"[Lỗi mạng {req.responseCode}]: {req.error}");
            }
            else
            {
                try
                {
                    T result = JsonUtility.FromJson<T>(req.downloadHandler.text);
                    onSuccess?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onError?.Invoke("Lỗi parse JSON: " + ex.Message);
                }
            }
        }
    }

    private IEnumerator PostRequest<T>(string url, object body, Action<T> onSuccess, Action<string> onError)
    {
        string json = JsonUtility.ToJson(body);
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] raw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            string token = GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
            {
                onError?.Invoke($"[Lỗi {req.responseCode}]: {req.downloadHandler.text}");
            }
            else
            {
                try
                {
                    T result = JsonUtility.FromJson<T>(req.downloadHandler.text);
                    onSuccess?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onError?.Invoke("Lỗi parse JSON: " + ex.Message);
                }
            }
        }
    }
}
