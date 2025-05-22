using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("TokenId", "GroupId", Name = "UQ_TokenPermissions_TokenId_GroupId", IsUnique = true)]
public partial class TokenPermission
{
    [Key]
    public int TokenPermissionId { get; set; }

    public int TokenId { get; set; }

    public int GroupId { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string AllowedHttpMethods { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    [ForeignKey("GroupId")]
    [InverseProperty("TokenPermissions")]
    public virtual EndpointGroup Group { get; set; } = null!;

    [ForeignKey("TokenId")]
    [InverseProperty("TokenPermissions")]
    public virtual ApiToken Token { get; set; } = null!;
}
