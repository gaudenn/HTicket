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

    public class EventsController : Controller
    {
        private readonly TestDbContext _context;

        public EventsController(TestDbContext context)
        {
            _context = context;
        }

        // GET: Events

        public async Task<IActionResult> Index(string status)
        {
            var statusList = await _context.Events.Select(e => e.Status).Distinct().ToListAsync();
            ViewBag.StatusList = statusList;
            ViewBag.CurrentFilter = status;

            var events = _context.Events.AsQueryable();


            if (!string.IsNullOrEmpty(status))
            {
                events = events.Where(e => e.Status == status);
            }

            return View(await events.ToListAsync());
        }

        // GET: Events/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .Include(e => e.Tickets)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,EventDate,Location,Category,Status")] Event @event)
        {
            if (ModelState.IsValid)
            {
                _context.Add(@event);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(@event);
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }
            return View(@event);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,EventDate,Location,Category,Status")] Event @event)
        {
            if (ModelState.IsValid)
            {
                _context.Update(@event);

                // Logic tự động xử lý vé khi sự kiện dừng lại
                if (@event.Status == "Đã kết thúc" || @event.Status == "Đã hủy")
                {
                    var tickets = await _context.Tickets.Where(t => t.EventId == id).ToListAsync();
                    foreach (var ticket in tickets)
                    {
                        ticket.RemainingQuantity = 0;
                        // Đưa số lượng về 0 để không thể đặt
                    }
                    _context.UpdateRange(tickets);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            if (id != @event.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(@event);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(@event.Id))
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
            return View(@event);
        }

        // GET: Events/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.Id == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event != null)
            {
                _context.Events.Remove(@event);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
