using System.Collections.Generic;
using UnityEngine;
using ProjectShrink.AI;

namespace ProjectShrink.Security
{
    /// <summary>
    /// Quản lý trạng thái An Ninh toàn phòng (Phòng 4 - Khu An Ninh & Camera):
    /// - Điều phối báo động giữa LaserScanner, SecurityCamera và SecurityDroneAI.
    /// - Hiển thị HUD trạng thái bảo mật (Zero GC Allocation trong OnGUI).
    /// </summary>
    public class SecurityAlarmSystem : MonoBehaviour
    {
        public static SecurityAlarmSystem Instance { get; private set; }

        [Header("1. Cấu Hình Báo Động")]
        public float alarmDuration = 6.0f;
        public string roomTitle = "PHÒNG 4: KHU AN NINH & CAMERA";

        [Header("2. Trạng Thái (Read Only)")]
        [SerializeField] private bool isAlarmActive = false;
        [SerializeField] private bool isSecurityHacked = false;
        [SerializeField] private float alarmTimer = 0f;
        [SerializeField] private Vector3 lastAlertPosition;

        private readonly List<LaserScanner> registeredLasers = new List<LaserScanner>();
        private readonly List<SecurityCameraController> registeredCameras = new List<SecurityCameraController>();
        private readonly List<SecurityDroneAI> registeredDrones = new List<SecurityDroneAI>();

        private static GUIStyle s_titleStyle;
        private static GUIStyle s_statusStyle;
        private static GUIStyle s_hintStyle;
        private int lastCountdownInt = -1;
        private string cachedAlarmText = "";

        public bool IsAlarmActive => isAlarmActive && !isSecurityHacked;
        public bool IsSecurityHacked => isSecurityHacked;
        public Vector3 LastAlertPosition => lastAlertPosition;

        void Awake()
        {
            Instance = this;
            isAlarmActive = false;
            isSecurityHacked = false;
            alarmTimer = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterLaser(LaserScanner laser)
        {
            if (laser != null && !registeredLasers.Contains(laser))
                registeredLasers.Add(laser);
        }

        public void RegisterCamera(SecurityCameraController cam)
        {
            if (cam != null && !registeredCameras.Contains(cam))
                registeredCameras.Add(cam);
        }

        public void RegisterDrone(SecurityDroneAI drone)
        {
            if (drone != null && !registeredDrones.Contains(drone))
                registeredDrones.Add(drone);
        }

        void Update()
        {
            if (isAlarmActive && !isSecurityHacked)
            {
                alarmTimer -= Time.deltaTime;
                if (alarmTimer <= 0f)
                {
                    isAlarmActive = false;
                    alarmTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Kích hoạt báo động tại tọa độ phát hiện Player, gọi các Drone bay tới xử lý.
        /// </summary>
        public void TriggerAlarm(Vector3 targetPos, string sourceName = "Cảm biến An Ninh")
        {
            if (isSecurityHacked) return;

            bool newlyTriggered = !isAlarmActive;
            isAlarmActive = true;
            alarmTimer = alarmDuration;
            lastAlertPosition = targetPos;

            SoundEmitter.Emit(targetPos, 3.5f, gameObject);

            for (int i = 0; i < registeredDrones.Count; i++)
            {
                SecurityDroneAI drone = registeredDrones[i];
                if (drone != null && drone.IsAlive)
                {
                    drone.NotifySecurityAlert(targetPos);
                }
            }

            if (newlyTriggered)
            {
                Debug.Log($"🚨 [PHÒNG 4 AN NINH] Báo động kích hoạt bởi [{sourceName}] tại {targetPos}!");
            }
        }

        /// <summary>
        /// Vô hiệu hóa toàn bộ lưới Laser & Camera khi người chơi Hack thành công Trạm Điều Khiển.
        /// </summary>
        public void HackAndDisableSecurity()
        {
            if (isSecurityHacked) return;

            isSecurityHacked = true;
            isAlarmActive = false;
            alarmTimer = 0f;

            for (int i = 0; i < registeredLasers.Count; i++)
            {
                if (registeredLasers[i] != null)
                    registeredLasers[i].SetLaserEnabled(false);
            }

            for (int i = 0; i < registeredCameras.Count; i++)
            {
                if (registeredCameras[i] != null)
                    registeredCameras[i].SetCameraOffline();
            }

            Debug.Log("✅ [PHÒNG 4 AN NINH] Hệ thống Laser & Camera đã bị vô hiệu hóa hoàn toàn!");
        }

        private static void EnsureStyles()
        {
            if (s_titleStyle == null)
            {
                s_titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    fontSize = 13
                };
                s_titleStyle.normal.textColor = new Color(0.85f, 0.94f, 1f);
            }

            if (s_statusStyle == null)
            {
                s_statusStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
            }

            if (s_hintStyle == null)
            {
                s_hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontStyle = FontStyle.Normal,
                    fontSize = 11
                };
                s_hintStyle.normal.textColor = new Color(0.82f, 0.86f, 0.92f);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (InventoryManager.Instance != null && InventoryManager.Instance.isInventoryOpen) return;

            EnsureStyles();

            float uiScale = Mathf.Max(1f, Screen.height / 540f);
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float vW = Screen.width / uiScale;
            float boxW = 385f;
            float boxH = 78f;
            float boxX = vW - boxW - 18f;
            float boxY = 18f;

            // Khung nền tối chuyên nghiệp
            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

            Color borderColor = isSecurityHacked
                ? new Color(0.2f, 0.92f, 0.52f, 1f)
                : (isAlarmActive ? new Color(1f, 0.22f, 0.22f, 1f) : new Color(0.28f, 0.70f, 1f, 0.95f));

            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxH - 2f, boxW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(boxX + 12f, boxY + 6f, boxW - 24f, 20f), $"🛡️ {roomTitle}", s_titleStyle);

            if (isSecurityHacked)
            {
                s_statusStyle.normal.textColor = new Color(0.32f, 1f, 0.58f);
                GUI.Label(new Rect(boxX + 12f, boxY + 26f, boxW - 24f, 20f), "✅ ĐÃ HACK AN NINH — CỬA THOÁT ĐÃ MỞ", s_statusStyle);
                GUI.Label(new Rect(boxX + 12f, boxY + 46f, boxW - 24f, 20f), "👉 Tiến tới Cửa Thoát (Xanh Lá) nhấn [E] để qua phòng", s_hintStyle);
            }
            else if (isAlarmActive)
            {
                int sec = Mathf.CeilToInt(alarmTimer);
                if (sec != lastCountdownInt)
                {
                    lastCountdownInt = sec;
                    cachedAlarmText = $"🚨 BÁO ĐỘNG! DRONE ĐANG TRUY QUÉT ({sec}s)";
                }
                s_statusStyle.normal.textColor = new Color(1f, 0.32f, 0.32f);
                GUI.Label(new Rect(boxX + 12f, boxY + 26f, boxW - 24f, 20f), cachedAlarmText, s_statusStyle);
                GUI.Label(new Rect(boxX + 12f, boxY + 46f, boxW - 24f, 20f), "⚠️ Né tia Laser [LeftShift] hoặc nấp sau Kệ Sắt!", s_hintStyle);
            }
            else
            {
                s_statusStyle.normal.textColor = new Color(1f, 0.80f, 0.28f);
                GUI.Label(new Rect(boxX + 12f, boxY + 26f, boxW - 24f, 20f), "🎯 Nhiệm vụ: Hack Trạm Điều Khiển cuối phòng", s_statusStyle);
                GUI.Label(new Rect(boxX + 12f, boxY + 46f, boxW - 24f, 20f), "⚡ Tránh tia Laser [Space nhảy / LeftShift lướt né]", s_hintStyle);
            }

            GUI.matrix = prevMatrix;
        }
    }
}
