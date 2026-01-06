using System.ComponentModel.DataAnnotations;

namespace Diska.Models
{
    public class Banner
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الصورة مطلوبة")]
        public string ImageUrl { get; set; }

        [Required(ErrorMessage = "العنوان الرئيسي مطلوب")]
        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string LinkUrl { get; set; } = "#"; // رابط الزر

        public string ButtonText { get; set; } = "تسوق الآن";

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; } = 0; // ترتيب الظهور
    }
}