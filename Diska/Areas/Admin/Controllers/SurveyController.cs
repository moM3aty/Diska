using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SurveyController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SurveyController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var surveys = await _context.Surveys
                .Include(s => s.Responses)
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();
            return View(surveys);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Survey survey, List<string> QuestionsText, List<string> QuestionsType)
        {
            if (ModelState.IsValid)
            {
                // حفظ الاستبيان أولاً
                _context.Surveys.Add(survey);
                await _context.SaveChangesAsync(); // للحصول على ID

                // حفظ الأسئلة
                if (QuestionsText != null)
                {
                    for (int i = 0; i < QuestionsText.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(QuestionsText[i]))
                        {
                            var question = new SurveyQuestion
                            {
                                SurveyId = survey.Id,
                                QuestionText = QuestionsText[i],
                                Type = QuestionsType[i] ?? "Text"
                            };
                            _context.SurveyQuestions.Add(question);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            return View(survey);
        }

        public async Task<IActionResult> Results(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .Include(s => s.Responses)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (survey == null) return NotFound();

            return View(survey);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var survey = await _context.Surveys.FindAsync(id);
            if (survey != null)
            {
                survey.IsActive = !survey.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var survey = await _context.Surveys.FindAsync(id);
            if (survey != null)
            {
                _context.Surveys.Remove(survey);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}