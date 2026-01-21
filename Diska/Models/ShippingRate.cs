using System.ComponentModel.DataAnnotations;

namespace Diska.Models
{
    public class ShippingRate
    {
        public int Id { get; set; }

        [Required]
        public string Governorate { get; set; } 

        public string City { get; set; } 
        [Required]
        public decimal Cost { get; set; } 
    }
}