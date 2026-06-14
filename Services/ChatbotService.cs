
using HTicket.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HTicket.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly TestDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _url;

        public ChatbotService(TestDbContext context, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;
            _apiKey = configuration["GeminiSettings:ApiKey"];
            _url = configuration["GeminiSettings:Url"];
        }

        public async Task<string> GetReplyAsync(string userQuestion, string customerId)
        {
            try
            {
                // 1. KIỂM TRA ID KHÁCH HÀNG
                if (!int.TryParse(customerId, out int cId)) return "Lỗi: ID khách hàng không hợp lệ.";

                // 2. LẤY TOÀN BỘ SỰ KIỆN ĐỂ SÀNG LỌC QUA VECTOR
                var allEventsFromDb = await _context.Events
                    .Select(e => new { id = e.Id, name = $"{e.Name} ngày {e.EventDate.Value.ToString("dd/MM/yyyy")}" })
                    .AsNoTracking()
                    .ToListAsync();

                List<int> relevantEventIds = new List<int>();

                if (allEventsFromDb.Any())
                {
                    try
                    {
                        var vectorRequest = new { query = userQuestion, events = allEventsFromDb };
                        var vectorResponse = await _httpClient.PostAsJsonAsync("https://hticketw2v-production.up.railway.app/api/retrieve", vectorRequest);
                        if (vectorResponse.IsSuccessStatusCode)
                        {
                            var vectorResult = await vectorResponse.Content.ReadFromJsonAsync<PythonVectorResponse>();
                            if (vectorResult != null && vectorResult.relevantEventIds != null)
                            {
                                relevantEventIds = vectorResult.relevantEventIds;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Vector AI Error]: {ex.Message}");
                    }
                }
                // 3. TRUY VẤN DATABASE CHÍNH DỰA TRÊN KẾT QUẢ SÀNG LỌC CỦA VECTOR
                var filteredEvents = relevantEventIds.Any()
                    ? await _context.Events
                        .Where(e => relevantEventIds.Contains(e.Id) && (e.Status == "Đang mở bán" || e.Status == "Sắp diễn ra"))
                        .Select(e => new { e.Id, e.Name, e.Location, e.EventDate, e.Status })
                        .AsNoTracking()
                        .ToListAsync()
                    : await _context.Events
                        .Where(e => e.Status == "Đang mở bán" || e.Status == "Sắp diễn ra")
                        .Select(e => new { e.Id, e.Name, e.Location, e.EventDate, e.Status })
                        .OrderByDescending(e => e.Id)
                        .Take(5)
                        .AsNoTracking()
                        .ToListAsync();

                // dùng .Select() để bốc phẳng dữ liệu phẳng
                var tickets = await _context.Tickets
                    .Where(t => t.Event.Status == "Đang mở bán" || t.Event.Status == "Sắp diễn ra")
                    .Select(t => new
                    {
                        t.Id,
                        EventName = t.Event.Name,
                        t.TicketType,
                        t.Price,
                        t.RemainingQuantity
                    })
                    .AsNoTracking()
                    .ToListAsync();

                var orders = await _context.Orders
                    .Where(o => o.CustomerId == cId && o.Status != "Đã hủy")
                    .Select(o => new
                    {
                        o.Id,
                        EventName = o.Ticket.Event.Name,
                        TicketType = o.Ticket.TicketType,
                        o.Status
                    })
                    .AsNoTracking()
                    .ToListAsync();


                // 4. XÂY DỰNG NGỮ CẢNH (PROMPT) GỬI CHO GEMINI

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("DANH SÁCH SỰ KIỆN PHÙ HỢP (ĐÃ QUA SÀNG LỌC AI):");
                foreach (var ev in filteredEvents)
                {
                    sb.AppendLine($"- [ID: {ev.Id}] {ev.Name} | Địa điểm: {ev.Location} | Ngày: {ev.EventDate:dd/MM/yyyy} | Trạng thái: {ev.Status}");
                }

                sb.AppendLine("\nCHI TIẾT KHO VÉ THỰC TẾ ĐANG CÒN:");
                foreach (var t in tickets)
                {
                    sb.AppendLine($"- MãVé: {t.Id} | Show: {t.EventName} | Loại: {t.TicketType} | Giá: {t.Price:N0}đ | Còn lại: {t.RemainingQuantity}");
                }

                sb.AppendLine("\nĐƠN HÀNG HIỆN TẠI CỦA KHÁCH:");
                if (orders.Any())
                {
                    foreach (var o in orders)
                    {
                        sb.AppendLine($"- Mã đơn: {o.Id} | Show: {o.EventName} | Loại vé: {o.TicketType} | Trạng thái: {o.Status}");
                    }
                }
                else
                {
                    sb.AppendLine("- Bạn chưa có đơn hàng nào.");
                }

                // 5. THIẾT LẬP PROMPT QUY TẮC NGHIÊM NGẶT CHO GEMINI
                string promptText = $@"
                Bạn là trợ lý ảo thông minh H-TICKET, hỗ trợ khách hàng dựa trên dữ liệu hệ thống thực tế sau:
                {sb}

                QUY TẮC ĐẶT/HỦY VÉ BẮT BUỘC:
                1. TRẢ LỜI THÔNG TIN:
                   - ĐỐI VỚI SỰ KIỆN 'Sắp diễn ra': 
                        + Nếu khách hỏi về sự kiện này, hãy thông báo rõ: 'Sự kiện này hiện tại chưa mở bán vé chính thức, bạn vui lòng theo dõi thêm thông báo từ hệ thống'. KHÔNG ĐƯỢC sinh lệnh [CREATE_ORDER] cho sự kiện này.
                        + ĐỒNG THỜI, bạn phải chủ động chủ động giới thiệu và gợi ý thêm từ 1 đến 2 sự kiện khác đang có trạng thái 'Đang mở bán'
                   - Trả lời đầy đủ, chi tiết, lịch sự bằng tiếng Việt. Sử dụng <br> để xuống dòng.
                   - Hãy dùng thẻ <b> để in đậm tên sự kiện nhằm tăng trải nghiệm người dùng.

                2. NGHIỆP VỤ ĐẶT VÉ (CREATE_ORDER):
                   - Khi khách hàng có ý định đặt vé hoặc mua vé: Hãy kiểm tra 'MãVé' và số lượng còn lại trong kho vé ở trên.
                   - Nếu khách mua vượt quá số lượng 'Còn lại', hãy báo: 'Hiện tại loại vé này chỉ còn [Số lượng] vé'.
                   - Không được nói mã vé cho khách
                   - Nếu thỏa mãn, bắt buộc trả về chuỗi lệnh chính xác theo cấu trúc: [CREATE_ORDER|MãVé|SốLượng]
                   - Ví dụ: [CREATE_ORDER|5|2] (Nghĩa là đặt 2 vé của mã vé có ID là 5).
                   - Không trả lời mã lệnh 
                3. NGHIỆP VỤ HỦY ĐƠN (CANCEL_ORDER):
                   - Nếu khách muốn hủy đơn hàng, hãy tra cứu trong mục 'ĐƠN HÀNG HIỆN TẠI CỦA KHÁCH' để lấy đúng Mã đơn.
                   - Nếu đơn hiện tại khách muốn hủy có status 'thành công' thì không thể hủy và báo lại khách liên hệ quản trị viên
                   - Định dạng lệnh hủy bắt buộc: [CANCEL_ORDER|MãĐơn]
                   - Ví dụ: [CANCEL_ORDER|102].

                LƯU Ý: - Không tự bịa ID sự kiện hoặc Mã vé nếu dữ liệu trên không tồn tại.
                       - Không trả lời mã lệnh [CANCEL_ORDER:MãĐơn], [CREATE_ORDER|MãVé|SốLượng]
                Câu hỏi của khách: {userQuestion}";

                // 6. GỬI PROMPT SANG GEMINI
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = promptText } } } } };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_url}?key={_apiKey}", content);
                string errorContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(result);
                    return doc.RootElement.GetProperty("candidates")[0]
                                          .GetProperty("content")
                                          .GetProperty("parts")[0]
                                          .GetProperty("text").GetString();
                }
                else
                {
                    return "H-TICKET xin chào! Hệ thống đang bận xử lý dữ liệu trong giây lát, bạn vui lòng thử lại sau nhé.";
                }



                }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL SECURITY LOG]: {ex.Message}\n{ex.StackTrace}");

                // Trả về câu thông báo vô hại để hacker không mò được lỗi gì phía sau
                return "H-TICKET xin chào! Hệ thống đang bận xử lý dữ liệu trong giây lát, bạn vui lòng thử lại sau nhé.";
            }
        }
    }

    public class PythonVectorResponse
    {
        public List<int> relevantEventIds { get; set; }
    }
}
