using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Quản lý gọi API xác thực (Login, Register, OTP) và lưu trữ JWT Session.
/// Singleton Pattern tồn tại xuyên suốt các Scene (DontDestroyOnLoad).
/// </summary>
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    // Thông tin phiên đăng nhập hiện tại
    public string   CurrentToken { get; private set; }
    public UserInfo CurrentUser  { get; private set; }
    public bool     IsLoggedIn   => !string.IsNullOrEmpty(CurrentToken);

    // Key lưu trữ token vào bộ nhớ máy người chơi
    private const string PREF_KEY_TOKEN = "USER_JWT_TOKEN";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSavedToken();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Đăng nhập tài khoản truyền thống (Username + Password).
    /// </summary>
    public void Login(string username, string password, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        LoginRequest reqData = new LoginRequest { username = username, password = password };
        StartCoroutine(PostRequest<AuthResponse>(ApiConfig.LOGIN, reqData, (res) =>
        {
            if (res.success && !string.IsNullOrEmpty(res.token))
            {
                SaveSession(res.token, res.user);
                onSuccess?.Invoke(res);
            }
            else
            {
                onError?.Invoke(string.IsNullOrEmpty(res.message) ? "Đăng nhập thất bại!" : res.message);
            }
        }, onError));
    }

    /// <summary>
    /// Đăng ký tài khoản mới (Username, Password, Email).
    /// </summary>
    public void Register(string username, string password, string email, Action<ApiResponse<UserInfo>> onSuccess, Action<string> onError)
    {
        RegisterRequest reqData = new RegisterRequest { username = username, password = password, email = email };
        StartCoroutine(PostRequest<ApiResponse<UserInfo>>(ApiConfig.REGISTER, reqData, (res) =>
        {
            if (res.success)
            {
                onSuccess?.Invoke(res);
            }
            else
            {
                onError?.Invoke(string.IsNullOrEmpty(res.message) ? "Đăng ký thất bại!" : res.message);
            }
        }, onError));
    }

    /// <summary>
    /// Gửi mã xác nhận OTP qua Gmail.
    /// </summary>
    public void SendOtp(string email, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        SendOtpRequest reqData = new SendOtpRequest { email = email };
        StartCoroutine(PostRequest<AuthResponse>(ApiConfig.SEND_OTP, reqData, (res) =>
        {
            if (res.success)
            {
                onSuccess?.Invoke(res);
            }
            else
            {
                onError?.Invoke(string.IsNullOrEmpty(res.message) ? "Gửi OTP thất bại!" : res.message);
            }
        }, onError));
    }

    /// <summary>
    /// Xác thực OTP đăng nhập nhanh hoặc tạo tài khoản mới qua Gmail.
    /// </summary>
    public void VerifyOtp(string email, string otp, Action<AuthResponse> onSuccess, Action<string> onError)
    {
        VerifyOtpRequest reqData = new VerifyOtpRequest { email = email, otp = otp };
        StartCoroutine(PostRequest<AuthResponse>(ApiConfig.VERIFY_OTP, reqData, (res) =>
        {
            if (res.success && !string.IsNullOrEmpty(res.token))
            {
                SaveSession(res.token, res.user);
                onSuccess?.Invoke(res);
            }
            else
            {
                onError?.Invoke(string.IsNullOrEmpty(res.message) ? "Mã OTP sai hoặc hết hạn!" : res.message);
            }
        }, onError));
    }

    /// <summary>
    /// Đăng xuất - Xóa toàn bộ Token đã lưu.
    /// </summary>
    public void Logout()
    {
        CurrentToken = null;
        CurrentUser = null;
        PlayerPrefs.DeleteKey(PREF_KEY_TOKEN);
        PlayerPrefs.Save();
        Debug.Log("[AuthManager] Đã đăng xuất.");
    }

    // ==========================================
    // CÁC HÀM TIỆN ÍCH LƯU TRỮ VÀ GỬI REQUEST
    // ==========================================

    private void SaveSession(string token, UserInfo user)
    {
        CurrentToken = token;
        CurrentUser = user;
        PlayerPrefs.SetString(PREF_KEY_TOKEN, token);
        PlayerPrefs.Save();
        Debug.Log($"[AuthManager] Đăng nhập thành công! User: {user?.username} ({user?.role})");
    }

    private void LoadSavedToken()
    {
        if (PlayerPrefs.HasKey(PREF_KEY_TOKEN))
        {
            CurrentToken = PlayerPrefs.GetString(PREF_KEY_TOKEN);
            Debug.Log("[AuthManager] Đã tải phiên đăng nhập cũ từ PlayerPrefs.");
        }
    }

    /// <summary>
    /// Generic Coroutine gửi POST JSON request tới Backend bằng UnityWebRequest.
    /// </summary>
    private IEnumerator PostRequest<T>(string url, object bodyObject, Action<T> onSuccess, Action<string> onError)
    {
        string jsonBody = JsonUtility.ToJson(bodyObject);
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // Tự động đính kèm Token nếu có (dành cho API yêu cầu Auth)
            if (!string.IsNullOrEmpty(CurrentToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + CurrentToken);
            }

            request.timeout = 10; // Giới hạn 10s tránh treo game

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || 
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                // Thử parse thông báo lỗi từ server JSON nếu có
                string errorMsg = "Không thể kết nối tới server!";
                try
                {
                    if (!string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        AuthResponse errResp = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(errResp.message)) errorMsg = errResp.message;
                    }
                }
                catch
                {
                    errorMsg = request.error;
                }
                onError?.Invoke(errorMsg);
            }
            else
            {
                try
                {
                    T result = JsonUtility.FromJson<T>(request.downloadHandler.text);
                    onSuccess?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onError?.Invoke("Lỗi định dạng dữ liệu từ server: " + ex.Message);
                }
            }
        }
    }
}
