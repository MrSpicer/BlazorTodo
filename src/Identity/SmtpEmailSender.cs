using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace TodoList.Identity;

public class SmtpEmailSender : IEmailSender
{
	private readonly IConfiguration _config;
	private readonly ILogger<SmtpEmailSender> _logger;

	public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
	{
		_config = config;
		_logger = logger;
	}

	public async Task SendEmailAsync(string email, string subject, string htmlMessage)
	{
		var host = _config["Email:Smtp:Host"] ?? "localhost";
		var port = int.TryParse(_config["Email:Smtp:Port"], out var p) ? p : 1025;
		var fromAddress = _config["Email:FromAddress"] ?? "noreply@blazortodo.local";
		var fromName = _config["Email:FromName"] ?? "BlazorTodo";
		var username = _config["Email:Smtp:Username"];
		var password = _config["Email:Smtp:Password"];
		var enableSsl = bool.TryParse(_config["Email:Smtp:EnableSsl"], out var ssl) && ssl;

		using var client = new SmtpClient(host, port)
		{
			EnableSsl = enableSsl,
			DeliveryMethod = SmtpDeliveryMethod.Network,
			UseDefaultCredentials = false,
		};

		if (!string.IsNullOrWhiteSpace(username))
		{
			client.Credentials = new NetworkCredential(username, password);
		}

		using var message = new MailMessage
		{
			From = new MailAddress(fromAddress, fromName),
			Subject = subject,
			Body = htmlMessage,
			IsBodyHtml = true,
		};
		message.To.Add(email);

		try
		{
			await client.SendMailAsync(message);
			_logger.LogInformation("Sent email to {Email} (subject: {Subject})", email, subject);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send email to {Email}", email);
			throw;
		}
	}
}
