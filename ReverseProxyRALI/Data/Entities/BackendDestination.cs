using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("Address", Name = "IX_BackendDestinations_Address")]
[Index("Address", Name = "UQ__BackendD__7D0C3F32702B0E61", IsUnique = true)]
public partial class BackendDestination
{
    [Key]
    public int DestinationId { get; set; }

    [StringLength(2048)]
    [Unicode(false)]
    public string Address { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? FriendlyName { get; set; }

    public bool IsEnabled { get; set; }

    [StringLength(2048)]
    [Unicode(false)]
    public string? HealthCheckPath { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Destination")]
    public virtual ICollection<EndpointGroupDestination> EndpointGroupDestinations { get; set; } = new List<EndpointGroupDestination>();
}
