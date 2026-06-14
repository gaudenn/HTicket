using System.ComponentModel.DataAnnotations;

namespace HTicket.Models
{
 
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        public string? FullName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string Role { get; set; } = "Customer"; // Mặc định là khách hàng

        // Lịch sử mua vé của khách hàng này
        public virtual ICollection<Order>? Orders { get; set; }
    }
}