
using HTicket.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
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

        public async Task<string> GetReplyAsync(string userQuestion, string customerId, List<ChatItem> history)
        {
            try
            {
                // KIỂM TRA ID KHÁCH HÀNG
                if (!int.TryParse(customerId, out int cId)) return "Lỗi: ID khách hàng không hợp lệ.";

                // LẤY TOÀN BỘ SỰ KIỆN ĐỂ SÀNG LỌC QUA VECTOR
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
                // XÂY DỰNG CHUỖI LỊCH SỬ CHAT CHO GEMINI
                StringBuilder conversationContext = new StringBuilder();
                if (history != null && history.Any())
                {
                    conversationContext.AppendLine("LỊCH SỬ 10 CUỘC TRÒ CHUYỆN TRƯỚC ĐÓ");
                    foreach (var chat in history)
                    {
                        var recentHistory = history.TakeLast(10);
                        string role = chat.Sender == "user" ? "Khách hàng" : "Chatbot";
                        conversationContext.AppendLine($"{role}: {chat.Text}");
                    }
                    conversationContext.AppendLine("--- KẾT THÚC LỊCH SỬ ---");
                }

                //  TRUY VẤN DATABASE
                // Lấy danh sách sự kiện dựa trên AI tìm kiếm hoặc fallback lấy sự kiện mới nhất
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
                        o.Status,
                        o.Quantity,
                        o.TotalAmount
                    })
                    .AsNoTracking()
                    .ToListAsync();


                // XÂY DỰNG NGỮ CẢNH (PROMPT) GỬI CHO GEMINI

                StringBuilder sb = new StringBuilder();

                sb.AppendLine("DANH SÁCH SỰ KIỆN PHÙ HỢP:");
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
                        sb.AppendLine($"- Mã đơn: {o.Id} | Show: {o.EventName} | Loại vé: {o.TicketType} | Trạng thái: {o.Status} | Số lượng: {o.Quantity} | Tổng cộng: {o.TotalAmount:N0}đ");
                    }
                }
                else
                {
                    sb.AppendLine("- Bạn chưa có đơn hàng nào.");
                }

                //THIẾT LẬP PROMPT QUY TẮC NGHIÊM NGẶT CHO GEMINI
                string promptText = $@"Bạn là H-TICKET, hệ thống xử lý giao dịch tự động. 
                Dữ liệu trạng thái hiện tại:
                {sb}
                Lịch sử hội thoại: {conversationContext}
                QUY TẮC PHẢN HỒI BẮT BUỘC:
                1. XỬ LÝ LỆNH (Ưu tiên tuyệt đối):   
                - Nếu yêu cầu là ĐẶT VÉ: Kiểm tra tồn kho từ {sb}. Nếu đủ, TRẢ VỀ LỆNH: [CREATE_ORDER|MãVé|SốLượng].   
                - Không cần hỏi xác nhận khách hàng, thực hiện ngay lập tức nếu hợp lệ.
                - Nếu yêu cầu là HỦY ĐƠN: 
                    + CHỈ thực hiện khi khách hàng có ý định muốn HỦY đơn (Ví dụ: ""hủy giúp tôi"", ""tôi muốn hủy đơn"").
                    + KIỂM TRA trạng thái đơn hàng trong {{sb}}. Nếu trạng thái là 'Chờ thanh toán', TRẢ VỀ LỆNH: [CANCEL_ORDER|MãĐơn].
                    + Nếu khách chỉ hỏi về đơn hàng (Ví dụ: ""Đơn chưa thanh toán có sao không?"", ""Kiểm tra đơn...""): TRẢ LỜI CÂU HỎI, CẤM TRẢ VỀ LỆNH [CANCEL_ORDER].   
                - Nếu yêu cầu là sửa số lượng thì phản hồi khách phải vào mục 'Đơn hàng của tôi' để tự sửa.                      
                - RÀNG BUỘC CẤM:      
                + CẤM tự ý phản hồi lệnh hủy đơn nếu không có yêu cầu.     
                + CẤM hỏi xác nhận trước khi hủy đơn. Thực hiện hủy ngay lập tức nếu hợp lệ.     
                + CẤM trả về bất kỳ nội dung nào khác ngoài lệnh khi có giao dịch được thực hiện.     
                + CẤM trả lời ID sự kiện hoặc Mã vé cho khách.
                2. CẤU TRÚC PHẢN HỒI:   
                - Nếu phát sinh lệnh: Phản hồi duy nhất 1 câu xác nhận kèm mã lệnh (Ví dụ: ""Đã hủy đơn 102. [CANCEL_ORDER|102]"").   
                - Nếu không phát sinh lệnh: Cung cấp thông tin chi tiết dựa vào {sb}. Sử dụng thẻ <b> cho tên sự kiện, <br> để xuống dòng.   
                
                3. ĐIỀU KIỆN RÀNG BUỘC:   
                - Nếu sự kiện ở trạng thái 'Sắp diễn ra': Thông báo 'Chưa mở bán' và giới thiệu các sự kiện 'Đang mở bán'. KHÔNG tạo lệnh.   
                - Luôn ưu tiên dữ liệu từ {sb}. Không sử dụng kiến thức bên ngoài nếu không có trong {sb}.

                Câu hỏi của khách: {userQuestion}";


                //  GỬI PROMPT SANG GEMINI
                var requestBody = new
                {
                    model = "google/gemini-2.5-flash-lite",
                    messages = new[]
                            {
                                new { role = "system", content = "Bạn là trợ lý H-TICKET thông minh." },
                                new { role = "user", content = promptText }
                            }
                };

                var json = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Tạo request message riêng để không đụng chạm đến HttpClient dùng chung
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _url);
                requestMessage.Content = content;

                // Thêm Header vào request này thay vì HttpClient
                requestMessage.Headers.Add("Authorization", $"Bearer {_apiKey}");
                requestMessage.Headers.Add("HTTP-Referer", "http://localhost:5000");
                requestMessage.Headers.Add("X-Title", "HTicket Chatbot");

                var response = await _httpClient.SendAsync(requestMessage);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(result);
                    return doc.RootElement.GetProperty("choices")[0]
                                          .GetProperty("message")
                                          .GetProperty("content").GetString();
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[OpenRouter Error]: {errorContent}"); // Log lỗi để debug
                    return "H-TICKET xin chào! Hệ thống đang bận xử lý dữ liệu, bạn vui lòng thử lại sau nhé.";
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
