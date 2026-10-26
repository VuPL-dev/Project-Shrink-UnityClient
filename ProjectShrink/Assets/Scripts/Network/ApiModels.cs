using System;
using System.Collections.Generic;

/// <summary>
/// Models ánh xạ JSON response chuẩn từ Backend.
/// Tương thích format: { success, message, data }
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
    public string id;           // UUID
    public string username;
    public string email;
    public string role;         // "Player" | "Moderator" | "ShopManager" | "Admin"
}

// ---- Inventory Models ----
[Serializable]
public class InventoryItemDto
{
    public string itemId;
    public int    quantity;
    public int    slotIndex;
}

[Serializable]
public class InventoryData
{
    public string userId;
    public int    capacity = 20;
    public float  maxWeight = 50f;
    public float  currentWeight = 0f;
    public List<InventoryItemDto> items = new List<InventoryItemDto>();
}

[Serializable]
public class InventoryActionRequest
{
    public string itemId;
    public int    quantity = 1;
}

[Serializable]
public class MoveItemRequest
{
    public int fromSlot;
    public int toSlot;
}
