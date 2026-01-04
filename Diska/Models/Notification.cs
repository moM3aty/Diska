using System;
using System.ComponentModel.DataAnnotations;

namespace Diska.Models
{
    public class UserNotification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // المستلم

        [Required]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        public string Type { get; set; } = "Info"; // Order, Alert, Offer
        public string Link { get; set; } // رابط التوجيه عند الضغط

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // دالة مساعدة لحساب الوقت المنقضي
        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 60) return $"منذ {Math.Ceiling(span.TotalMinutes)} دقيقة";
                if (span.TotalHours < 24) return $"منذ {Math.Ceiling(span.TotalHours)} ساعة";
                return CreatedAt.ToString("dd/MM/yyyy");
            }
        }
    }
}