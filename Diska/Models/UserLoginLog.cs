using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diska.Models
{
    public class UserLoginLog
    {
        public int Id { get; set; }

        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public DateTime LoginTime { get; set; } = DateTime.Now;
        public string IpAddress { get; set; }
        public string DeviceInfo { get; set; } 
        public bool IsSuccess { get; set; } 
        public string FailureReason { get; set; }
    }
}