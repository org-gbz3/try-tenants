using System.Collections.Concurrent;
using Backend.Email;
using Microsoft.AspNetCore.WebUtilities;

namespace Backend.Tests.Infrastructure;

public sealed class CapturingEmailSender : IAuthEmailSender
{
    public ConcurrentQueue<SentEmail> Sent { get; } = new();

    public Task SendRegistrationLinkAsync(string email, string link, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(new SentEmail(email, link, SentEmailKind.Registration));
        return Task.CompletedTask;
    }

    public Task SendPasskeyResetLinkAsync(string email, string link, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(new SentEmail(email, link, SentEmailKind.PasskeyReset));
        return Task.CompletedTask;
    }

    public SentEmail Last(string email) => Sent.Last(m => m.Email == email);
}

public enum SentEmailKind
{
    Registration,
    PasskeyReset,
}

public sealed record SentEmail(string Email, string Link, SentEmailKind Kind)
{
    public (string UserId, string Token) SetupParameters
    {
        get
        {
            var query = QueryHelpers.ParseQuery(new Uri(Link).Query);
            return (query["userId"].ToString(), query["token"].ToString());
        }
    }
}
