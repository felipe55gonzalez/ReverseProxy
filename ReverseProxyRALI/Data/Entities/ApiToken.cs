using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("TokenValue", Name = "IX_ApiTokens_TokenValue")]
[Index("TokenValue", Name = "UQ__ApiToken__FE1B80EC0746DC84", IsUnique = true)]
public partial class ApiToken
{
    [Key]
    public int TokenId { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string TokenValue { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? Description { get; set; }

    [StringLength(150)]
    [Unicode(false)]
    public string? OwnerName { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? OwnerContact { get; set; }

    public bool IsEnabled { get; set; }

    public bool DoesExpire { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? CreatedBy { get; set; }

    [InverseProperty("TokenIdUsedNavigation")]
    public virtual ICollection<RequestLog> RequestLogs { get; set; } = new List<RequestLog>();

    [InverseProperty("Token")]
    public virtual ICollection<TokenPermission> TokenPermissions { get; set; } = new List<TokenPermission>();
}
