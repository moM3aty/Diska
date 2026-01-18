using Diska.Models;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Diska.Data
{
    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry)
        {
            Entry = entry;
        }

        public EntityEntry Entry { get; }
        public string UserId { get; set; }
        public string IpAddress { get; set; }
        public string TableName { get; set; }
        public Dictionary<string, object> KeyValues { get; } = new Dictionary<string, object>();
        public Dictionary<string, object> OldValues { get; } = new Dictionary<string, object>();
        public Dictionary<string, object> NewValues { get; } = new Dictionary<string, object>();
        public List<PropertyEntry> TemporaryProperties { get; } = new List<PropertyEntry>();

        public bool HasTemporaryProperties => TemporaryProperties.Any();

        public AuditLog ToAudit()
        {
            var audit = new AuditLog
            {
                UserId = UserId,
                IpAddress = IpAddress,
                EntityName = TableName,
                Timestamp = DateTime.Now
            };

            audit.Action = Entry.State.ToString();

            var details = new Dictionary<string, object>();

            if (Entry.State == Microsoft.EntityFrameworkCore.EntityState.Modified)
            {
                details["Old"] = OldValues;
                details["New"] = NewValues;
            }
            else if (Entry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
            {
                details["New"] = NewValues;
            }
            else if (Entry.State == Microsoft.EntityFrameworkCore.EntityState.Deleted)
            {
                details["Old"] = OldValues;
            }

            audit.Details = details.Count > 0 ? JsonConvert.SerializeObject(details) : "{}";

            audit.EntityId = JsonConvert.SerializeObject(KeyValues);

            return audit;
        }
    }
}