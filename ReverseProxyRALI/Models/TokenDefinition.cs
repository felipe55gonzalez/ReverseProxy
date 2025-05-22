using System;
using System.Collections.Generic; 

namespace ReverseProxyRALI.Models
{
    public class TokenDefinition
    {
        public string Token { get; set; }
        public List<string> AllowedEndpointGroups { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; }

        public TokenDefinition(string token, List<string> allowedGroups)
        {
            Token = token;
            AllowedEndpointGroups = allowedGroups ?? new List<string>();
            IsActive = true;
            ExpiryDate = DateTime.UtcNow.AddDays(30);
        }
    }
}