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
using Microsoft.AspNetCore.Http;

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

        // Action للتحقق من وجود استبيان معلق (يتم استدعاؤه بواسطة AJAX)
        [HttpGet]
        public async Task<IActionResult> CheckPending()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(null);

            var userRole = await _userManager.IsInRoleAsync(user, "Merchant") ? "Merchant" : "Customer";

            // البحث عن أول استبيان نشط وموجه لهذا المستخدم ولم يقم بالإجابة عليه
            var pendingSurvey = await _context.Surveys
                .Where(s => s.IsActive && s.EndDate > DateTime.Now && (s.TargetAudience == "All" || s.TargetAudience == userRole))
                .Where(s => !_context.SurveyResponses.Any(r => r.SurveyId == s.Id && r.UserId == user.Id))
                .OrderByDescending(s => s.StartDate)
                .Select(s => new { s.Id, s.Title, s.TitleEn })
                .FirstOrDefaultAsync();

            return Json(pendingSurvey);
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var userRole = await _userManager.IsInRoleAsync(user, "Merchant") ? "Merchant" : "Customer";

            var allSurveys = await _context.Surveys
                .Where(s => s.IsActive && s.EndDate > DateTime.Now && (s.TargetAudience == "All" || s.TargetAudience == userRole))
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            var respondedSurveyIds = await _context.SurveyResponses
                .Where(r => r.UserId == user.Id)
                .Select(r => r.SurveyId)
                .ToListAsync();

            var availableSurveys = allSurveys.Where(s => !respondedSurveyIds.Contains(s.Id)).ToList();

            return View(availableSurveys);
        }

        [HttpGet]
        public async Task<IActionResult> Take(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive && s.EndDate > DateTime.Now);

            if (survey == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            bool alreadyTaken = await _context.SurveyResponses.AnyAsync(r => r.SurveyId == id && r.UserId == user.Id);
            if (alreadyTaken)
            {
                TempData["Info"] = "لقد شاركت في هذا الاستبيان من قبل.";
                return RedirectToAction(nameof(Index));
            }

            return View(survey);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int surveyId, IFormCollection form)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var answers = new Dictionary<string, string>();
            foreach (var key in form.Keys.Where(k => k.StartsWith("q_")))
            {
                answers[key] = form[key].ToString();
            }

            if (answers.Count == 0)
            {
                TempData["Error"] = "يرجى الإجابة على الأسئلة.";
                return RedirectToAction("Take", new { id = surveyId });
            }

            // التحقق من عدم التكرار
            bool alreadyTaken = await _context.SurveyResponses.AnyAsync(r => r.SurveyId == surveyId && r.UserId == user.Id);
            if (!alreadyTaken)
            {
                var response = new SurveyResponse
                {
                    SurveyId = surveyId,
                    UserId = user.Id,
                    SubmittedAt = DateTime.Now,
                    AnswerJson = JsonSerializer.Serialize(answers)
                };

                _context.SurveyResponses.Add(response);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Success");
        }

        public IActionResult Success()
        {
            return View();
        }
    }
}