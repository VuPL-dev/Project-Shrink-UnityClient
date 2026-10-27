using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectShrink.Config
{
    /// <summary>
    /// Đồng bộ thông số nhân vật và quái vật từ Web Admin CMS qua REST API.
    /// - URL: GET /api/v1/game/attributes (hoặc cấu hình tùy chỉnh).
    /// - Khi bật game: Tự động gửi request kéo thông số mới nhất về cập nhật vào Player & Enemy.
    /// - Fallback an toàn: Nếu Web chưa bật hoặc mất mạng, tự động dùng thông số mặc định trong Inspector.
    /// </summary>
    public class GameConfigSync : MonoBehaviour
    {
        [Header("1. Cấu hình Kết Nối Web")]
        [Tooltip("Địa chỉ Backend API")]
        public string apiBaseUrl = "http://localhost:3000";
        public string endpoint = "/api/v1/game/attributes";
        public bool syncOnStart = true;
        public float requestTimeout = 3.0f;

        [Header("2. Trạng Thái Đồng Bộ (Read Only)")]
        [SerializeField] private bool isSyncing = false;
        [SerializeField] private bool isSuccess = false;
        [SerializeField] private string lastSyncMessage = "Chưa đồng bộ";

        [Header("3. Thông Số Runtime Đã Nhận")]
        public GameAttributesData currentAttributes = new GameAttributesData();

        [System.Serializable]
        public class GameAttributesData
        {
            public float playerHealthBase = 100f;
            public float walkSpeed = 0.55f;
            public float runSpeed = 1.35f;
            public float punchDamage = 15f;
            public float kickDamage = 25f;

            public float enemyHealth = 50f;
            public float enemyDamage = 15f;
            public float patrolSpeed = 0.35f;
            public float chaseSpeed = 0.85f;
            public float viewRadius = 3.5f;
            public float viewAngle = 110f;
            public float hearingRadius = 8.0f;
        }

        [System.Serializable]
        private class ApiResponseWrapper
        {
            public string status;
            public string message;
            public GameAttributesData data;
        }

        void Start()
        {
            if (syncOnStart)
            {
                StartCoroutine(FetchConfigFromWeb());
            }
        }

        public void ManualSync()
        {
            if (!isSyncing)
            {
                StartCoroutine(FetchConfigFromWeb());
            }
        }

        private IEnumerator FetchConfigFromWeb()
        {
            isSyncing = true;
            lastSyncMessage = "Đang kết nối tới Web Server...";
            string fullUrl = $"{apiBaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

            using (UnityWebRequest request = UnityWebRequest.Get(fullUrl))
            {
                request.timeout = (int)requestTimeout;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    try
                    {
                        // Hỗ trợ cả JSON bọc { data: ... } lẫn JSON phẳng
                        if (json.Contains("\"data\""))
                        {
                            ApiResponseWrapper wrapper = JsonUtility.FromJson<ApiResponseWrapper>(json);
                            if (wrapper != null && wrapper.data != null)
                            {
                                currentAttributes = wrapper.data;
                            }
                            else
                            {
                                JsonUtility.FromJsonOverwrite(json, currentAttributes);
                            }
                        }
                        else
                        {
                            JsonUtility.FromJsonOverwrite(json, currentAttributes);
                        }

                        ApplyAttributesToEntities(currentAttributes);
                        isSuccess = true;
                        lastSyncMessage = $"Đồng bộ thành công lúc {DateTime.Now:HH:mm:ss}!";
                        Debug.Log($"✅ [GameConfigSync] Đã tải và áp dụng thành công thông số từ Web: {json}");
                    }
                    catch (Exception ex)
                    {
                        isSuccess = false;
                        lastSyncMessage = $"Lỗi đọc JSON: {ex.Message}";
                        Debug.LogWarning($"⚠️ [GameConfigSync] Nhận được dữ liệu nhưng lỗi định dạng JSON. Dùng thông số mặc định.");
                    }
                }
                else
                {
                    isSuccess = false;
                    lastSyncMessage = $"Server offline ({request.error}). Đang dùng thông số mặc định.";
                    Debug.Log($"ℹ️ [GameConfigSync] Web Server chưa bật ({fullUrl}). Game tự động chạy với thông số mặc định.");
                }
            }

            isSyncing = false;
        }

        private void ApplyAttributesToEntities(GameAttributesData data)
        {
            // 1. Cập nhật Player
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.maxHealth = data.playerHealthBase;
                playerHealth.currentHealth = Mathf.Min(playerHealth.currentHealth, playerHealth.maxHealth);
            }

            PlayerController playerCtrl = FindFirstObjectByType<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.walkSpeed = data.walkSpeed;
                playerCtrl.runSpeed = data.runSpeed;
            }

            // 2. Cập nhật Enemy
            EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.maxHealth = data.enemyHealth;
                enemy.currentHealth = Mathf.Min(enemy.currentHealth, enemy.maxHealth);
                enemy.attackDamage = data.enemyDamage;
                enemy.patrolSpeed = data.patrolSpeed;
                enemy.chaseSpeed = data.chaseSpeed;
                enemy.viewRadius = data.viewRadius;
                enemy.viewAngle = data.viewAngle;
                enemy.hearingRadius = data.hearingRadius;
            }
        }
    }
}
