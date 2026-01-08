using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Diska.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم القسم (عربي) مطلوب")]
        public string Name { get; set; }

        [Required(ErrorMessage = "اسم القسم (إنجليزي) مطلوب")] 
        public string NameEn { get; set; }

        public string IconClass { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }
}