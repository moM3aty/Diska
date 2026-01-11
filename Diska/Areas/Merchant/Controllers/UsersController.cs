using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.AspNetCore.Identity;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Areas.Merchant.Controllers
{
    [Area("Merchant")]
    [Authorize(Roles = "Merchant")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // عرض فريق العمل التابع للتاجر
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // في هذا النظام المبسط، سنفترض أن الموظفين يتم ربطهم بالتاجر عبر حقل معين 
            // أو سنقوم بعرض المستخدمين الذين يحملون Role "Staff" وتم إنشاؤهم بواسطة هذا التاجر
            // للتبسيط الآن: سنعرض صفحة فارغة مهيأة للإضافة مستقبلاً إذا تم توسيع الموديل ليشمل ParentId

            // var staff = await _userManager.Users.Where(u => u.ParentId == currentUser.Id).ToListAsync();

            return View(new List<ApplicationUser>());
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string fullName, string phone, string password, List<string> permissions)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);

                var user = new ApplicationUser
                {
                    UserName = phone,
                    PhoneNumber = phone,
                    FullName = fullName,
                    ShopName = currentUser.ShopName, // يتبع نفس المحل
                    Email = $"{phone}@diska.staff",
                    IsVerifiedMerchant = true, // مفعل لأنه تابع لتاجر موثق
                    // ParentId = currentUser.Id // (لو تم إضافته للموديل)
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Staff");

                    // حفظ الصلاحيات المحددة
                    foreach (var perm in permissions)
                    {
                        _context.MerchantPermissions.Add(new MerchantPermission
                        {
                            MerchantId = user.Id,
                            Module = perm,
                            CanView = true,
                            CanCreate = true, // تبسيط للصلاحيات
                            CanEdit = true
                        });
                    }
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم إضافة الموظف بنجاح.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            return View();
        }
    }
}