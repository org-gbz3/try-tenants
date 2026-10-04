namespace Backend.Auth;

// Identity のロールはシステム全体の権限だけに使う。テナント内の権限は将来の所属情報で扱う(decisions/0003 参照)。
public static class Roles
{
    public const string SystemAdmin = "SystemAdmin";
}
