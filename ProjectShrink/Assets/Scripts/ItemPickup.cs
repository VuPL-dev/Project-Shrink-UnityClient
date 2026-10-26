using UnityEngine;

/// <summary>
/// Gắn script này vào Prefab hoặc Model item rơi ra từ quái (ví dụ: health_syringe).
/// Hỗ trợ hiệu ứng xoay, nhấp nhô nhẹ và tự động nhặt khi Player chạm vào.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    [Header("Item Configuration")]
    [Tooltip("Mã định danh của item trùng khớp với database MongoDB")]
    public string itemId = "health_syringe";
    public string itemName = "Health Syringe";
    public int quantity = 1;

    [Header("Visual Effects (Hiệu ứng nhấp nhô & xoay)")]
    public float rotationSpeed = 60f;
    public float hoverSpeed = 2f;
    public float hoverHeight = 0.1f;

    private Vector3 initialPosition;

    private void Start()
    {
        initialPosition = transform.position;

        // Đảm bảo Collider là Trigger để người chơi đi xuyên qua và nhặt được
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        // Hiệu ứng tự xoay vòng tròn quanh trục Y
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Hiệu ứng nhấp nhô lên xuống nhẹ nhàng thu hút sự chú ý
        float newY = initialPosition.y + Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Player không
        if (other.CompareTag("Player") || other.name.ToLower().Contains("player") || other.name.ToLower().Contains("ethan"))
        {
            Pickup(other.gameObject);
        }
    }

    /// <summary>
    /// Xử lý logic khi Player nhặt item
    /// </summary>
    public void Pickup(GameObject player)
    {
        Debug.Log($"[ItemPickup] Player đã nhặt: {itemName} (ID: {itemId}, Số lượng: {quantity})");

        // Gọi đồng bộ thêm item vào Database MongoDB qua API /api/inventory/add
        if (InventoryNetworkManager.Instance != null)
        {
            InventoryNetworkManager.Instance.AddItem(itemId, quantity,
                onSuccess: (inv) => Debug.Log($"[ItemPickup] Đã đồng bộ {itemId} vào Database MongoDB thành công!"),
                onError: (err) => Debug.LogWarning($"[ItemPickup] Lưu DB thất bại (có thể chưa đăng nhập): {err}")
            );
        }

        // Biến mất khỏi màn hình sau khi nhặt
        Destroy(gameObject);
    }
}
