using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HTicket.Models;

namespace HTicket.Models
{

    public class Order
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        public int TicketId { get; set; }
        [ForeignKey("TicketId")]
        public virtual Ticket? Ticket { get; set; }

        [Display(Name = "Số lượng mua")]
        public int Quantity { get; set; }

        [Display(Name = "Tổng tiền")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } // Sẽ bằng Ticket.Price * Quantity

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string? Status { get; set; } // Thành công, Đã hủy...
    }
}