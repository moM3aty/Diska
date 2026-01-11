using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace Diska.Controllers
{
    [Authorize]
    public class UserSurveyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserSurveyController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. عرض الاستبيانات النشطة والمتاحة للمستخدم
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var userRole = await _userManager.IsInRoleAsync(user, "Merchant") ? "Merchant" : "Customer";

            // جلب الاستبيانات:
            // 1. نشطة
            // 2. تاريخها ساري
            // 3. موجهة لهذا النوع من المستخدمين أو للكل
            var allSurveys = await _context.Surveys
                .Where(s => s.IsActive && s.EndDate > DateTime.Now && (s.TargetAudience == "All" || s.TargetAudience == userRole))
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            // استبعاد الاستبيانات التي شارك فيها المستخدم سابقاً
            var respondedSurveyIds = await _context.SurveyResponses
                .Where(r => r.UserId == user.Id)
                .Select(r => r.SurveyId)
                .ToListAsync();

            var availableSurveys = allSurveys.Where(s => !respondedSurveyIds.Contains(s.Id)).ToList();

            return View(availableSurveys);
        }

        // 2. صفحة ملء الاستبيان (GET)
        [HttpGet]
        public async Task<IActionResult> Take(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive && s.EndDate > DateTime.Now);

            if (survey == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // التأكد مرة أخرى أن المستخدم لم يشارك من قبل
            bool alreadyTaken = await _context.SurveyResponses.AnyAsync(r => r.SurveyId == id && r.UserId == user.Id);
            if (alreadyTaken)
            {
                TempData["Error"] = "لقد شاركت في هذا الاستبيان من قبل.";
                return RedirectToAction(nameof(Index));
            }

            return View(survey);
        }

        // 3. حفظ الإجابات (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int surveyId, IFormCollection form)
        {
            var user = await _userManager.GetUserAsync(User);

            // تجميع الإجابات في Dictionary
            var answers = new Dictionary<string, string>();

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("q_")) // مفاتيح الأسئلة تبدأ بـ q_
                {
                    answers[key] = form[key];
                }
            }

            if (answers.Count == 0)
            {
                TempData["Error"] = "يرجى الإجابة على الأسئلة.";
                return RedirectToAction("Take", new { id = surveyId });
            }

            var response = new SurveyResponse
            {
                SurveyId = surveyId,
                UserId = user.Id,
                SubmittedAt = DateTime.Now,
                AnswerJson = JsonSerializer.Serialize(answers) // تخزين الإجابات كـ JSON
            };

            _context.SurveyResponses.Add(response);
            await _context.SaveChangesAsync();

            return RedirectToAction("Success");
        }

        // 4. صفحة النجاح
        public IActionResult Success()
        {
            return View();
        }
    }
}