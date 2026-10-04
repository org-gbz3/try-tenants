namespace Backend.Email;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 25;

    public string From { get; set; } = "";

    // 認証情報は環境変数または User Secrets で渡し、設定ファイルには書かない。
    public string? UserName { get; set; }

    public string? Password { get; set; }
}
