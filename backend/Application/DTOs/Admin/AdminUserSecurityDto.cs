using Domain.Enums;
using Domain.ValueObjects;

namespace Application.DTOs.Admin
{
    public class AdminUserSecurityDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsTwoFactorEnabled { get; set; }
        public DateTime CreatedAtStart { get; set; }
        public DateTime CreatedAtEnd { get; set; }
        public List<Address>? Address { get; set; }
    }
}