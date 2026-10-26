/// <summary>
/// Cấu hình địa chỉ server Backend cho Game Client Unity.
/// </summary>
public static class ApiConfig
{
    // ---- Đổi sang IP thực của server khi deploy/build ----
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

    // ---- Cloud Save/Load Endpoints ----
    public const string GAME_SAVE    = BASE_URL + "/api/v1/game/save";
    public const string GAME_LOAD    = BASE_URL + "/api/v1/game/load";

    // ---- Inventory Endpoints ----
    public const string INVENTORY        = BASE_URL + "/api/inventory";
    public const string INVENTORY_ADD    = BASE_URL + "/api/inventory/add";
    public const string INVENTORY_REMOVE = BASE_URL + "/api/inventory/remove";
    public const string INVENTORY_USE    = BASE_URL + "/api/inventory/use";
}
