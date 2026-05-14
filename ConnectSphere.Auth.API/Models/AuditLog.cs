using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectSphere.Auth.Models;

/// <summary>
/// Audit log for all significant admin actions.
/// Every suspension, deletion, and modification is recorded here.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    public int AuditLogId { get; set; }

    // Who performed the action
    public int ActorId { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    // Entity type affected: "User", "Post", "Comment"
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    // JSON snapshot of before/after state
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}