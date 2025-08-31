using ClassroomBookingSystem.Api.Contracts;
using ClassroomBookingSystem.Api.Services;
using ClassroomBookingSystem.Core.Entities;
using ClassroomBookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace ClassroomBookingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, JwtTokenService jwt, ILogger<AuthController> logger)
    {
        _db = db; _jwt = jwt; _logger = logger;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "تسجيل مستخدم جديد" )]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Password policy: >=8 and contains at least 3 of these: upper/lower/digit/symbol
        if (!IsStrongPassword(req.Password))
            return BadRequest(new { message = "Password must be at least 8 characters and include at least 3 of: uppercase, lowercase, digit, symbol" });

        // Department is optional for all roles; if provided, only use it when it exists
        int? resolvedDepartmentId = null;
        if (req.DepartmentId.HasValue)
        {
            var departmentExists = await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId.Value);
            if (departmentExists)
                resolvedDepartmentId = req.DepartmentId.Value;
        }
        var exists = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (exists)
            return BadRequest(new { message = "Email already in use" });

        string hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var user = new User
        {
            Email = req.Email,
            PasswordHash = hash,
            FullName = req.FullName,
            Role = req.Role,
            DepartmentId = resolvedDepartmentId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // create email confirmation token
        var emailToken = new UserToken
        {
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N"),
            Purpose = "EmailConfirmation",
            ExpiresAt = DateTime.UtcNow.AddDays(2)
        };
        _db.UserTokens.Add(emailToken);
        await _db.SaveChangesAsync();

        // issue tokens
        var access = _jwt.GenerateToken(user.Id, user.Email, user.Role);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _jwt.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            IsRevoked = false
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        return Ok(new TokenResponse { AccessToken = access, RefreshToken = refresh.Token });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "تسجيل الدخول" )]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", req.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: invalid password for email {Email}", req.Email);
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var access = _jwt.GenerateToken(user.Id, user.Email, user.Role);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _jwt.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            IsRevoked = false
        };
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);
        return Ok(new TokenResponse { AccessToken = access, RefreshToken = refresh.Token });
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "تحديث التوكن عبر RefreshToken" )]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var rt = await _db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == req.RefreshToken);
        if (rt == null || rt.IsRevoked || rt.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized();

        // rotate refresh token
        rt.IsRevoked = true;
        var newRt = new RefreshToken
        {
            UserId = rt.UserId,
            Token = _jwt.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            IsRevoked = false
        };
        _db.RefreshTokens.Add(newRt);

        var access = _jwt.GenerateToken(rt.User!.Id, rt.User.Email, rt.User.Role);
        await _db.SaveChangesAsync();

        return Ok(new TokenResponse { AccessToken = access, RefreshToken = newRt.Token });
    }

    // POST /api/auth/confirm-email
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "تأكيد البريد الإلكتروني" )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
    {
        var token = await _db.UserTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == req.Token && t.Purpose == "EmailConfirmation");
        if (token == null || token.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired token" });

        token.User!.EmailConfirmed = true;
        _db.UserTokens.Remove(token);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "إعادة تعيين كلمة المرور" )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return BadRequest(new { message = "Email not found" });

        var token = await _db.UserTokens.FirstOrDefaultAsync(t => t.UserId == user.Id && t.Purpose == "PasswordReset" && t.Token == req.Token);
        if (token == null || token.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new { message = "Invalid or expired token" });

        if (!IsStrongPassword(req.NewPassword))
            return BadRequest(new { message = "Password must be at least 8 characters and include at least 3 of: uppercase, lowercase, digit, symbol" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        _db.UserTokens.Remove(token);
        await _db.SaveChangesAsync();

        return Ok();
    }

    // POST /api/auth/change-password
    [HttpPost("change-password")]
    [Authorize]
    [SwaggerOperation(Summary = "تغيير كلمة المرور للمستخدم الحالي (مطلوب تسجيل الدخول)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Unauthorized();

        // تحقق من كلمة المرور الحالية
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect" });

        // تحقق سياسة كلمة المرور الجديدة
        if (!IsStrongPassword(req.NewPassword))
            return BadRequest(new { message = "Password must be at least 8 characters and include at least 3 of: uppercase, lowercase, digit, symbol" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} changed password successfully", user.Id);
        return Ok(new { message = "Password changed successfully" });
    }

    private bool IsStrongPassword(string password)
    {
        if (password.Length < 8) return false;
        bool hasLower = password.Any(char.IsLower);
        bool hasUpper = password.Any(char.IsUpper);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSymbol = password.Any(ch => !char.IsLetterOrDigit(ch));
        int groups = 0;
        if (hasLower) groups++;
        if (hasUpper) groups++;
        if (hasDigit) groups++;
        if (hasSymbol) groups++;
        return groups >= 3;
    }
}