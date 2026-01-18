using System;
using System.ComponentModel.DataAnnotations;

namespace Diska.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public string UserId { get; set; } 
        public string Action { get; set; } 
        public string EntityName { get; set; } 
        public string EntityId { get; set; }
        public string Details { get; set; } 
        public string IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}