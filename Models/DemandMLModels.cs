using Microsoft.ML.Data;

namespace HTicket.Models
{
    public class TicketSalesData
    {
        public float RemainingQuantity { get; set; }
        public float SalesVelocity { get; set; }
        public float AvgQtyPerOrder { get; set; }
        public float DaysSinceLaunch { get; set; } // Feature cực quan trọng
        public float Label { get; set; } // Số ngày thực tế cho đến khi hết vé
    }

    public class TicketSalesPrediction
    {
        [ColumnName("Score")]
        public float PredictedDays { get; set; }
    }

}