using Microsoft.AspNetCore.Mvc;
using Diska.Services;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System;
using Diska.Data;
using Diska.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ShippingController : Controller
    {
        private readonly IShippingService _shippingService;
        private readonly ApplicationDbContext _context;

        public ShippingController(IShippingService shippingService, ApplicationDbContext context)
        {
            _shippingService = shippingService;
            _context = context;
        }

        // 1. عرض القائمة (Excel + Manual)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rates = await _context.ShippingRates
                .OrderBy(r => r.Governorate)
                .ThenBy(r => r.City)
                .ToListAsync();

            return View(rates);
        }

        // 2. رفع ملف إكسل
        [HttpPost]
        public async Task<IActionResult> UploadSheet(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "يرجى اختيار ملف صحيح.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // سيقوم هذا بمسح القديم وإضافة الجديد من الملف
                await _shippingService.ImportFromExcelAsync(file);
                TempData["Success"] = "تم تحديث أسعار الشحن بنجاح من الملف.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "حدث خطأ أثناء قراءة الملف: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // 3. تصدير البيانات (Export) - لتعديلها وإعادة رفعها
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var rates = await _context.ShippingRates.ToListAsync();

            var builder = new StringBuilder();
            // إضافة BOM لضمان قراءة Excel للعربية بشكل صحيح
            builder.Append('\uFEFF');
            builder.AppendLine("Governorate,City,Cost");

            foreach (var rate in rates)
            {
                // استبدال الفواصل لتجنب تكسير ملف الـ CSV
                var gov = rate.Governorate?.Replace(",", " ") ?? "";
                var city = rate.City?.Replace(",", " ") ?? "";
                builder.AppendLine($"{gov},{city},{rate.Cost}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "ShippingRates.csv");
        }

        // 4. حفظ سعر (إضافة أو تعديل) - Manual
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRate(ShippingRate model)
        {
            // إزالة التحقق من الـ City إذا كانت اختيارية في الموديل
            if (string.IsNullOrEmpty(model.City)) ModelState.Remove("City");

            if (ModelState.IsValid)
            {
                if (model.Id == 0) // إضافة جديد
                {
                    _context.ShippingRates.Add(model);
                    TempData["Success"] = "تم إضافة السعر بنجاح.";
                }
                else // تعديل موجود
                {
                    // جلب السجل القديم وتحديث بياناته
                    var rate = await _context.ShippingRates.FindAsync(model.Id);
                    if (rate != null)
                    {
                        rate.Governorate = model.Governorate;
                        rate.City = model.City;
                        rate.Cost = model.Cost;

                        _context.Update(rate);
                        TempData["Success"] = "تم تعديل السعر بنجاح.";
                    }
                    else
                    {
                        TempData["Error"] = "السعر غير موجود.";
                        return RedirectToAction(nameof(Index));
                    }
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "بيانات غير صالحة، تأكد من ملء الحقول المطلوبة.";
            return RedirectToAction(nameof(Index));
        }

        // 5. حذف سعر - Manual
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var rate = await _context.ShippingRates.FindAsync(id);
            if (rate != null)
            {
                _context.ShippingRates.Remove(rate);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف السعر.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}