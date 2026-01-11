using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{

   
    public class UserNotification
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } = "Info";
        public string Link { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 60) return $"منذ {(int)span.TotalMinutes} دقيقة";
                if (span.TotalHours < 24) return $"منذ {(int)span.TotalHours} ساعة";
                return CreatedAt.ToString("dd/MM/yyyy");
            }
        }
    }
}