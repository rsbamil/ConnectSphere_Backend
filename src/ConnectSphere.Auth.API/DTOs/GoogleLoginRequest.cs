using System.ComponentModel.DataAnnotations;

namespace ConnectSphere.Auth.DTOs;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
