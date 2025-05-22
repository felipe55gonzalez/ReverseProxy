using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("OriginUrl", Name = "IX_AllowedCorsOrigins_OriginUrl")]
[Index("OriginUrl", Name = "UQ__AllowedC__CEBBABAF9EA9DF02", IsUnique = true)]
public partial class AllowedCorsOrigin
{
    [Key]
    public int OriginId { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string OriginUrl { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
