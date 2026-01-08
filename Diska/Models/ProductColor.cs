using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class ProductColor
    {
        public int Id { get; set; }

        public string ColorName { get; set; } 
        public string ColorHex { get; set; } 

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}