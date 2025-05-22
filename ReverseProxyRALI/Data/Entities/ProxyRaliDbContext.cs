using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ReverseProxyRALI.Data.Entities;

public partial class ProxyRaliDbContext : DbContext
{
    public ProxyRaliDbContext()
    {
    }

    public ProxyRaliDbContext(DbContextOptions<ProxyRaliDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AllowedCorsOrigin> AllowedCorsOrigins { get; set; }

    public virtual DbSet<ApiToken> ApiTokens { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<BackendDestination> BackendDestinations { get; set; }

    public virtual DbSet<BlockedIp> BlockedIps { get; set; }

    public virtual DbSet<EndpointGroup> EndpointGroups { get; set; }

    public virtual DbSet<EndpointGroupDestination> EndpointGroupDestinations { get; set; }

    public virtual DbSet<HourlyTrafficSummary> HourlyTrafficSummaries { get; set; }

    public virtual DbSet<RequestLog> RequestLogs { get; set; }

    public virtual DbSet<TokenPermission> TokenPermissions { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AllowedCorsOrigin>(entity =>
        {
            entity.HasKey(e => e.OriginId).HasName("PK__AllowedC__171FA226A500044F");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("PK__ApiToken__658FEEEA3002F82D");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.DoesExpire).HasDefaultValue(true);
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditId).HasName("PK__AuditLog__A17F23983C81028E");

            entity.Property(e => e.TimestampUtc).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<BackendDestination>(entity =>
        {
            entity.HasKey(e => e.DestinationId).HasName("PK__BackendD__DB5FE4CC88B77392");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<BlockedIp>(entity =>
        {
            entity.HasKey(e => e.BlockedIpId).HasName("PK__BlockedI__951101FD030AFA20");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<EndpointGroup>(entity =>
        {
            entity.HasKey(e => e.GroupId).HasName("PK__Endpoint__149AF36A81E0EA25");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        });

        modelBuilder.Entity<EndpointGroupDestination>(entity =>
        {
            entity.HasKey(e => e.EndpointGroupDestinationId).HasName("PK__Endpoint__8C0416CC392E2B3C");

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsEnabledInGroup).HasDefaultValue(true);

            entity.HasOne(d => d.Destination).WithMany(p => p.EndpointGroupDestinations).HasConstraintName("FK_EndpointGroupDestinations_BackendDestinations");

            entity.HasOne(d => d.Group).WithMany(p => p.EndpointGroupDestinations).HasConstraintName("FK_EndpointGroupDestinations_EndpointGroups");
        });

        modelBuilder.Entity<HourlyTrafficSummary>(entity =>
        {
            entity.HasKey(e => e.SummaryId).HasName("PK__HourlyTr__DAB10E2F85F43495");

            entity.HasOne(d => d.EndpointGroup).WithMany(p => p.HourlyTrafficSummaries)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HourlyTrafficSummary_EndpointGroups");
        });

        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__RequestL__5E54864826A011F2");

            entity.HasOne(d => d.TokenIdUsedNavigation).WithMany(p => p.RequestLogs).HasConstraintName("FK_RequestLogs_ApiTokens");
        });

        modelBuilder.Entity<TokenPermission>(entity =>
        {
            entity.HasKey(e => e.TokenPermissionId).HasName("PK__TokenPer__F0CB170D272BA643");

            entity.Property(e => e.AllowedHttpMethods).HasDefaultValue("GET,POST");
            entity.Property(e => e.AssignedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Group).WithMany(p => p.TokenPermissions).HasConstraintName("FK_TokenPermissions_EndpointGroups");

            entity.HasOne(d => d.Token).WithMany(p => p.TokenPermissions).HasConstraintName("FK_TokenPermissions_ApiTokens");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
