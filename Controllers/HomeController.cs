using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using HTicket.Models;

namespace HTicket.Controllers
{
    public class HomeController : Controller
    {
        private readonly TestDbContext _context;
        public HomeController(TestDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy tất cả sự kiện, bao gồm cả danh sách vé của từng sự kiện
            var events = await _context.Events
                .Include(e => e.Tickets)
                .OrderByDescending(e => e.EventDate) // Sự kiện mới nhất/tương lai lên đầu
                .ToListAsync();

            return View(events);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
