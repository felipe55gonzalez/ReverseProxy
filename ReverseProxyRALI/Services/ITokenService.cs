using ReverseProxyRALI.Models; // Asumiendo que TokenDefinition está aquí

namespace ReverseProxyRALI.Services
{
    public interface ITokenService
    {
        Task<(bool IsValid, TokenDefinition? TokenDetails)> ValidateTokenAsync(string tokenValue, string requiredEndpointGroup);
    }
}