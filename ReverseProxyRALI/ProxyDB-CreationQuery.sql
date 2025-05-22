-- Verificar si la base de datos ProxyDB existe y crearla si no
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ProxyDB')
BEGIN
    CREATE DATABASE ProxyDB;
END
GO

USE ProxyDB;
GO

-- 1. Tabla: EndpointGroups
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EndpointGroups]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EndpointGroups (
        GroupId INT IDENTITY(1,1) PRIMARY KEY,
        GroupName VARCHAR(100) UNIQUE NOT NULL,
        PathPattern VARCHAR(512) NOT NULL, 
        MatchOrder INT NOT NULL DEFAULT 0,  
        Description NVARCHAR(MAX) NULL,
        ReqToken BIT NOT NULL DEFAULT 1, -- Columna para indicar si el grupo requiere token
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    CREATE INDEX IX_EndpointGroups_PathPattern ON dbo.EndpointGroups(PathPattern);
    PRINT 'Tabla EndpointGroups creada.';
END
ELSE
BEGIN
    PRINT 'Tabla EndpointGroups ya existe. Verificando columnas...';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'PathPattern' AND Object_ID = Object_ID(N'dbo.EndpointGroups'))
    BEGIN ALTER TABLE dbo.EndpointGroups ADD PathPattern VARCHAR(512) NOT NULL DEFAULT '/api/default_placeholder/*'; PRINT 'Columna PathPattern agregada a EndpointGroups.'; END
    ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'PathPattern' AND Object_ID = Object_ID(N'dbo.EndpointGroups') AND is_nullable = 1) 
    BEGIN
        UPDATE dbo.EndpointGroups SET PathPattern = '/api/temp_default/*' WHERE PathPattern IS NULL;
        ALTER TABLE dbo.EndpointGroups ALTER COLUMN PathPattern VARCHAR(512) NOT NULL;
        PRINT 'Columna PathPattern en EndpointGroups ahora es NOT NULL.';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'MatchOrder' AND Object_ID = Object_ID(N'dbo.EndpointGroups'))
    BEGIN ALTER TABLE dbo.EndpointGroups ADD MatchOrder INT NOT NULL DEFAULT 0; PRINT 'Columna MatchOrder agregada a EndpointGroups.'; END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'ReqToken' AND Object_ID = Object_ID(N'dbo.EndpointGroups'))
    BEGIN ALTER TABLE dbo.EndpointGroups ADD ReqToken BIT NOT NULL DEFAULT 1; PRINT 'Columna ReqToken agregada a EndpointGroups.'; END
END
GO

-- 2. Tabla: ApiTokens
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ApiTokens]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.ApiTokens (
        TokenId INT IDENTITY(1,1) PRIMARY KEY,
        TokenValue VARCHAR(512) UNIQUE NOT NULL,
        Description VARCHAR(255) NULL,
        OwnerName VARCHAR(150) NULL,
        OwnerContact VARCHAR(255) NULL,
        IsEnabled BIT NOT NULL DEFAULT 1,
        DoesExpire BIT NOT NULL DEFAULT 1, 
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        ExpiresAt DATETIME2 NULL, 
        LastUsedAt DATETIME2 NULL,
        CreatedBy VARCHAR(100) NULL
    );
    CREATE INDEX IX_ApiTokens_TokenValue ON dbo.ApiTokens(TokenValue);
    PRINT 'Tabla ApiTokens creada.';
END
ELSE
    PRINT 'Tabla ApiTokens ya existe.';
GO

-- 3. Tabla: TokenPermissions
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TokenPermissions]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.TokenPermissions (
        TokenPermissionId INT IDENTITY(1,1) PRIMARY KEY,
        TokenId INT NOT NULL,
        GroupId INT NOT NULL,
        AllowedHttpMethods VARCHAR(100) NOT NULL DEFAULT 'GET,POST',
        AssignedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        CONSTRAINT FK_TokenPermissions_ApiTokens FOREIGN KEY (TokenId) REFERENCES dbo.ApiTokens(TokenId) ON DELETE CASCADE,
        CONSTRAINT FK_TokenPermissions_EndpointGroups FOREIGN KEY (GroupId) REFERENCES dbo.EndpointGroups(GroupId) ON DELETE CASCADE,
        CONSTRAINT UQ_TokenPermissions_TokenId_GroupId UNIQUE (TokenId, GroupId)
    );
    PRINT 'Tabla TokenPermissions creada.';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'AllowedHttpMethods' AND Object_ID = Object_ID(N'dbo.TokenPermissions'))
    BEGIN
        ALTER TABLE dbo.TokenPermissions
        ADD AllowedHttpMethods VARCHAR(100) NOT NULL DEFAULT 'GET,POST';
        PRINT 'Columna AllowedHttpMethods agregada a TokenPermissions.';
    END
    ELSE
        PRINT 'Tabla TokenPermissions ya existe y la columna AllowedHttpMethods también.';
END
GO

-- 4. Tabla: RequestLogs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RequestLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.RequestLogs (
        LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
        RequestId VARCHAR(100) UNIQUE NOT NULL,
        TimestampUTC DATETIME2(3) NOT NULL,
        ClientIpAddress VARCHAR(45) NOT NULL,
        HttpMethod VARCHAR(10) NOT NULL,
        RequestPath VARCHAR(2048) NOT NULL,
        QueryString NVARCHAR(MAX) NULL,
        RequestHeaders NVARCHAR(MAX) NULL,
        RequestBodyPreview NVARCHAR(MAX) NULL,
        RequestSizeBytes BIGINT NULL,
        TokenIdUsed INT NULL,
        WasTokenValid BIT NULL,
        EndpointGroupAccessed VARCHAR(100) NULL,
        BackendTargetUrl VARCHAR(2048) NULL,
        ResponseStatusCode INT NOT NULL,
        ResponseHeaders NVARCHAR(MAX) NULL,
        ResponseBodyPreview NVARCHAR(MAX) NULL,
        ResponseSizeBytes BIGINT NULL,
        DurationMs INT NOT NULL,
        ProxyProcessingError NVARCHAR(MAX) NULL,
        UserAgent VARCHAR(512) NULL,
        GeoCountry VARCHAR(100) NULL, 
        GeoCity VARCHAR(100) NULL,  
        CONSTRAINT FK_RequestLogs_ApiTokens FOREIGN KEY (TokenIdUsed) REFERENCES dbo.ApiTokens(TokenId)
    );
    CREATE INDEX IX_RequestLogs_TimestampUTC ON dbo.RequestLogs(TimestampUTC);
    CREATE INDEX IX_RequestLogs_RequestId ON dbo.RequestLogs(RequestId);
    CREATE INDEX IX_RequestLogs_ClientIpAddress ON dbo.RequestLogs(ClientIpAddress);
    CREATE INDEX IX_RequestLogs_RequestPath ON dbo.RequestLogs(RequestPath);
    CREATE INDEX IX_RequestLogs_TokenIdUsed ON dbo.RequestLogs(TokenIdUsed);
    CREATE INDEX IX_RequestLogs_EndpointGroupAccessed ON dbo.RequestLogs(EndpointGroupAccessed);
    PRINT 'Tabla RequestLogs creada.';
END
ELSE
BEGIN
    PRINT 'Tabla RequestLogs ya existe. Verificando columnas adicionales...';
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'RequestSizeBytes' AND Object_ID = Object_ID(N'dbo.RequestLogs'))
    BEGIN ALTER TABLE dbo.RequestLogs ADD RequestSizeBytes BIGINT NULL; PRINT 'Columna RequestSizeBytes agregada a RequestLogs.'; END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'ResponseSizeBytes' AND Object_ID = Object_ID(N'dbo.RequestLogs'))
    BEGIN ALTER TABLE dbo.RequestLogs ADD ResponseSizeBytes BIGINT NULL; PRINT 'Columna ResponseSizeBytes agregada a RequestLogs.'; END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'GeoCountry' AND Object_ID = Object_ID(N'dbo.RequestLogs'))
    BEGIN ALTER TABLE dbo.RequestLogs ADD GeoCountry VARCHAR(100) NULL; PRINT 'Columna GeoCountry agregada a RequestLogs.'; END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'GeoCity' AND Object_ID = Object_ID(N'dbo.RequestLogs'))
    BEGIN ALTER TABLE dbo.RequestLogs ADD GeoCity VARCHAR(100) NULL; PRINT 'Columna GeoCity agregada a RequestLogs.'; END
END
GO

-- 5. Tabla: AuditLogs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.AuditLogs (
        AuditId BIGINT IDENTITY(1,1) PRIMARY KEY,
        TimestampUTC DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UserId VARCHAR(100) NULL, 
        EntityType VARCHAR(100) NOT NULL,
        EntityId VARCHAR(100) NOT NULL, 
        Action VARCHAR(255) NOT NULL, 
        OldValues NVARCHAR(MAX) NULL, 
        NewValues NVARCHAR(MAX) NULL, 
        AffectedComponent VARCHAR(100) NULL, 
        IpAddress VARCHAR(45) NULL
    );
    PRINT 'Tabla AuditLogs creada.';
END
ELSE
BEGIN
    PRINT 'Tabla AuditLogs ya existe. Verificando columna Action...';
    IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'Action' AND Object_ID = Object_ID(N'dbo.AuditLogs'))
    BEGIN
        DECLARE @ActionMaxLength INT;
        SELECT @ActionMaxLength = max_length 
        FROM sys.columns 
        WHERE Name = N'Action' AND Object_ID = Object_ID(N'dbo.AuditLogs');

        IF @ActionMaxLength < 255
        BEGIN
            ALTER TABLE dbo.AuditLogs ALTER COLUMN Action VARCHAR(255) NOT NULL;
            PRINT 'Columna Action en AuditLogs aumentada a VARCHAR(255).';
        END
        ELSE
            PRINT 'Columna Action en AuditLogs ya tiene un tamaño adecuado.';
    END
    ELSE 
    BEGIN
         ALTER TABLE dbo.AuditLogs ADD Action VARCHAR(255) NOT NULL DEFAULT 'Unknown'; 
         PRINT 'Columna Action agregada a AuditLogs con VARCHAR(255).';
    END
END
GO

-- 6. Tabla: BlockedIPs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BlockedIPs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.BlockedIPs (
        BlockedIpId INT IDENTITY(1,1) PRIMARY KEY,
        IpAddress VARCHAR(45) UNIQUE NOT NULL,
        Reason NVARCHAR(MAX) NULL,
        BlockedUntil DATETIME2 NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    PRINT 'Tabla BlockedIPs creada.';
END
ELSE
    PRINT 'Tabla BlockedIPs ya existe.';
GO

-- 7. Tabla: AllowedCorsOrigins
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AllowedCorsOrigins]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.AllowedCorsOrigins (
        OriginId INT IDENTITY(1,1) PRIMARY KEY,
        OriginUrl VARCHAR(512) UNIQUE NOT NULL,
        Description VARCHAR(255) NULL,
        IsEnabled BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    CREATE INDEX IX_AllowedCorsOrigins_OriginUrl ON dbo.AllowedCorsOrigins(OriginUrl);
    PRINT 'Tabla AllowedCorsOrigins creada.';
END
ELSE
    PRINT 'Tabla AllowedCorsOrigins ya existe.';
GO

-- 8. Tabla: HourlyTrafficSummary
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HourlyTrafficSummary]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.HourlyTrafficSummary (
        SummaryId BIGINT IDENTITY(1,1) PRIMARY KEY,
        HourUTC DATETIME2 NOT NULL, 
        EndpointGroupId INT NOT NULL,
        HttpMethod VARCHAR(10) NOT NULL,
        RequestCount INT NULL,
        ErrorCount4xx INT NULL,
        ErrorCount5xx INT NULL,
        AverageDurationMs DECIMAL(10,2) NULL,
        P95DurationMs DECIMAL(10,2) NULL, 
        TotalRequestBytes BIGINT NULL,
        TotalResponseBytes BIGINT NULL,
        UniqueClientIps INT NULL,
        CONSTRAINT FK_HourlyTrafficSummary_EndpointGroups FOREIGN KEY (EndpointGroupId) REFERENCES dbo.EndpointGroups(GroupId),
        CONSTRAINT UQ_HourlyTraffic_Hour_Group_Method UNIQUE (HourUTC, EndpointGroupId, HttpMethod)
    );
    CREATE INDEX IX_HourlyTrafficSummary_HourUTC_GroupId ON dbo.HourlyTrafficSummary(HourUTC, EndpointGroupId);
    PRINT 'Tabla HourlyTrafficSummary creada.';
END
ELSE
BEGIN
    PRINT 'Tabla HourlyTrafficSummary ya existe. Verificando columnas para permitir NULLs...';
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'RequestCount' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN RequestCount INT NULL; PRINT 'RequestCount en HourlyTrafficSummary ahora permite NULL.'; END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ErrorCount4xx' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN ErrorCount4xx INT NULL; PRINT 'ErrorCount4xx en HourlyTrafficSummary ahora permite NULL.'; END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ErrorCount5xx' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN ErrorCount5xx INT NULL; PRINT 'ErrorCount5xx en HourlyTrafficSummary ahora permite NULL.'; END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'AverageDurationMs' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN AverageDurationMs DECIMAL(10,2) NULL; PRINT 'AverageDurationMs en HourlyTrafficSummary ahora permite NULL.'; END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TotalRequestBytes' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN TotalRequestBytes BIGINT NULL; PRINT 'TotalRequestBytes en HourlyTrafficSummary ahora permite NULL.'; END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'TotalResponseBytes' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN TotalResponseBytes BIGINT NULL; PRINT 'TotalResponseBytes en HourlyTrafficSummary ahora permite NULL.'; END
    IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'UniqueClientIps' AND Object_ID = Object_ID(N'dbo.HourlyTrafficSummary') AND is_nullable = 0)
        BEGIN ALTER TABLE dbo.HourlyTrafficSummary ALTER COLUMN UniqueClientIps INT NULL; PRINT 'UniqueClientIps en HourlyTrafficSummary ahora permite NULL.'; END
END
GO

-- 9. Tabla: BackendDestinations 
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BackendDestinations]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.BackendDestinations (
        DestinationId INT IDENTITY(1,1) PRIMARY KEY,
        Address VARCHAR(2048) UNIQUE NOT NULL,
        FriendlyName VARCHAR(255) NULL,
        IsEnabled BIT NOT NULL DEFAULT 1,
        HealthCheckPath VARCHAR(2048) NULL,
        MetadataJson NVARCHAR(MAX) NULL, 
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    CREATE INDEX IX_BackendDestinations_Address ON dbo.BackendDestinations(Address);
    PRINT 'Tabla BackendDestinations creada.';
END
ELSE
    PRINT 'Tabla BackendDestinations ya existe.';
GO

-- 10. Tabla: EndpointGroupDestinations 
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EndpointGroupDestinations]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EndpointGroupDestinations (
        EndpointGroupDestinationId INT IDENTITY(1,1) PRIMARY KEY,
        GroupId INT NOT NULL,
        DestinationId INT NOT NULL,
        IsEnabledInGroup BIT NOT NULL DEFAULT 1,
        AssignedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        CONSTRAINT FK_EndpointGroupDestinations_EndpointGroups FOREIGN KEY (GroupId) REFERENCES dbo.EndpointGroups(GroupId) ON DELETE CASCADE,
        CONSTRAINT FK_EndpointGroupDestinations_BackendDestinations FOREIGN KEY (DestinationId) REFERENCES dbo.BackendDestinations(DestinationId) ON DELETE CASCADE,
        CONSTRAINT UQ_EndpointGroupDestinations_GroupId_DestinationId UNIQUE (GroupId, DestinationId)
    );
    PRINT 'Tabla EndpointGroupDestinations creada.';
END
ELSE
    PRINT 'Tabla EndpointGroupDestinations ya existe.';
GO

PRINT 'Proceso de creación de tablas finalizado.';
GO
