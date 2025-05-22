using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("GroupName", Name = "UQ__Endpoint__6EFCD43439D8D313", IsUnique = true)]
public class EndpointGroup
{
    public int GroupId { get; set; }
    public string GroupName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? PathPattern { get; set; }
    public int MatchOrder { get; set; }
    public bool ReqToken { get; set; }

    public virtual ICollection<EndpointGroupDestination> EndpointGroupDestinations { get; set; }
    public virtual ICollection<HourlyTrafficSummary> HourlyTrafficSummaries { get; set; }
    public virtual ICollection<TokenPermission> TokenPermissions { get; set; }
}
