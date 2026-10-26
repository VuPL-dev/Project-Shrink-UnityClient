using UnityEngine;

/// <summary>
/// Script nhặt vật phẩm (Hồi máu từ Ống Tiêm - Health Syringe):
/// - Tự động phát hiện va chạm với nhân vật (Player).
/// - Hồi phục máu cho PlayerHealth và hiển thị thông báo.
/// - Có hiệu ứng xoay tròn và nhấp nhô (Floating / Bobbing) sinh động.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemPickup : MonoBehaviour
{
    public enum ItemType { Health, Ammo, Key }

    [Header("1. Loại Vật Phẩm & Chỉ Số")]
    public ItemType itemType = ItemType.Health;
    [Tooltip("Lượng máu hồi phục khi nhặt")]
    public float healAmount = 30f;

    [Header("2. Hiệu Ứng Trôi & Xoay (Floating / Bobbing)")]
    public bool enableFloating = true;
    public float floatAmplitude = 0.015f;
    public float floatSpeed = 3f;
    public float rotationSpeed = 75f;

    private Vector3 basePosition;
    private bool isPickedUp = false;

    void Start()
    {
        basePosition = transform.position;

        // Đảm bảo Collider là Trigger để nhân vật đi xuyên qua nhặt được
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void Update()
    {
        if (enableFloating && !isPickedUp)
        {
            // Xoay quanh trục Y
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            // Nhấp nhô nhẹ nhàng
            float newY = basePosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isPickedUp) return;

        // Tìm component PlayerHealth trên đối tượng hoặc cha của nó
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        // Nhận diện qua Tag "Player" hoặc có PlayerHealth
        if (playerHealth != null || other.CompareTag("Player"))
        {
            // 1. Nếu có Túi đồ (InventoryManager), ưu tiên nhặt vào túi đồ và đồng bộ DB
            if (InventoryManager.Instance != null)
            {
                bool added = InventoryManager.Instance.AddItem(
                    "health_syringe", 
                    "Ống Tiêm Sinh Lực", 
                    "Ống tiêm chứa dung dịch nano sinh học, hồi phục 30 HP tức thì cho cơ thể thu nhỏ.", 
                    1, 
                    healAmount, 
                    "Consumable"
                );

                if (added)
                {
                    isPickedUp = true;
                    Debug.Log($"🎒 [TÚI ĐỒ] Đã nhặt Ống Tiêm Sinh Lực vào túi đồ! Bấm B hoặc Tab để mở túi đồ.");
                    Destroy(gameObject);
                    return;
                }
            }

            // 2. Nếu túi đồ đầy hoặc không có Túi đồ, hồi máu trực tiếp nếu máu chưa đầy
            if (playerHealth != null)
            {
                if (playerHealth.currentHealth < playerHealth.maxHealth)
                {
                    isPickedUp = true;
                    playerHealth.Heal(healAmount);
                    Debug.Log($"💊 [ITEM] Đã dùng ngay Ống Tiêm! +{healAmount} HP (Hiện tại: {playerHealth.currentHealth}/{playerHealth.maxHealth})");
                    Destroy(gameObject);
                }
                else
                {
                    Debug.Log("💊 [ITEM] Túi đầy hoặc Máu nhân vật đang 100%.");
                }
            }
            else
            {
                isPickedUp = true;
                Destroy(gameObject);
            }
        }
    }
}
