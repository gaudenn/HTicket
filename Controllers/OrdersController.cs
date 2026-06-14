using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HTicket.Models;

namespace HTicket.Controllers
{
    public class OrdersController : Controller
    {
      
        private readonly TestDbContext _context;

        public OrdersController(TestDbContext context)
        {
            
            _context = context;
        }
  
        public async Task<IActionResult> Index(string searchString, string statusFilter, int? eventId)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            // 1. Lấy danh sách sự kiện cho Dropdown 
            ViewBag.Events = await _context.Events
                .OrderByDescending(e => e.EventDate)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            var ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Ticket).ThenInclude(t => t.Event)
                .AsQueryable();

            // 2. Phân quyền: Nếu không phải Admin thì chỉ thấy đơn của mình
            if (userRole != "Admin")
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.Email == userEmail);
            }

            // 3. Bộ lọc theo tên khách hàng
            if (!string.IsNullOrEmpty(searchString))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName.Contains(searchString));
            }

            // 4. Bộ lọc theo trạng thái
            if (!string.IsNullOrEmpty(statusFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Status == statusFilter);
            }

            // 5. theo sự kiện
            if (eventId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.Ticket.EventId == eventId.Value);
            }

            // 6. Thực thi truy vấn và GroupBy
            var orders = await ordersQuery.OrderByDescending(o => o.OrderDate).ToListAsync();
            var finalResult = orders.GroupBy(o => o.Customer).ToList();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentEvent"] = eventId;

            return View(finalResult);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Ticket)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // GET: Orders/Create+
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Create(int eventId)
        {
            // 1. Lấy thông tin người dùng đang đăng nhập
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);

            if (customer != null)
            {
                ViewBag.CurrentCustomerId = customer.Id;
                ViewBag.CurrentCustomerName = customer.FullName;
            }

            // 2. Chuẩn bị dữ liệu cho Dropdown
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "FullName", customer?.Id);
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", eventId);

            var availableEvents = _context.Events.Where(e => e.Status == "Đang mở bán").ToList();

            // Đưa danh sách đã lọc vào SelectList
            ViewData["EventId"] = new SelectList(availableEvents, "Id", "Name", eventId);
            var tickets = await _context.Tickets
                .Where(t => t.EventId == eventId)
                .Select(t => new
                {
                    Id = t.Id,
                    Display = t.TicketType + " - " + t.Price.ToString("N0") + " VNĐ (Còn " + t.RemainingQuantity + ")"
                }).ToListAsync();

            ViewData["TicketId"] = new SelectList(tickets, "Id", "Display");

            return View();
        }


        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int CustomerId, Dictionary<int, int> quantities, int eventId)
        {
            // Xác định khách hàng từ hệ thống để đảm bảo an toàn dữ liệu
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);
            int finalCustomerId = customer != null ? customer.Id : CustomerId;

            if (quantities == null || !quantities.Any(x => x.Value > 0))
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất một loại vé và số lượng lớn hơn 0.");
            }

            if (ModelState.IsValid)
            {
                bool hasError = false;
                foreach (var item in quantities)
                {
                    int ticketId = item.Key;
                    int buyQty = item.Value;

                    if (buyQty > 0)
                    {
                        var ticket = await _context.Tickets
                            .Include(t => t.Event)
                            .FirstOrDefaultAsync(t => t.Id == ticketId);

                        if (ticket != null)
                        {
                            if (ticket.RemainingQuantity >= buyQty)
                            {
                                // Logic tạo Order cũ của bạn
                                var newOrder = new Order
                                {
                                    CustomerId = finalCustomerId,
                                    TicketId = ticketId,
                                    Quantity = buyQty,
                                    TotalAmount = buyQty * ticket.Price,
                                    OrderDate = DateTime.Now,
                                    Status = "Chờ thanh toán"
                                };

                                ticket.RemainingQuantity -= buyQty;
                                _context.Add(newOrder);
                                _context.Update(ticket);
                                await UpdateEventStatus(ticket.EventId);
                            }
                            else
                            {
                                ModelState.AddModelError("", $"Hạng vé '{ticket.TicketType}' chỉ còn {ticket.RemainingQuantity} vé.");
                                hasError = true;
                            }
                        }
                    }
                }

                if (!hasError)
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }

            // Nếu có lỗi, nạp lại dữ liệu cho View
            if (customer != null)
            {
                ViewBag.CurrentCustomerId = customer.Id;
                ViewBag.CurrentCustomerName = customer.FullName;
            }
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "FullName", finalCustomerId);
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", eventId);
            return View();
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.Ticket)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();

            PopulateEditData(order);
            return View(order);
        }

        // POST: Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CustomerId,TicketId,Quantity,TotalAmount,OrderDate,Status")] Order order)
        {
            if (id != order.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var oldOrder = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
                    if (oldOrder == null) return NotFound();

                    if (oldOrder.Status != "Đã hủy")
                    {
                        var oldTicket = await _context.Tickets.FindAsync(oldOrder.TicketId);
                        if (oldTicket != null)
                        {
                            oldTicket.RemainingQuantity += oldOrder.Quantity;
                            _context.Update(oldTicket);
                        }

                        var newTicket = await _context.Tickets.FindAsync(order.TicketId);
                        if (newTicket != null)
                        {
                            if (newTicket.RemainingQuantity < order.Quantity)
                            {
                                ModelState.AddModelError("", "Hạng vé mới không đủ số lượng.");
                                PopulateEditData(order);
                                return View(order);
                            }
                            newTicket.RemainingQuantity -= order.Quantity;
                            _context.Update(newTicket);
                        }
                    }

                    _context.Update(order);
                    await _context.SaveChangesAsync();

                    var currentTicket = await _context.Tickets.FindAsync(order.TicketId);
                    if (currentTicket != null) await UpdateEventStatus(currentTicket.EventId);

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.Id)) return NotFound();
                    else throw;
                }
            }
            PopulateEditData(order);
            return View(order);
        }

        private void PopulateEditData(Order order)
        {
            var ticketList = _context.Tickets
                .Where(t => t.EventId == (order.Ticket != null ? order.Ticket.EventId : 0))
                .Select(t => new {
                    Id = t.Id,
                    DisplayText = $"{t.TicketType} (Giá: {t.Price:N0} đ)"
                }).ToList();
            ViewData["CustomerId"] = new SelectList(_context.Customers, "Id", "FullName", order.CustomerId);
            ViewData["TicketId"] = new SelectList(ticketList, "Id", "DisplayText", order.TicketId);
        }

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Ticket)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null) return NotFound();
            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool restoreStock = false)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            int eventIdForUpdate = 0;
            var ticket = await _context.Tickets.FindAsync(order.TicketId);
            if (ticket != null)
            {
                eventIdForUpdate = ticket.EventId;
                if (restoreStock)
                {
                    ticket.RemainingQuantity += order.Quantity;
                    _context.Update(ticket);
                }
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            if (eventIdForUpdate > 0) await UpdateEventStatus(eventIdForUpdate);

            return RedirectToAction(nameof(Index));
        }

        // --- CÁC HÀM HỖ TRỢ ---
        public async Task<JsonResult> GetTicketsByEvent(int eventId)
        {
            var tickets = await _context.Tickets
                .Where(t => t.EventId == eventId)
                .Select(t => new {
                    id = t.Id,
                    ticketType = t.TicketType,
                    price = t.Price,
                    remainingQuantity = t.RemainingQuantity
                }).ToListAsync();
            return Json(tickets);
        }

        private async Task UpdateEventStatus(int eventId)
        {
            var eventItem = await _context.Events.FindAsync(eventId);
            if (eventItem == null) return;

            bool isAnyTicketAvailable = await _context.Tickets
                .AnyAsync(t => t.EventId == eventId && t.RemainingQuantity > 0);

            eventItem.Status = isAnyTicketAvailable ? "Đang mở bán" : "Hết vé";
            _context.Update(eventItem);
            await _context.SaveChangesAsync();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id, bool restoreStock = false)
        {

            var order = await _context.Orders
                .Include(o => o.Ticket)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null && order.Status == "Chờ thanh toán")
            {
                order.Status = "Đã hủy";
                if (restoreStock && order.Ticket != null)
                {
                    order.Ticket.RemainingQuantity += order.Quantity;
                    _context.Update(order.Ticket);
                }
                _context.Update(order);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        



        [HttpGet]
        public async Task<IActionResult> Checkout(int customerId)
        {
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Ticket)
                    .ThenInclude(t => t.Event) // Thêm dòng này để lấy thông tin Sự kiện
                .Where(o => o.CustomerId == customerId && o.Status == "Chờ thanh toán")
                .ToListAsync();

            if (!orders.Any())
            {
                return RedirectToAction(nameof(Index));
            }

            // Gửi CustomerId qua View để dùng nếu cần (ví dụ quay lại trang trước)
            ViewBag.CustomerId = customerId;
            return View(orders);
        }
        
        [HttpPost]
        public IActionResult ConfirmPayment(int[] selectedOrderIds)
        {
            var orders = _context.Orders.Where(o => selectedOrderIds.Contains(o.Id)).ToList();
            if (orders.Count == 0) return NotFound();

            // 1. Thông tin tài khoản ngân hàng của bạn (Để nhận tiền demo)
            string NH_BIN = "970415"; // Ví dụ: 970436 là mã BIN của Vietcombank
            string STK = "103873185406"; // Số tài khoản của bạn
            string TEN_TK = "LE MANH HUNG"; // Tên tài khoản (không dấu)

            // 2. Tính tổng tiền
            int amount = (int)orders.Sum(o => (decimal)o.TotalAmount);
            string description = "Thanh toan don hang " + DateTime.Now.ToString("ddHHmm");

            // 3. Tạo Link ảnh QR theo chuẩn VietQR (Dịch vụ miễn phí của VietQR.io)
            // Cấu trúc: https://img.vietqr.io/image/<BANK_ID>-<ACCOUNT_NO>-<TEMPLATE>.png?amount=<AMOUNT>&addInfo=<DESCRIPTION>&accountName=<ACCOUNT_NAME>
            string vietQrUrl = $"https://img.vietqr.io/image/{NH_BIN}-{STK}-qr_only.png?amount={amount}&addInfo={description}&accountName={TEN_TK}";

            // 4. Trả về một View để hiển thị cái mã QR này
            ViewBag.QrUrl = vietQrUrl;
            ViewBag.Amount = amount;
            ViewBag.OrderId = description;

            TempData["PaidOrderIds"] = selectedOrderIds;
            return View("ShowQRCode");
            
        }
        
        [HttpGet]
        public async Task<IActionResult> ConfirmPaymentSuccess()
        {
            // Lấy lại danh sách ID đã chọn từ TempData
            var paidIds = TempData["PaidOrderIds"] as int[];

            if (paidIds != null && paidIds.Any())
            {
                var ordersToUpdate = await _context.Orders
                    .Where(o => paidIds.Contains(o.Id)) // CHỈ cập nhật những đơn đã chọn
                    .ToListAsync();

                foreach (var order in ordersToUpdate)
                {
                    order.Status = "Thành công";
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", "Orders");
        }


        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }
    }
}