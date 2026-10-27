/// <summary>
/// Cấu hình địa chỉ server Backend.
/// Thay BASE_URL thành IP thực của server khi build game.
/// </summary>
public static class ApiConfig
{
    // ---- Đổi sang IP thực khi build release ----
    public const string BASE_URL = "http://localhost:3000";

    // ---- Auth Endpoints ----
    public const string LOGIN        = BASE_URL + "/api/auth/login";
    public const string REGISTER     = BASE_URL + "/api/auth/register";
    public const string SEND_OTP     = BASE_URL + "/api/auth/send-otp";
    public const string VERIFY_OTP   = BASE_URL + "/api/auth/verify-otp";

    // ---- Game Data Endpoints ----
    public const string GAME_ATTRS   = BASE_URL + "/api/v1/game/attributes";
    public const string QUESTS       = BASE_URL + "/api/v1/quests";
    public const string CRAFTING     = BASE_URL + "/api/v1/crafting/weapons";
}
