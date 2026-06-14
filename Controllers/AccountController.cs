using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HTicket.Models;
public class AccountController : Controller
{
    private readonly TestDbContext _context;
    [HttpGet]
    public IActionResult Login() => View();
    public AccountController(TestDbContext context)
    {
        _context = context;
    }
    [HttpPost]
    public async Task<IActionResult> Login(string email)
    {
        //  Kiểm tra Email trống
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập Email.");
            return View();
        }

        //  Truy vấn Database để tìm khách hàng
        var customer = await _context.Customers.FirstOrDefaultAsync(u => u.Email == email);

        if (customer == null)
        {
            TempData["ErrorMessage"] = "Email này chưa được đăng ký hoặc không tồn tại!";
            return View(); 
        }

        string userRole = (customer.Email.ToLower() == "admin@gmail.com") ? "Admin" : "Customer";

        //  Tạo danh sách Claims từ thông tin trong DB
        var claims = new List<Claim>
    {
        // Lấy FullName từ DB làm tên hiển thị
        new Claim(ClaimTypes.Name, customer.FullName ?? email.Split('@')[0]),
        new Claim(ClaimTypes.Email, customer.Email),
        new Claim(ClaimTypes.Role, userRole),

    };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        //  Đăng nhập vào hệ thống Cookie
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        //  Chuyển hướng
        return RedirectToAction("Index", "Home");
    }
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Register(string fullName, string email, string phoneNumber)
    {
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError("", "Email là bắt buộc để định danh người dùng.");
            return View();
        }

        var existingUser = await _context.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (existingUser != null)
        {    
            ModelState.AddModelError("email", "Email này đã được đăng ký. Vui lòng sử dụng email khác.");
            return View();
        }
        var customer = new Customer
        {
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
            Password = null, 
            Role = "Customer"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return await Login(email);
    }
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("Login");

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == userEmail);
        if (customer == null) return NotFound();

        return View(customer);
    }
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(int id, [Bind("Id,FullName,Email,PhoneNumber,Role,Password")] Customer customer)
    {
        if (id != customer.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(customer);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Cập nhật thông tin thành công!";
                return RedirectToAction(nameof(Profile));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Customers.Any(e => e.Id == customer.Id)) return NotFound();
                throw;
            }
        }
        return View(customer);
    }
}