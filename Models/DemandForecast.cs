namespace HTicket.Models
{
    public class DemandForecast
    {
        public int EventId { get; set; }
        public string EventName { get; set; }
        public string TicketType { get; set; }

        public double SalesVelocity { get; set; }

        // Số ngày dự báo (Kết quả từ AI hoặc toán học dự phòng)
        public int PredictedDaysUntilSoldOut { get; set; }

        // Gợi ý hành động (ví dụ: "Cần chạy Marketing", "Sắp cháy vé")
        public string Recommendation { get; set; }

        // Mức độ nhu cầu (Thấp, Trung bình, Hot, Rất nóng 🔥)
        public string DemandLevel { get; set; }
    }
}