using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HTicket.Models;

namespace HTicket.Controllers
{
    [Authorize(Roles = "Admin")]

    public class TicketsController : Controller
    {
        private readonly TestDbContext _context;

        public TicketsController(TestDbContext context)
        {
            _context = context;
        }

        // GET: Tickets
        public async Task<IActionResult> Index(string statusFilter, string stockFilter)
        {
            // 1. Khởi tạo query bao gồm thông tin Event
            var query = _context.Tickets.Include(t => t.Event).AsQueryable();

            // 2. Lọc theo Trạng thái sự kiện (Status)
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(t => t.Event.Status == statusFilter);
            }

            // 3. Lọc theo tình trạng kho vé
            if (stockFilter == "low")
                query = query.Where(t => t.RemainingQuantity > 0 && t.RemainingQuantity <= 10);
            else if (stockFilter == "out")
                query = query.Where(t => t.RemainingQuantity == 0);
            else if (stockFilter == "in")
                query = query.Where(t => t.RemainingQuantity > 10);

            // 4. Lấy dữ liệu và Nhóm theo Event
            var tickets = await query.ToListAsync();
            var groupedTickets = tickets.GroupBy(t => t.Event).ToList();

            return View(groupedTickets);

        }
        // GET: Tickets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // GET: Tickets/Create
        // GET: Tickets/Create
        public IActionResult Create()
        {
            // Nạp danh sách sự kiện cho vòng lặp foreach trong View
            ViewBag.Events = _context.Events.OrderBy(e => e.Name).ToList();

            // Giữ lại SelectList dự phòng nếu cần
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name");

            return View();
        }

        // POST: Tickets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,EventId,TicketType,Price,TotalQuantity,RemainingQuantity")] Ticket ticket)
        {
            // 1. Kiểm tra logic nghiệp vụ
            if (ticket.RemainingQuantity > ticket.TotalQuantity)
            {
                ModelState.AddModelError("RemainingQuantity", "Số lượng khả dụng không thể lớn hơn tổng số lượng phát hành!");
            }

            // 2. Nếu dữ liệu hợp lệ thì lưu vào DB
            if (ModelState.IsValid)
            {
                _context.Add(ticket);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // 3. Nếu CÓ LỖI (Logic hoặc Valid), phải nạp lại toàn bộ dữ liệu cho Dropdown trước khi trả về View
            // Nạp lại cho SelectList (asp-items)
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);

            // Nạp lại cho ViewBag.Events (Để sửa lỗi NullReferenceException của vòng lặp foreach)
            ViewBag.Events = await _context.Events.OrderBy(e => e.Name).ToListAsync();

            // Quan trọng: Trả về View cùng với đối tượng 'ticket' để giữ lại các giá trị khách đã nhập
            return View(ticket);
        }



        // GET: Tickets/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                return NotFound();
            }
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);
            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EventId,TicketType,Price,TotalQuantity,RemainingQuantity")] Ticket ticket)
        {
            if (id != ticket.Id) return NotFound();

            // KIỂM TRA LOGIC TẠI ĐÂY
            if (ticket.RemainingQuantity > ticket.TotalQuantity)
            {
                ModelState.AddModelError("RemainingQuantity", "Số lượng còn lại không được lớn hơn tổng số lượng.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ticket);
                    await _context.SaveChangesAsync();

                    // Cập nhật trạng thái sự kiện sau khi sửa vé
                    await UpdateEventStatusAfterTicketEdit(ticket.EventId);
                }
                catch (DbUpdateConcurrencyException) { /* ... */ }
                return RedirectToAction(nameof(Index));
            }
            // Trả về view kèm thông báo lỗi nếu không hợp lệ
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);
            if (id != ticket.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ticket);
                    await _context.SaveChangesAsync();

                
                    await UpdateEventStatusAfterTicketEdit(ticket.EventId);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TicketExists(ticket.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            var oldTicket = await _context.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);

            // Nếu bạn muốn nhập số lượng bán thêm (ví dụ nhập 500 vào một ô "Thêm vé")
            // thì RemainingQuantity mới = RemainingQuantity cũ + Số lượng thêm
            // và TotalQuantity mới = TotalQuantity cũ + Số lượng thêm

            if (ticket.RemainingQuantity > ticket.TotalQuantity)
            {
                ModelState.AddModelError("RemainingQuantity", "Số lượng còn lại không thể lớn hơn tổng số lượng. Hãy cập nhật lại cả 'Tổng số lượng' nếu bạn muốn bán thêm vé!");
                return View(ticket);
            }

            _context.Update(ticket);
            await _context.SaveChangesAsync();
            
            if (id != ticket.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ticket);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TicketExists(ticket.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);
            return View(ticket);
        }

        // GET: Tickets/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // POST: Tickets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket != null)
            {
                _context.Tickets.Remove(ticket);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TicketExists(int id)
        {
            return _context.Tickets.Any(e => e.Id == id);
        }
        private async Task UpdateEventStatusAfterTicketEdit(int eventId)
        {
            var eventItem = await _context.Events.FindAsync(eventId);
            if (eventItem == null) return;

            // Kiểm tra xem có bất kỳ vé nào thuộc sự kiện này còn số lượng > 0 không
            bool isStillAvailable = await _context.Tickets
                .AnyAsync(t => t.EventId == eventId && t.RemainingQuantity > 0);

            // Cập nhật trạng thái sự kiện dựa trên kết quả kiểm tra
            eventItem.Status = isStillAvailable ? "Đang mở bán" : "Hết vé";

            _context.Update(eventItem);
            await _context.SaveChangesAsync();
        }
    }
}
