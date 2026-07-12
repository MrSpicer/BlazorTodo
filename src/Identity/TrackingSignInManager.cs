using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TodoList.Services.Admin;

namespace TodoList.Identity;

/// <summary>
/// A <see cref="SignInManager{TUser}"/> that records failed password sign-ins into the in-memory
/// <see cref="ILoginActivityTracker"/>. This is how the admin dashboard sees failed-login counts
/// without modifying the compiled default Identity UI: the default Login page resolves
/// <c>SignInManager&lt;ApplicationUser&gt;</c> from DI, so registering this subclass via
/// <c>.AddSignInManager&lt;TrackingSignInManager&gt;()</c> puts it in the path.
/// </summary>
public sealed class TrackingSignInManager : SignInManager<ApplicationUser>
{
	private readonly ILoginActivityTracker _loginActivity;

	public TrackingSignInManager(
		UserManager<ApplicationUser> userManager,
		IHttpContextAccessor contextAccessor,
		IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
		IOptions<IdentityOptions> optionsAccessor,
		ILogger<SignInManager<ApplicationUser>> logger,
		IAuthenticationSchemeProvider schemes,
		IUserConfirmation<ApplicationUser> confirmation,
		ILoginActivityTracker loginActivity)
		: base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
	{
		_loginActivity = loginActivity;
	}

	// The default UI calls this (userName) overload; it also covers unknown emails, which never
	// reach the TUser overload. Count genuine credential failures and lockouts — but not
	// two-factor prompts or the "email not confirmed" (NotAllowed) branch, which aren't bad creds.
	public override async Task<SignInResult> PasswordSignInAsync(
		string userName, string password, bool isPersistent, bool lockoutOnFailure)
	{
		var result = await base.PasswordSignInAsync(userName, password, isPersistent, lockoutOnFailure);

		if (result.IsLockedOut || (!result.Succeeded && !result.RequiresTwoFactor && !result.IsNotAllowed))
			_loginActivity.RecordFailure(userName);

		return result;
	}
}
