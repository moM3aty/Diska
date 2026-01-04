using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Survey
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "عنوان الاستبيان مطلوب")]
        public string Title { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);

        public string TargetAudience { get; set; } = "All"; // All, Merchant, Customer

        public virtual List<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
        public virtual List<SurveyResponse> Responses { get; set; } = new List<SurveyResponse>();
    }

    public class SurveyQuestion
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }

        [Required(ErrorMessage = "نص السؤال مطلوب")]
        public string QuestionText { get; set; }

        public string Type { get; set; } = "Text"; // Text, Star, YesNo, Choice
        public string Options { get; set; } // مفصول بفاصلة للاختيارات
    }

    public class SurveyResponse
    {
        public int Id { get; set; }
        public int SurveyId { get; set; }
        public string UserId { get; set; }

        public string AnswerJson { get; set; } // تخزين الإجابات كـ JSON للتبسيط
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }
}