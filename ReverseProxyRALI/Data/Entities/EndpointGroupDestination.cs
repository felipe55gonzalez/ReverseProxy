using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("GroupId", "DestinationId", Name = "UQ_EndpointGroupDestinations_GroupId_DestinationId", IsUnique = true)]
public partial class EndpointGroupDestination
{
    [Key]
    public int EndpointGroupDestinationId { get; set; }

    public int GroupId { get; set; }

    public int DestinationId { get; set; }

    public bool IsEnabledInGroup { get; set; }

    public DateTime AssignedAt { get; set; }

    [ForeignKey("DestinationId")]
    [InverseProperty("EndpointGroupDestinations")]
    public virtual BackendDestination Destination { get; set; } = null!;

    [ForeignKey("GroupId")]
    [InverseProperty("EndpointGroupDestinations")]
    public virtual EndpointGroup Group { get; set; } = null!;
}
