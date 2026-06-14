using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HTicket.Models;

namespace HTicket.Models
{
 
    public class Ticket
    {
        public int Id { get; set; }

        public int EventId { get; set; }
        [ForeignKey("EventId")]
        public virtual Event? Event { get; set; }

        [Display(Name = "Loại vé")]
        public string? TicketType { get; set; } // VIP, Thường...

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int TotalQuantity { get; set; } // Tổng số phát hành

        public int RemainingQuantity { get; set; } // Số lượng còn lại thực tế
    }
}