namespace Backend.Email;

// Identity 標準の IEmailSender はパスワード再設定を前提とした文面のため、パスキー専用の送信口を定義する。
public interface IAuthEmailSender
{
    Task SendRegistrationLinkAsync(string email, string link, CancellationToken cancellationToken = default);

    Task SendPasskeyResetLinkAsync(string email, string link, CancellationToken cancellationToken = default);
}
