using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class UserAddress
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [Required(ErrorMessage = "اسم العنوان مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "المحافظة مطلوبة")]
        public string Governorate { get; set; }

        [Required(ErrorMessage = "المدينة/المنطقة مطلوبة")]
        public string City { get; set; }

        [Required(ErrorMessage = "العنوان بالتفصيل مطلوب")]
        public string Street { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        public string PhoneNumber { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public bool IsDefault { get; set; } = false;
    }
}