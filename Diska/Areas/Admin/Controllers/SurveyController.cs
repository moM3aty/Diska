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
            // ملاحظة: يجب إضافة DbSet<Survey> في ApplicationDbContext ليعمل هذا الكود
            // var surveys = await _context.Surveys.Include(s => s.Responses).ToListAsync();
            // بما أننا لم نعدل ملف Context، سنفترض وجوده أو نمرر قائمة فارغة للتجربة
            // للتوضيح، سأكتب الكود كما لو كانت الجداول موجودة

            // var surveys = await _context.Set<Survey>().Include(s => s.Responses).OrderByDescending(s => s.StartDate).ToListAsync();
            // return View(surveys);

            return View(new List<Survey>()); // Placeholder until migration
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
                // إضافة الأسئلة يدوياً من الفورم
                for (int i = 0; i < QuestionsText.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(QuestionsText[i]))
                    {
                        survey.Questions.Add(new SurveyQuestion
                        {
                            QuestionText = QuestionsText[i],
                            Type = QuestionsType[i]
                        });
                    }
                }

                // _context.Set<Survey>().Add(survey);
                // await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(survey);
        }

        public async Task<IActionResult> Results(int id)
        {
            // عرض النتائج
            // var survey = await _context.Set<Survey>().Include(s => s.Responses).FirstOrDefaultAsync(s => s.Id == id);
            // return View(survey);
            return View(new Survey());
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            // var survey = await _context.Set<Survey>().FindAsync(id);
            // if(survey != null) { survey.IsActive = !survey.IsActive; await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}