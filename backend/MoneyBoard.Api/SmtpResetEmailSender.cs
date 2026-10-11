using System.Net;
using System.Net.Mail;

namespace MoneyBoard.Api;

public interface IResetEmailSender
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken);
}

public sealed class SmtpResetEmailSender(IConfiguration configuration) : IResetEmailSender
{
    public async Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("SMTP email delivery is not configured.");

        var port = int.TryParse(configuration["Email:SmtpPort"], out var configuredPort) ? configuredPort : 1025;
        using var message = new MailMessage(from, email)
        {
            Subject = "Reset your MoneyBoard password",
            IsBodyHtml = true,
            Body = $"<p>We received a request to reset your MoneyBoard password.</p><p><a href=\"{WebUtility.HtmlEncode(resetUrl)}\">Reset your password</a></p><p>This link expires in 30 minutes. If you did not request this, you can ignore this email.</p>"
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(configuration["Email:EnableSsl"], out var enableSsl) && enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        var username = configuration["Email:Username"];
        var password = configuration["Email:Password"];
        if (!string.IsNullOrEmpty(username)) client.Credentials = new NetworkCredential(username, password);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
    }
}
