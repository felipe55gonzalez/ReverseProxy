using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

public partial class AuditLog
{
    [Key]
    public long AuditId { get; set; }

    [Column("TimestampUTC")]
    public DateTime TimestampUtc { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? UserId { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string EntityType { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string EntityId { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string Action { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? AffectedComponent { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string? IpAddress { get; set; }
}
