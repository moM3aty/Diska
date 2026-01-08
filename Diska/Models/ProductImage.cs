using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    // جدول جديد لصور المنتج الإضافية
    public class ProductImage
    {
        public int Id { get; set; }

        public string ImageUrl { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}