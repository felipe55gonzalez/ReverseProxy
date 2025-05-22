// En Services/IEndpointCategorizer.cs
using ReverseProxyRALI.Models; // Para EndpointCategorizationResult

namespace ReverseProxyRALI.Services
{
    public interface IEndpointCategorizer
    {
        EndpointCategorizationResult? GetEndpointGroupForPath(string path);
    }
}