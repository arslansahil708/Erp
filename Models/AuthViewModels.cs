using System.ComponentModel.DataAnnotations;

namespace ZnErp.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username likhein.")] public string UserName { get; set; } = "";
    [Required(ErrorMessage = "Password likhein.")] public string Password { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Maujooda password likhein.")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Naya password likhein.")] public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "Naya password dobara likhein.")] public string ConfirmPassword { get; set; } = "";
}

public class UserFormViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Username zaroori hai."), StringLength(50)] public string UserName { get; set; } = "";
    [Required(ErrorMessage = "Poora naam zaroori hai."), StringLength(120)] public string FullName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Sales;
    public bool IsActive { get; set; } = true;
    /// <summary>Naye user ke liye zaroori; edit mein khali chhorein to password nahi badlega.</summary>
    public string? Password { get; set; }
}
