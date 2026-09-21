using Microsoft.AspNetCore.Identity;

namespace BiteShare.Data;

/// <summary>
/// ASP.NET Core Identity user. DisplayName is the only field we add beyond
/// the standard Identity columns (email, password hash, etc.).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
