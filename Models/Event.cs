using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

namespace HTicket.Models

{
    public class Event
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên sự kiện là bắt buộc")]
        [Display(Name = "Tên sự kiện")]
        public string? Name { get; set; }

        public string? Description { get; set; }

        [Display(Name = "Ngày diễn ra")]
        public DateTime? EventDate { get; set; }
        [Display(Name = "Địa điểm")]
        public string? Location { get; set; }

        [Display(Name = "Danh mục")] // Để AI phân tích xu hướng theo loại hình
        public string? Category { get; set; }

        public string? Status { get; set; }

        // Liên kết với danh sách vé của sự kiện này
        public virtual ICollection<Ticket>? Tickets { get; set; }
    }
}