using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace Diska.Controllers
{
    public class UserSurveyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserSurveyController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // جلب استبيان نشط للعرض (Pop-up)
        [HttpGet]
        public async Task<IActionResult> GetActiveSurvey()
        {
            var user = await _userManager.GetUserAsync(User);
            string role = user != null ? (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Customer" : "Guest";

            // البحث عن استبيان نشط مناسب للدور ولم يقم المستخدم بالإجابة عليه
            // var survey = await _context.Set<Survey>()
            //    .Include(s => s.Questions)
            //    .Where(s => s.IsActive && s.EndDate > DateTime.Now && (s.TargetAudience == "All" || s.TargetAudience == role))
            //    .FirstOrDefaultAsync();

            // if (survey != null && user != null)
            // {
            //    bool answered = await _context.Set<SurveyResponse>().AnyAsync(r => r.SurveyId == survey.Id && r.UserId == user.Id);
            //    if (answered) return Content(""); 
            // }

            // return PartialView("_SurveyPopup", survey);
            return Content(""); // Placeholder
        }

        [HttpPost]
        public async Task<IActionResult> SubmitResponse(int surveyId, IFormCollection form)
        {
            var user = await _userManager.GetUserAsync(User);
            var answers = new Dictionary<string, string>();

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("q_"))
                {
                    answers[key] = form[key];
                }
            }

            var response = new SurveyResponse
            {
                SurveyId = surveyId,
                UserId = user?.Id ?? "Guest",
                AnswerJson = JsonSerializer.Serialize(answers),
                SubmittedAt = DateTime.Now
            };

            // _context.Set<SurveyResponse>().Add(response);
            // await _context.SaveChangesAsync();

            return Json(new { success = true, message = "شكراً لمشاركتك!" });
        }
    }
}