using UnityEngine;

namespace ProjectShrink.Security
{
    /// <summary>
    /// Trạm Điều Khiển An Ninh (Phòng 4):
    /// - Người chơi lại gần và giữ phím [E] trong 1.5 giây để Hack hệ thống.
    /// - Khi Hack xong: Toàn bộ lưới Laser & Camera tắt ngay lập tức, đồng thời mở khóa Cửa Thoát Phòng 4!
    /// </summary>
    public class SecurityTerminal : MonoBehaviour
    {
        [Header("1. Cấu Hình Tương Tác")]
        public float interactRadius = 0.22f;
        public float hackDuration = 1.5f;
        public KeyCode interactKey = KeyCode.E;

        [Header("2. Tham Chiếu Visual")]
        public Renderer screenRenderer;
        public Light terminalLight;
        public RoomPortal exitPortal;

        [Header("3. Trạng Thái (Read Only)")]
        [SerializeField] private bool isHacked = false;
        [SerializeField] private float currentProgress = 0f;

        private bool playerNearby = false;
        private static GUIStyle s_promptStyle;

        void Start()
        {
            UpdateVisual(false);
        }

        void Update()
        {
            if (isHacked) return;

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

            if (playerNearby && Input.GetKey(interactKey))
            {
                currentProgress += Time.deltaTime / Mathf.Max(0.2f, hackDuration);
                if (currentProgress >= 1f)
                {
                    CompleteHack();
                }
            }
            else if (!Input.GetKey(interactKey) && currentProgress > 0f)
            {
                currentProgress = Mathf.Max(0f, currentProgress - Time.deltaTime * 0.8f);
            }
        }

        void CompleteHack()
        {
            isHacked = true;
            currentProgress = 1f;
            UpdateVisual(true);

            if (SecurityAlarmSystem.Instance != null)
            {
                SecurityAlarmSystem.Instance.HackAndDisableSecurity();
            }

            if (exitPortal != null)
            {
                exitPortal.UnlockPortal();
            }
        }

        void UpdateVisual(bool hacked)
        {
            Color col = hacked ? new Color(0.15f, 1f, 0.45f, 1f) : new Color(1f, 0.25f, 0.20f, 1f);
            if (terminalLight != null) terminalLight.color = col;
            if (screenRenderer != null && screenRenderer.material != null)
            {
                screenRenderer.material.color = col;
                if (screenRenderer.material.HasProperty("_BaseColor"))
                    screenRenderer.material.SetColor("_BaseColor", col);
                if (screenRenderer.material.HasProperty("_EmissionColor"))
                    screenRenderer.material.SetColor("_EmissionColor", col * 2.2f);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (isHacked || !playerNearby) return;
            if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen) return;

            if (s_promptStyle == null)
            {
                s_promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
                s_promptStyle.normal.textColor = new Color(0.45f, 0.95f, 1f);
            }

            float uiScale = Mathf.Max(1f, Screen.height / 540f);
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float vW = Screen.width / uiScale;
            float vH = Screen.height / uiScale;
            float w = 320f;
            float h = 44f;
            float x = (vW - w) * 0.5f;
            float y = vH - 96f;

            GUI.color = new Color(0.05f, 0.08f, 0.14f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            if (currentProgress > 0.01f)
            {
                GUI.color = new Color(0.16f, 0.88f, 0.55f, 0.85f);
                GUI.DrawTexture(new Rect(x + 6f, y + h - 10f, (w - 12f) * Mathf.Clamp01(currentProgress), 6f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            int pct = Mathf.RoundToInt(currentProgress * 100f);
            string msg = pct > 0
                ? $"💻 Đang Hack Trạm An Ninh... {pct}%"
                : "💻 [Giữ phím E] Hack Trạm Điều Khiển & Tắt Laser";
            GUI.Label(new Rect(x, y + 4f, w, 26f), msg, s_promptStyle);

            GUI.matrix = prev;
        }
    }
}
