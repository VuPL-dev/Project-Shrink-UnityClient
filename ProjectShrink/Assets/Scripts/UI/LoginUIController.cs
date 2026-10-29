using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Quản lý toàn diện Giao diện Đăng nhập, Đăng ký và Đăng nhập nhanh qua Gmail OTP.
/// Tương thích 100% với TextMeshPro và các API sẵn có của BE_Doan.
/// </summary>
public class LoginUIController : MonoBehaviour
{
    [Header("=== 1. FORM ĐĂNG NHẬP THƯỜNG ===")]
    [SerializeField] private GameObject     panelLogin;
    [SerializeField] private TMP_InputField tmpLoginUsername;
    [SerializeField] private TMP_InputField tmpLoginPassword;
    [SerializeField] private Button         btnLogin;
    [SerializeField] private Button         btnSwitchToRegister;
    [SerializeField] private Button         btnSwitchToOtp;

    [Header("=== 2. FORM ĐĂNG KÝ TÀI KHOẢN ===")]
    [SerializeField] private GameObject     panelRegister;
    [SerializeField] private TMP_InputField tmpRegUsername;
    [SerializeField] private TMP_InputField tmpRegPassword;
    [SerializeField] private TMP_InputField tmpRegEmail;
    [SerializeField] private Button         btnSubmitRegister;
    [SerializeField] private Button         btnBackToLoginFromReg;

    [Header("=== 3. FORM GMAIL OTP ===")]
    [SerializeField] private GameObject     panelOtp;
    [SerializeField] private TMP_InputField tmpOtpEmail;
    [SerializeField] private TMP_InputField tmpOtpCode;
    [SerializeField] private Button         btnSendOtp;
    [SerializeField] private Button         btnVerifyOtp;
    [SerializeField] private Button         btnBackToLoginFromOtp;

    [Header("=== THÔNG BÁO VÀ CHUYỂN SCENE ===")]
    [SerializeField] private TMP_Text       tmpStatusMessage;
    [SerializeField] private GameObject     loadingIndicator;
    [SerializeField] private string         nextSceneName = "SampleScene";

    // Hỗ trợ dự phòng Legacy UI (nếu có dùng)
    [Header("=== DỰ PHÒNG LEGACY (Tùy chọn) ===")]
    [SerializeField] private InputField     legacyUsername;
    [SerializeField] private InputField     legacyPassword;
    [SerializeField] private Text           legacyStatusMessage;

    private void Start()
    {
        // Gán sự kiện Form Đăng nhập
        if (btnLogin != null) btnLogin.onClick.AddListener(OnLoginClicked);
        if (btnSwitchToRegister != null) btnSwitchToRegister.onClick.AddListener(() => SwitchPanel(panelRegister));
        if (btnSwitchToOtp != null) btnSwitchToOtp.onClick.AddListener(() => SwitchPanel(panelOtp));

        // Gán sự kiện Form Đăng ký
        if (btnSubmitRegister != null) btnSubmitRegister.onClick.AddListener(OnRegisterClicked);
        if (btnBackToLoginFromReg != null) btnBackToLoginFromReg.onClick.AddListener(() => SwitchPanel(panelLogin));

        // Gán sự kiện Form OTP Gmail
        if (btnSendOtp != null) btnSendOtp.onClick.AddListener(OnSendOtpClicked);
        if (btnVerifyOtp != null) btnVerifyOtp.onClick.AddListener(OnVerifyOtpClicked);
        if (btnBackToLoginFromOtp != null) btnBackToLoginFromOtp.onClick.AddListener(() => SwitchPanel(panelLogin));

        // Mặc định hiện Form Đăng nhập
        SwitchPanel(panelLogin);
        ShowLoading(false);
        SetStatus("");
    }

    // -------------------------------------------------------------
    // 1. XỬ LÝ ĐĂNG NHẬP THƯỜNG (Username + Password)
    // -------------------------------------------------------------
    private void OnLoginClicked()
    {
        string username = GetText(tmpLoginUsername, legacyUsername).Trim();
        string password = GetText(tmpLoginPassword, legacyPassword);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            SetStatus("Vui lòng nhập đầy đủ Username và Password!");
            return;
        }

        ShowLoading(true);
        SetStatus("Đang đăng nhập...");

        AuthManager.Instance.Login(username, password,
            onSuccess: (res) =>
            {
                ShowLoading(false);
                SetStatus($"Xin chào {res.user.username}! Đang vào game...");
                Invoke(nameof(LoadNextScene), 1.0f);
            },
            onError: (err) =>
            {
                ShowLoading(false);
                SetStatus($"Lỗi: {err}");
            }
        );
    }

    // -------------------------------------------------------------
    // 2. XỬ LÝ ĐĂNG KÝ (Username + Password + Email)
    // -------------------------------------------------------------
    private void OnRegisterClicked()
    {
        string username = tmpRegUsername != null ? tmpRegUsername.text.Trim() : "";
        string password = tmpRegPassword != null ? tmpRegPassword.text : "";
        string email    = tmpRegEmail != null ? tmpRegEmail.text.Trim() : "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
        {
            SetStatus("Vui lòng điền đầy đủ Username, Password và Email!");
            return;
        }

        ShowLoading(true);
        SetStatus("Đang khởi tạo tài khoản mới...");

        AuthManager.Instance.Register(username, password, email,
            onSuccess: (res) =>
            {
                ShowLoading(false);
                SetStatus("Đăng ký thành công! Hãy đăng nhập để bắt đầu.");
                
                // Tự điền sẵn username vừa tạo sang ô đăng nhập và chuyển về form login
                if (tmpLoginUsername != null) tmpLoginUsername.text = username;
                SwitchPanel(panelLogin);
            },
            onError: (err) =>
            {
                ShowLoading(false);
                SetStatus($"Đăng ký thất bại: {err}");
            }
        );
    }

    // -------------------------------------------------------------
    // 3. XỬ LÝ GỬI MÃ OTP GMAIL
    // -------------------------------------------------------------
    private void OnSendOtpClicked()
    {
        string email = tmpOtpEmail != null ? tmpOtpEmail.text.Trim() : "";
        if (string.IsNullOrEmpty(email))
        {
            SetStatus("Vui lòng nhập Email để nhận mã OTP!");
            return;
        }

        ShowLoading(true);
        SetStatus("Đang gửi mã OTP về hòm thư...");

        AuthManager.Instance.SendOtp(email,
            onSuccess: (res) =>
            {
                ShowLoading(false);
                if (res.devMode && !string.IsNullOrEmpty(res.otpDev))
                {
                    SetStatus($"[Dev Mode] Mã OTP test của bạn là: {res.otpDev}");
                    if (tmpOtpCode != null) tmpOtpCode.text = res.otpDev; // Tự điền luôn cho tiện test
                }
                else
                {
                    SetStatus("Đã gửi mã OTP! Vui lòng kiểm tra hộp thư Gmail.");
                }
            },
            onError: (err) =>
            {
                ShowLoading(false);
                SetStatus($"Lỗi gửi OTP: {err}");
            }
        );
    }

    // -------------------------------------------------------------
    // 4. XỬ LÝ XÁC NHẬN MÃ OTP ĐỂ VÀO GAME
    // -------------------------------------------------------------
    private void OnVerifyOtpClicked()
    {
        string email = tmpOtpEmail != null ? tmpOtpEmail.text.Trim() : "";
        string otp   = tmpOtpCode != null ? tmpOtpCode.text.Trim() : "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(otp))
        {
            SetStatus("Vui lòng nhập Email và mã OTP 6 số!");
            return;
        }

        ShowLoading(true);
        SetStatus("Đang xác nhận mã OTP...");

        AuthManager.Instance.VerifyOtp(email, otp,
            onSuccess: (res) =>
            {
                ShowLoading(false);
                SetStatus($"Đăng nhập OTP thành công! Xin chào {res.user.username}...");
                Invoke(nameof(LoadNextScene), 1.0f);
            },
            onError: (err) =>
            {
                ShowLoading(false);
                SetStatus($"Lỗi OTP: {err}");
            }
        );
    }

    // -------------------------------------------------------------
    // CÁC HÀM TIỆN ÍCH GIAO DIỆN
    // -------------------------------------------------------------
    private void SwitchPanel(GameObject targetPanel)
    {
        if (panelLogin != null) panelLogin.SetActive(panelLogin == targetPanel);
        if (panelRegister != null) panelRegister.SetActive(panelRegister == targetPanel);
        if (panelOtp != null) panelOtp.SetActive(panelOtp == targetPanel);
        SetStatus("");
    }

    private string GetText(TMP_InputField tmp, InputField legacy)
    {
        if (tmp != null) return tmp.text;
        if (legacy != null) return legacy.text;
        return "";
    }

    private void SetStatus(string msg)
    {
        if (tmpStatusMessage != null) tmpStatusMessage.text = msg;
        else if (legacyStatusMessage != null) legacyStatusMessage.text = msg;
        else Debug.Log($"[Status] {msg}");
    }

    private void ShowLoading(bool isLoading)
    {
        if (loadingIndicator != null) loadingIndicator.SetActive(isLoading);
        if (btnLogin != null) btnLogin.interactable = !isLoading;
        if (btnSubmitRegister != null) btnSubmitRegister.interactable = !isLoading;
        if (btnSendOtp != null) btnSendOtp.interactable = !isLoading;
        if (btnVerifyOtp != null) btnVerifyOtp.interactable = !isLoading;
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
