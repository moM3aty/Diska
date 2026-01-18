using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Diska.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

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
            return View(new Survey { StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(14) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Survey survey, List<string> QText, List<string> QTextEn, List<string> QType, List<string> QOptions)
        {
            if (ModelState.IsValid)
            {
                _context.Surveys.Add(survey);
                await _context.SaveChangesAsync();

                if (QText != null)
                {
                    for (int i = 0; i < QText.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(QText[i]))
                        {
               
                            string optionsValue = "";
                            if (QOptions != null && QOptions.Count > i && !string.IsNullOrEmpty(QOptions[i]))
                            {
                                optionsValue = QOptions[i];
                            }

                            var question = new SurveyQuestion
                            {
                                SurveyId = survey.Id,
                                QuestionText = QText[i],
                                QuestionTextEn = (QTextEn != null && QTextEn.Count > i && !string.IsNullOrEmpty(QTextEn[i])) ? QTextEn[i] : QText[i],
                                Type = QType[i],
                                Options = optionsValue
                            };
                            _context.SurveyQuestions.Add(question);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "تم إنشاء الاستبيان بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            return View(survey);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var survey = await _context.Surveys
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (survey == null) return NotFound();
            return View(survey);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Survey survey, List<string> QText, List<string> QTextEn, List<string> QType, List<string> QOptions)
        {
            if (id != survey.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSurvey = await _context.Surveys
                        .Include(s => s.Questions)
                        .FirstOrDefaultAsync(s => s.Id == id);

                    if (existingSurvey == null) return NotFound();

                    existingSurvey.Title = survey.Title;
                    existingSurvey.TitleEn = survey.TitleEn;
                    existingSurvey.Description = survey.Description;
                    existingSurvey.StartDate = survey.StartDate;
                    existingSurvey.EndDate = survey.EndDate;
                    existingSurvey.TargetAudience = survey.TargetAudience;
                    existingSurvey.IsActive = survey.IsActive;

            
                    _context.SurveyQuestions.RemoveRange(existingSurvey.Questions);

                    if (QText != null)
                    {
                        for (int i = 0; i < QText.Count; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(QText[i]))
                            {
                                string optionsValue = "";
                                if (QOptions != null && QOptions.Count > i && !string.IsNullOrEmpty(QOptions[i]))
                                {
                                    optionsValue = QOptions[i];
                                }

                                var question = new SurveyQuestion
                                {
                                    SurveyId = existingSurvey.Id,
                                    QuestionText = QText[i],
                                    QuestionTextEn = (QTextEn != null && QTextEn.Count > i && !string.IsNullOrEmpty(QTextEn[i])) ? QTextEn[i] : QText[i],
                                    Type = QType[i],
                                    Options = optionsValue
                                };
                                _context.SurveyQuestions.Add(question);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث الاستبيان بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Surveys.Any(e => e.Id == id)) return NotFound();
                    else throw;
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
                TempData["Success"] = survey.IsActive ? "تم تفعيل الاستبيان" : "تم إيقاف الاستبيان";
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
                TempData["Success"] = "تم حذف الاستبيان";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}