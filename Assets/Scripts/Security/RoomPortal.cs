using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectShrink.Security
{
    /// <summary>
    /// Cửa chuyển phòng / Cửa Thoát Hiểm:
    /// - Đặt ở đầu/cuối phòng để người chơi chuyển giữa SampleScene và Phòng 4 (Room4_Security).
    /// - Hỗ trợ khóa cửa cho đến khi Hack xong SecurityTerminal.
    /// </summary>
    public class RoomPortal : MonoBehaviour
    {
        [Header("1. Cấu Hình Chuyển Phòng")]
        public string targetSceneName = "SampleScene";
        public string portalLabel = "Cửa Chuyển Phòng";
        public bool isUnlocked = false;
        public float interactRadius = 0.20f;
        public KeyCode interactKey = KeyCode.E;

        [Header("2. Đèn Trạng Thái Cửa")]
        public Renderer doorLightRenderer;
        public Light doorPointLight;

        private bool playerNearby = false;
        private static GUIStyle s_labelStyle;

        void Start()
        {
            UpdateDoorVisual();
        }

        public void UnlockPortal()
        {
            isUnlocked = true;
            UpdateDoorVisual();
        }

        void UpdateDoorVisual()
        {
            Color col = isUnlocked ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(1f, 0.22f, 0.22f, 1f);
            if (doorPointLight != null) doorPointLight.color = col;
            if (doorLightRenderer != null && doorLightRenderer.material != null)
            {
                doorLightRenderer.material.color = col;
                if (doorLightRenderer.material.HasProperty("_BaseColor"))
                    doorLightRenderer.material.SetColor("_BaseColor", col);
                if (doorLightRenderer.material.HasProperty("_EmissionColor"))
                    doorLightRenderer.material.SetColor("_EmissionColor", col * 2f);
            }
        }

        void Update()
        {
            PlayerController pc = PlayerController.Instance;
            if (pc == null)
            {
                playerNearby = false;
                return;
            }

            float dist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(pc.transform.position.x, pc.transform.position.z)
            );
            playerNearby = dist <= interactRadius;

            if (playerNearby && isUnlocked && Input.GetKeyDown(interactKey))
            {
                if (!string.IsNullOrEmpty(targetSceneName))
                {
                    Debug.Log($"🚪 [PORTAL] Chuyển sang màn chơi: {targetSceneName}");
                    SceneManager.LoadScene(targetSceneName);
                }
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!playerNearby) return;
            if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen) return;

            if (s_labelStyle == null)
            {
                s_labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
            }

            float uiScale = Mathf.Max(1f, Screen.height / 540f);
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float vW = Screen.width / uiScale;
            float vH = Screen.height / uiScale;
            float w = 310f;
            float h = 34f;
            float x = (vW - w) * 0.5f;
            float y = vH - 96f;

            GUI.color = new Color(0.05f, 0.08f, 0.14f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (isUnlocked)
            {
                s_labelStyle.normal.textColor = new Color(0.35f, 1f, 0.55f);
                GUI.Label(new Rect(x, y, w, h), $"🚪 [Phím E] Vào {portalLabel}", s_labelStyle);
            }
            else
            {
                s_labelStyle.normal.textColor = new Color(1f, 0.42f, 0.35f);
                GUI.Label(new Rect(x, y, w, h), "🔒 Cửa Khóa! Hãy Hack Trạm Điều Khiển trước", s_labelStyle);
            }

            GUI.matrix = prev;
        }
    }
}
