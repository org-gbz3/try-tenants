using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Backend.Email;

public class SmtpAuthEmailSender(IOptions<SmtpOptions> options) : IAuthEmailSender
{
    public Task SendRegistrationLinkAsync(string email, string link, CancellationToken cancellationToken = default) =>
        SendAsync(
            email,
            "【try-tenants】パスキーの登録",
            $"""
            try-tenants へのご登録ありがとうございます。
            次のリンクを開き、24 時間以内にパスキーを登録してください。

            {link}

            お心当たりのない場合は、このメールを破棄してください。
            """,
            cancellationToken);

    public Task SendPasskeyResetLinkAsync(string email, string link, CancellationToken cancellationToken = default) =>
        SendAsync(
            email,
            "【try-tenants】パスキーの再設定",
            $"""
            管理者がパスキーの再設定を受け付けました。登録済みのパスキーはすべて無効になっています。
            次のリンクを開き、24 時間以内に新しいパスキーを登録してください。

            {link}
            """,
            cancellationToken);

    private async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.From))
        {
            throw new InvalidOperationException("Smtp:Host と Smtp:From を設定してください。");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(smtp.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        // 465 は暗黙の TLS、それ以外は STARTTLS を可能なら使う。開発用 Mailpit(1025)は平文で接続される。
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrEmpty(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? "", cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
