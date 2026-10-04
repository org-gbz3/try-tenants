namespace Backend.Auth;

public static class RateLimitPolicies
{
    // メール送信やトークン検証を伴う匿名 API の連続実行を抑える。
    public const string AuthEmail = "auth-email";
}
