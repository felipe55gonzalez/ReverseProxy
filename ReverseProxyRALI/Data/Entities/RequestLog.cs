using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

[Index("ClientIpAddress", Name = "IX_RequestLogs_ClientIpAddress")]
[Index("EndpointGroupAccessed", Name = "IX_RequestLogs_EndpointGroupAccessed")]
[Index("RequestId", Name = "IX_RequestLogs_RequestId")]
[Index("RequestPath", Name = "IX_RequestLogs_RequestPath")]
[Index("TimestampUtc", Name = "IX_RequestLogs_TimestampUTC")]
[Index("TokenIdUsed", Name = "IX_RequestLogs_TokenIdUsed")]
[Index("RequestId", Name = "UQ__RequestL__33A8517B9104DDE9", IsUnique = true)]
public partial class RequestLog
{
    [Key]
    public long LogId { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string RequestId { get; set; } = null!;

    [Column("TimestampUTC")]
    [Precision(3)]
    public DateTime TimestampUtc { get; set; }

    [StringLength(45)]
    [Unicode(false)]
    public string ClientIpAddress { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string HttpMethod { get; set; } = null!;

    [StringLength(2048)]
    [Unicode(false)]
    public string RequestPath { get; set; } = null!;

    public string? QueryString { get; set; }

    public string? RequestHeaders { get; set; }

    public string? RequestBodyPreview { get; set; }

    public long? RequestSizeBytes { get; set; }

    public int? TokenIdUsed { get; set; }

    public bool? WasTokenValid { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? EndpointGroupAccessed { get; set; }

    [StringLength(2048)]
    [Unicode(false)]
    public string? BackendTargetUrl { get; set; }

    public int ResponseStatusCode { get; set; }

    public string? ResponseHeaders { get; set; }

    public string? ResponseBodyPreview { get; set; }

    public long? ResponseSizeBytes { get; set; }

    public int DurationMs { get; set; }

    public string? ProxyProcessingError { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? UserAgent { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? GeoCountry { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? GeoCity { get; set; }

    [ForeignKey("TokenIdUsed")]
    [InverseProperty("RequestLogs")]
    public virtual ApiToken? TokenIdUsedNavigation { get; set; }
}
