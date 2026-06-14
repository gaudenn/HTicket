using HTicket.Models;
using System.Collections.Generic;

namespace HTicket.Services
{
    public interface IDemandPredictionService
    {
        // 1. Huấn luyện mô hình
        void TrainModel(IEnumerable<TicketSalesData> trainingData);

        // 2. Dự báo số ngày bán hết (Chỉ giữ lại một định nghĩa duy nhất)
        // Tham số: Số vé còn lại, Tốc độ bán, Trung bình mỗi đơn, Số ngày từ khi mở bán
        float PredictDaysUntilSoldOut(float remainingQty, float velocity, float avgQty, float daysSinceLaunch);

        // 3. Kiểm tra trạng thái Model
        bool IsTrained { get; }
    }
}