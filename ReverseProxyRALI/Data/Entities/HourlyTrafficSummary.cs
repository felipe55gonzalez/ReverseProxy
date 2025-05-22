using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Table("HourlyTrafficSummary")]
[Index("HourUtc", "EndpointGroupId", Name = "IX_HourlyTrafficSummary_HourUTC_GroupId")]
[Index("HourUtc", "EndpointGroupId", "HttpMethod", Name = "UQ_HourlyTraffic_Hour_Group_Method", IsUnique = true)]
public partial class HourlyTrafficSummary
{
    [Key]
    public long SummaryId { get; set; }

    [Column("HourUTC")]
    public DateTime HourUtc { get; set; }

    public int EndpointGroupId { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string HttpMethod { get; set; } = null!;

    public int? RequestCount { get; set; }

    public int? ErrorCount4xx { get; set; }

    public int? ErrorCount5xx { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? AverageDurationMs { get; set; }

    [Column("P95DurationMs", TypeName = "decimal(10, 2)")]
    public decimal? P95durationMs { get; set; }

    public long? TotalRequestBytes { get; set; }

    public long? TotalResponseBytes { get; set; }

    public int? UniqueClientIps { get; set; }

    [ForeignKey("EndpointGroupId")]
    [InverseProperty("HourlyTrafficSummaries")]
    public virtual EndpointGroup EndpointGroup { get; set; } = null!;
}
