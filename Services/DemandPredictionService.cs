using HTicket.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace HTicket.Services
{
    public class DemandPredictionService : IDemandPredictionService
    {
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private PredictionEngine<TicketSalesData, TicketSalesPrediction> _predictionEngine;

        public bool IsTrained => _model != null;

        public DemandPredictionService() { _mlContext = new MLContext(seed: 1); }

        public void TrainModel(IEnumerable<TicketSalesData> trainingData)
        {
            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Thêm DaysSinceLaunch vào Features để AI biết show mới hay show cũ
            var pipeline = _mlContext.Transforms.Concatenate("Features",
                    "RemainingQuantity", "SalesVelocity", "AvgQtyPerOrder", "DaysSinceLaunch")
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.Regression.Trainers.FastTree(labelColumnName: "Label"));

            _model = pipeline.Fit(dataView);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<TicketSalesData, TicketSalesPrediction>(_model);
        }

        public float PredictDaysUntilSoldOut(float remainingQty, float velocity, float avgQty, float daysSinceLaunch)
        {
            if (_predictionEngine == null) return -1;

            // Nếu tốc độ bằng 0, không cần hỏi AI, trả về vô hạn (hoặc số lớn)
            if (velocity <= 0) return 999;

            var input = new TicketSalesData
            {
                RemainingQuantity = remainingQty,
                SalesVelocity = velocity,
                AvgQtyPerOrder = avgQty,
                DaysSinceLaunch = daysSinceLaunch
            };

            var prediction = _predictionEngine.Predict(input);

            // Ràng buộc kết quả không được nhỏ hơn 0.5 ngày nếu vẫn còn vé
            return prediction.PredictedDays < 0.5f ? 0.5f : prediction.PredictedDays;
        }
    }
}