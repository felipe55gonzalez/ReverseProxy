using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Table("BlockedIPs")]
[Index("IpAddress", Name = "UQ__BlockedI__30C707A3C69EA902", IsUnique = true)]
public partial class BlockedIp
{
    [Key]
    public int BlockedIpId { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string IpAddress { get; set; } = null!;

    public string? Reason { get; set; }

    public DateTime? BlockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }
}
