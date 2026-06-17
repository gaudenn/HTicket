using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HTicket.Models;
using HTicket.Services;

namespace HTicket.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AIController : Controller
    {
        private readonly TestDbContext _context;
        private readonly IDemandPredictionService _aiService;

        public AIController(TestDbContext context, IDemandPredictionService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Kiểm tra và huấn luyện AI (Giữ nguyên logic cũ)
            if (!_aiService.IsTrained)
            {
                var historyData = await GetHistoryFromOrders();
                _aiService.TrainModel(historyData);
            }

            // 2. Lấy danh sách vé và Event kèm theo
            var activeTickets = await _context.Tickets
                .Include(t => t.Event)
                .Where(t => t.Event.Status == "Đang mở bán" && t.Event.EventDate >= DateTime.Now)
                .ToListAsync();

            var ticketIds = activeTickets.Select(t => t.Id).ToList();

            // TỐI ƯU: Chỉ lấy dữ liệu Orders cần thiết và phân nhóm (Lookup) ngay trên bộ nhớ để tránh trùng lặp
            var allOrders = await _context.Orders
                .Where(o => ticketIds.Contains(o.TicketId) && o.Status == "Thành công")
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            // Chuyển danh sách thành ToLookup để tìm kiếm với độ phức tạp O(1) thay vì Where liên tục
            var ordersLookup = allOrders.ToLookup(o => o.TicketId);

            var forecasts = new List<DemandForecast>();
            var now = DateTime.Now;
            var sevenDaysAgo = now.AddDays(-7);

            foreach (var t in activeTickets)
            {
                // TỐI ƯU: Lấy danh sách order từ Lookup
                var ordersForTicket = ordersLookup[t.Id].ToList();
                var recentOrders = ordersForTicket.Where(o => o.OrderDate >= sevenDaysAgo).ToList();

                // Logic tính DaysSinceLaunch (Giữ nguyên)
                float daysSinceLaunch = ordersForTicket.Any()
                    ? (float)(now - ordersForTicket.First().OrderDate).TotalDays
                    : 0.1f;

                // Logic tính Tốc độ Velocity (Giữ nguyên)
                float measureDays = Math.Min(daysSinceLaunch, 7.0f);
                if (measureDays < 0.1f) measureDays = 0.1f;

                float velocity = (float)recentOrders.Sum(o => o.Quantity) / measureDays;
                float avgQty = recentOrders.Any() ? (float)recentOrders.Average(o => o.Quantity) : 0;

                float daysUntilEvent = (float)(t.Event.EventDate.Value - now).TotalDays;

                // Gọi dịch vụ AI dự báo (Giữ nguyên)
                float predictedDays = _aiService.PredictDaysUntilSoldOut(
                    (float)t.RemainingQuantity, velocity, avgQty, daysSinceLaunch);

                if (predictedDays > daysUntilEvent) predictedDays = daysUntilEvent;

                forecasts.Add(FormatDemandForecast(t, velocity, predictedDays, daysUntilEvent));
            }

            return View(forecasts.GroupBy(f => f.EventName).ToList());
        }

        private async Task<List<TicketSalesData>> GetHistoryFromOrders()
        {
            // Lấy danh sách ID vé đã kết thúc
            var finishedTicketIds = await _context.Tickets
                .Where(t => t.RemainingQuantity == 0 || t.Event.EventDate < DateTime.Now)
                .Select(t => t.Id)
                .ToListAsync();

            // TỐI ƯU CỰC LỚN: Lấy TẤT CẢ orders của các vé đã kết thúc bằng 1 câu lệnh SQL duy nhất
            var allFinishedOrders = await _context.Orders
                .Where(o => finishedTicketIds.Contains(o.TicketId) && o.Status == "Thành công")
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            // Phân nhóm orders theo TicketId trên bộ nhớ RAM
            var finishedOrdersLookup = allFinishedOrders.ToLookup(o => o.TicketId);
            var trainingData = new List<TicketSalesData>();

            foreach (var id in finishedTicketIds)
            {
                // TỐI ƯU: Lấy dữ liệu từ bộ nhớ thay vì gọi DB trong vòng lặp foreach
                var orders = finishedOrdersLookup[id].ToList();

                if (orders.Count < 2) continue;

                var firstDate = orders.First().OrderDate;
                var lastDate = orders.Last().OrderDate;
                var totalQty = orders.Sum(o => o.Quantity);
                float avgQtyPerOrder = (float)orders.Average(o => o.Quantity);

                // Logic chia mốc milestone 25%, 50%, 75% (Giữ nguyên 100% logic cũ)
                for (int i = 1; i <= 3; i++)
                {
                    float milestoneTarget = totalQty * (i * 0.25f);

                    // Tính tổng lũy tiến để tìm đơn hàng đạt mốc milestone
                    int runningSum = 0;
                    Order milestoneOrder = null;
                    foreach (var o in orders)
                    {
                        runningSum += o.Quantity;
                        if (runningSum >= milestoneTarget)
                        {
                            milestoneOrder = o;
                            break;
                        }
                    }

                    if (milestoneOrder != null)
                    {
                        float daysFromStart = (float)(milestoneOrder.OrderDate - firstDate).TotalDays;
                        float daysRemaining = (float)(lastDate - milestoneOrder.OrderDate).TotalDays;

                        trainingData.Add(new TicketSalesData
                        {
                            RemainingQuantity = totalQty - milestoneTarget,
                            SalesVelocity = milestoneTarget / (daysFromStart + 0.1f),
                            AvgQtyPerOrder = avgQtyPerOrder,
                            DaysSinceLaunch = daysFromStart,
                            Label = daysRemaining
                        });
                    }
                }
            }

            // Dữ liệu ảo dự phòng nếu thiếu data huấn luyện (Giữ nguyên)
            if (trainingData.Count < 5)
            {
                trainingData.Add(new TicketSalesData { RemainingQuantity = 100, SalesVelocity = 5, AvgQtyPerOrder = 2, DaysSinceLaunch = 2, Label = 20 });
                trainingData.Add(new TicketSalesData { RemainingQuantity = 200, SalesVelocity = 20, AvgQtyPerOrder = 2, DaysSinceLaunch = 1, Label = 10 });
                trainingData.Add(new TicketSalesData { RemainingQuantity = 50, SalesVelocity = 2, AvgQtyPerOrder = 1.5f, DaysSinceLaunch = 10, Label = 25 });
            }

            return trainingData;
        }

        private DemandForecast FormatDemandForecast(Ticket t, float v, float p, float daysUntilEvent)
        {
            // Giữ nguyên 100% logic phân loại và gợi ý cũ của bạn
            var forecast = new DemandForecast
            {
                EventName = t.Event.Name,
                TicketType = t.TicketType,
                SalesVelocity = Math.Round(v, 2),
                PredictedDaysUntilSoldOut = (int)Math.Ceiling(p)
            };

            if (t.RemainingQuantity <= 0)
            {
                forecast.DemandLevel = "HẾT VÉ";
                forecast.Recommendation = "Ngưng quảng cáo để tiết kiệm ngân sách.";
            }
            else if (p >= daysUntilEvent && v > 0)
            {
                forecast.DemandLevel = "RỦI RO";
                forecast.Recommendation = "Tốc độ bán quá chậm. Cần giảm giá hoặc đẩy mạnh Marketing ngay nếu không sẽ tồn vé!";
            }
            else if (p <= 2)
            {
                forecast.DemandLevel = "RẤT CAO";
                forecast.Recommendation = "Sắp cháy vé!";
            }
            else if (p <= 7)
            {
                forecast.DemandLevel = "CAO";
                forecast.Recommendation = "Nhu cầu tốt. Duy trì giá bán và tập trung vào tệp khách chưa thanh toán.";
            }
            else
            {
                forecast.DemandLevel = "ỔN ĐỊNH";
                forecast.Recommendation = "Theo dõi thêm. Có thể tặng kèm quà nhỏ để đẩy nhanh tiến độ.";
            }

            return forecast;
        }
    }
}