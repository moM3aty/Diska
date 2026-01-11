using System;
using System.ComponentModel.DataAnnotations;

namespace Diska.Models
{
    public class Banner
    {
        public int Id { get; set; }

        [Display(Name = "العنوان (عربي)")]
        [Required(ErrorMessage = "العنوان مطلوب")]
        public string Title { get; set; }

        [Display(Name = "العنوان (إنجليزي)")]
        [Required(ErrorMessage = "العنوان مطلوب")]
        public string TitleEn { get; set; }

        public string Subtitle { get; set; }
        public string SubtitleEn { get; set; }

        public string ImageDesktop { get; set; }
        public string ImageMobile { get; set; }  

        public string LinkType { get; set; } = "External"; 
        public string LinkId { get; set; } 

        public string ButtonText { get; set; } = "تسوق الآن";
        public string ButtonTextEn { get; set; } = "Shop Now";

        public int Priority { get; set; } = 0; 
        public bool IsActive { get; set; } = true;

        [DataType(DataType.DateTime)]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [DataType(DataType.DateTime)]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(30);

        public string Status
        {
            get
            {
                if (!IsActive) return "Inactive";
                if (DateTime.Now < StartDate) return "Scheduled";
                if (DateTime.Now > EndDate) return "Expired";
                return "Active";
            }
        }
    }
}