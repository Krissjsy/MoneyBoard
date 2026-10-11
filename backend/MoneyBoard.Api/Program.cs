using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MoneyBoard.Api;
using MoneyBoard.Api.Data;
using MoneyBoard.Api.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var connection = builder.Configuration.GetConnectionString("MoneyBoard");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set ConnectionStrings:MoneyBoard with .NET user-secrets or the ConnectionStrings__MoneyBoard environment variable.");
var signingKey = builder.Configuration["Authentication:SigningKey"] ?? throw new InvalidOperationException("Authentication:SigningKey is required.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32) throw new InvalidOperationException("Authentication:SigningKey must contain at least 32 UTF-8 bytes.");
builder.Services.AddDbContext<MoneyBoardDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddIdentityCore<MoneyBoardUser>(options => { options.User.RequireUniqueEmail = true; options.Password.RequiredLength = 10; options.Password.RequireDigit = true; options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); })
    .AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<MoneyBoardDbContext>().AddSignInManager().AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));
builder.Services.AddSingleton<IResetEmailSender, SmtpResetEmailSender>();
builder.Services.AddRateLimiter(options => options.AddPolicy("password-reset", context => RateLimitPartition.GetFixedWindowLimiter(
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
    _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true })));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = builder.Configuration["Authentication:Issuer"] ?? "MoneyBoard", ValidateAudience = true, ValidAudience = builder.Configuration["Authentication:Audience"] ?? "MoneyBoard", ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) });
builder.Services.AddAuthorization(); builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (exception is BadHttpRequestException badRequest)
    {
        context.Response.StatusCode = badRequest.StatusCode;
        await context.Response.WriteAsJsonAsync(new { error = "The request body or parameters are invalid." });
        return;
    }
    app.Logger.LogError(exception, "Request failed for {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Please try again." });
}));
app.UseCors(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", async (RegisterRequest input, UserManager<MoneyBoardUser> users) => {
    var email = input.Email?.Trim();
    var validEmail = !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && MailAddress.TryCreate(email, out var address) && address.Address == email;
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100 || !validEmail || string.IsNullOrWhiteSpace(input.Password)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["input"] = ["Name, a valid email and a password are required."] });
    var user = new MoneyBoardUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = input.Name.Trim(), Currency = "GBP", CreatedAt = DateTime.UtcNow };
    var result = await users.CreateAsync(user, input.Password);
    return result.Succeeded ? Results.Created("/api/auth/me", new { user.Id, user.Email, user.DisplayName, user.Currency }) : Results.ValidationProblem(result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
});
auth.MapPost("/login", async (LoginRequest input, UserManager<MoneyBoardUser> users, SignInManager<MoneyBoardUser> signIn, IConfiguration config) => {
    if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password)) return Results.Unauthorized();
    var user = await users.FindByEmailAsync(input.Email.Trim());
    if (user is null || (await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true)).Succeeded is false) return Results.Unauthorized();
    var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email!), new Claim(ClaimTypes.Name, user.DisplayName) };
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Authentication:SigningKey"]!)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(config["Authentication:Issuer"] ?? "MoneyBoard", config["Authentication:Audience"] ?? "MoneyBoard", claims, expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: credentials);
    return Results.Ok(new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = token.ValidTo, user = new { user.Id, user.Email, user.DisplayName, user.Currency } });
});
auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<MoneyBoardUser> users) => { var user = await users.GetUserAsync(principal); return user is null ? Results.Unauthorized() : Results.Ok(new { user.Id, user.Email, user.DisplayName, user.Currency }); }).RequireAuthorization();
auth.MapPost("/forgot-password", async (ForgotPasswordRequest input, UserManager<MoneyBoardUser> users, IResetEmailSender emailSender, IConfiguration config, IWebHostEnvironment environment, ILoggerFactory loggerFactory) => {
    if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || !MailAddress.TryCreate(input.Email.Trim(), out _))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Enter a valid email address."] });

    var user = await users.FindByEmailAsync(input.Email.Trim());
    if (user is not null)
    {
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var frontendBaseUrl = config["Frontend:BaseUrl"];
        var validFrontendUrl = Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var baseUri);
        var isLoopbackHost = validFrontendUrl && (string.Equals(baseUri!.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(baseUri.Host, out var frontendAddress) && IPAddress.IsLoopback(frontendAddress));
        if (validFrontendUrl
            && (environment.IsDevelopment()
                ? baseUri!.Scheme is "https" or "http"
                : baseUri!.Scheme == "https" && !isLoopbackHost))
        {
            var resetUrl = $"{baseUri.ToString().TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
            try { await emailSender.SendPasswordResetAsync(user.Email!, resetUrl, CancellationToken.None); }
            catch (Exception) { loggerFactory.CreateLogger("PasswordReset").LogWarning("Password reset email delivery failed."); }
        }
    }
    return Results.Ok(new { message = "If an account exists for this email address, you will receive instructions to reset your password." });
}).RequireRateLimiting("password-reset");
auth.MapPost("/reset-password", async (ResetPasswordRequest input, UserManager<MoneyBoardUser> users) => {
    if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || !MailAddress.TryCreate(input.Email.Trim(), out _) || string.IsNullOrWhiteSpace(input.Token) || input.Token.Length > 4096 || string.IsNullOrEmpty(input.NewPassword))
        return Results.BadRequest(new { error = "This reset link is invalid or has expired. Request a new one and try again." });
    var user = await users.FindByEmailAsync(input.Email.Trim());
    if (user is null) return Results.BadRequest(new { error = "This reset link is invalid or has expired. Request a new one and try again." });
    var result = await users.ResetPasswordAsync(user, input.Token, input.NewPassword);
    return result.Succeeded
        ? Results.Ok(new { message = "Your password has been reset. You can now sign in." })
        : Results.BadRequest(new { error = "This reset link is invalid or has expired, or the new password does not meet the password requirements." });
}).RequireRateLimiting("password-reset");
app.MapMoneyBoardEndpoints();
app.Run();

record RegisterRequest(string Name, string Email, string Password);
record LoginRequest(string Email, string Password);
record ForgotPasswordRequest(string Email);
record ResetPasswordRequest(string Email, string Token, string NewPassword);
