using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HTicket.Services;
using HTicket.Models;
using System.Collections.Generic;

namespace HTicket.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly IChatbotService _chatbotService;
        private readonly TestDbContext _context;

        public ChatbotController(IChatbotService chatbotService, TestDbContext context)
        {
            _chatbotService = chatbotService;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            // 1. Kiểm tra dữ liệu đầu vào dữ dằn
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { reply = "Nội dung tin nhắn không được để trống." });
            }

            // 2. Xác thực Email của User đang đăng nhập
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
            {
                return Json(new { reply = "Vui lòng đăng nhập để hệ thống nhận diện bạn!" });
            }

            // 3. Tìm thông tin khách hàng dưới Database
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Email == userEmail);
            if (customer == null)
            {
                return Json(new { reply = "Không tìm thấy thông tin khách hàng trong hệ thống." });
            }

            try
            {
                // 4. Gọi cổng Service AI mới: Truyền 3 tham số (Câu hỏi, ID khách, Lịch sử chat)
                string aiReply = await _chatbotService.GetReplyAsync(request.Message, customer.Id.ToString(), request.History);

                // Khởi tạo chuỗi thông báo từ hệ thống backend
                string systemNotification = "";

                // ----------------------------------------------------
                // KHỐI XỬ LÝ 1: QUÉT VÀ THỰC THI TẤT CẢ LỆNH ĐẶT VÉ [CREATE_ORDER|...|...]
                // ----------------------------------------------------
                var createMatches = Regex.Matches(aiReply, @"\[CREATE_ORDER\|(\d+)\|(\d+)\]");
                foreach (Match match in createMatches)
                {
                    int ticketId = int.Parse(match.Groups[1].Value);
                    int buyQty = int.Parse(match.Groups[2].Value);

                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            var ticket = await _context.Tickets.Include(t => t.Event).FirstOrDefaultAsync(t => t.Id == ticketId);

                            if (ticket == null)
                            {
                                systemNotification += $"<br>❌ <b>Hệ thống:</b> Không tìm thấy Mã vé {ticketId}.";
                            }
                            else if (ticket.RemainingQuantity < buyQty)
                            {
                                systemNotification += $"<br>❌ <b>Hệ thống:</b> Show <b>{ticket.Event?.Name}</b> không đủ vé (Còn lại: {ticket.RemainingQuantity}).";
                            }
                            else
                            {
                                var newOrder = new Order
                                {
                                    CustomerId = customer.Id,
                                    TicketId = ticketId,
                                    Quantity = buyQty,
                                    TotalAmount = buyQty * ticket.Price,
                                    OrderDate = DateTime.Now,
                                    Status = "Chờ thanh toán"
                                };

                                ticket.RemainingQuantity -= buyQty;
                                _context.Orders.Add(newOrder);
                                _context.Tickets.Update(ticket);

                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                systemNotification += $"✅ <b>Hệ thống:</b> Đã tạo đơn hàng thành công cho show <b>{ticket.Event?.Name}</b> (Số lượng: {buyQty}). Bạn vui lòng vào mục Đơn hàng của tôi để thanh toán ngay nhé";
                            }
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            systemNotification += $"<br>❌ <b>Hệ thống:</b> Gặp sự cố khi đặt vé mã #{ticketId}.";
                        }
                    }
                    // Xóa mã lệnh ẩn ra khỏi câu thoại của AI
                    aiReply = aiReply.Replace(match.Value, "");
                }

                // ----------------------------------------------------
                // KHỐI XỬ LÝ 2: QUÉT VÀ THỰC THI TẤT CẢ LỆNH HỦY ĐƠN [CANCEL_ORDER|...]
                // ----------------------------------------------------
                var cancelMatches = Regex.Matches(aiReply, @"\[CANCEL_ORDER\|(\d+)\]");
                foreach (Match match in cancelMatches)
                {
                    int orderId = int.Parse(match.Groups[1].Value);

                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            var order = await _context.Orders.Include(o => o.Ticket).FirstOrDefaultAsync(o => o.Id == orderId);

                            if (order == null)
                            {
                                systemNotification += $"<br>❌ <b>Hệ thống:</b> Không tìm thấy đơn hàng #{orderId}.";
                            }
                            else if (order.CustomerId != customer.Id)
                            {
                                systemNotification += $"<br>❌ <b>Hệ thống:</b> Bạn không có quyền hủy đơn hàng #{orderId}.";
                            }
                            else if (order.Status != "Chờ thanh toán")
                            {
                                systemNotification += $"<br>❌ <b>Hệ thống:</b> Đơn hàng #{orderId} không thể hủy do trạng thái là '{order.Status}'.";
                            }
                            else
                            {
                                order.Status = "Đã hủy";
                                if (order.Ticket != null)
                                {
                                    order.Ticket.RemainingQuantity += order.Quantity;
                                    _context.Tickets.Update(order.Ticket);
                                }

                                _context.Orders.Update(order);
                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                systemNotification += $"<br>✅ <b>Hệ thống:</b> Đã hủy đơn hàng #{orderId} thành công. Cảm ơn bạn";
                            }
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync();
                            systemNotification += $"<br>❌ <b>Hệ thống:</b> Gặp sự cố khi hủy đơn hàng #{orderId}.";
                        }
                    }
                    // Xóa mã lệnh ẩn ra khỏi câu thoại của AI
                    aiReply = aiReply.Replace(match.Value, "");
                }

                // Gộp lời thoại của AI (sau khi đã sạch lệnh) với thông báo tác vụ hệ thống
                string finalResponse = aiReply.Trim() + systemNotification;

                return Json(new { reply = finalResponse });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatbotController Error]: {ex.Message}");
                return StatusCode(500, new { reply = "Có lỗi xảy ra ở hệ thống backend." });
            }
        }
    }
}
