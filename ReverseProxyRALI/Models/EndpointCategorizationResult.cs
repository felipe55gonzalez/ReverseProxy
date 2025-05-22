namespace ReverseProxyRALI.Models
{
    public record EndpointCategorizationResult(
        string GroupName,
        bool RequiresToken,
        string MatchedPathPattern 
    );
}
