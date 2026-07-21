using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.ValueObjects
{
    public sealed record Address
    {
        public string Street { get; init; } = string.Empty;
        public string BuildingNumber { get; init; } = string.Empty;
        public string? ApartmentNumber { get; init; }
        public string City { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
    }
}
