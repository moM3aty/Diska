using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Display(Name = "الاسم (عربي)")]
        [Required(ErrorMessage = "الاسم العربي مطلوب")]
        public string Name { get; set; }

        [Display(Name = "الاسم (إنجليزي)")]
        [Required(ErrorMessage = "الاسم الانجليزي مطلوب")]
        public string NameEn { get; set; }

        // Media
        public string IconClass { get; set; } // FontAwesome Icon
        public string ImageUrl { get; set; }  // Category Banner/Image

        // Control
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;

        // Hierarchy (Multi-level)
        public int? ParentId { get; set; }
        [ForeignKey("ParentId")]
        public virtual Category Parent { get; set; }
        public virtual ICollection<Category> Children { get; set; }

        // SEO Fields
        public string Slug { get; set; }
        public string MetaTitle { get; set; }
        public string MetaDescription { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }
}