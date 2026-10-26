using System;

/// <summary>
/// Models ánh xạ JSON response chuẩn từ Backend.
/// Tương thích format: { success, message, token, user }
/// </summary>

// ---- Generic Wrapper (dùng cho mọi API) ----
[Serializable]
public class ApiResponse<T>
{
    public bool   success;
    public string message;
    public T      data;
}

// ---- Auth Models ----
[Serializable]
public class LoginRequest
{
    public string username;
    public string password;
}

[Serializable]
public class RegisterRequest
{
    public string username;
    public string password;
    public string email;
}

[Serializable]
public class SendOtpRequest
{
    public string email;
}

[Serializable]
public class VerifyOtpRequest
{
    public string email;
    public string otp;
}

/// <summary>
/// Response đặc biệt của Login/VerifyOTP — token nằm ngoài "data"
/// do cấu trúc BE trả { success, message, token, user }
/// </summary>
[Serializable]
public class AuthResponse
{
    public bool       success;
    public string     message;
    public string     token;    // JWT Token
    public UserInfo   user;
    public string     otpDev;   // Chỉ dùng khi server ở DevMode
    public bool       devMode;
}

[Serializable]
public class UserInfo
{
    public string username;
    public string email;
    public string role;         // "Player" | "Moderator" | "ShopManager" | "Admin"
}
