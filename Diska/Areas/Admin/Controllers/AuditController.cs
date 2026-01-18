using Microsoft.AspNetCore.Mvc;
using Diska.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Diska.Models;

namespace Diska.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        // عرض سجلات النظام
        public async Task<IActionResult> Index(string userId, string actionType, string entityName, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.AuditLogs.AsQueryable();

            // الفلاتر
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            if (!string.IsNullOrEmpty(actionType) && actionType != "All")
            {
                query = query.Where(l => l.Action == actionType);
            }

            if (!string.IsNullOrEmpty(entityName) && entityName != "All")
            {
                query = query.Where(l => l.EntityName == entityName);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(l => l.Timestamp >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(l => l.Timestamp < toDate.Value.AddDays(1));
            }

            var logs = await query.OrderByDescending(l => l.Timestamp).Take(150).ToListAsync();

     
            var userIds = logs.Select(l => l.UserId).Distinct().ToList();
            var usersInfo = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { Name = u.FullName, Email = u.Email, Role = "User" }); 

            ViewBag.UsersMap = usersInfo;

            ViewBag.UsersList = new SelectList(await _context.Users.Select(u => new { u.Id, u.FullName }).ToListAsync(), "Id", "FullName", userId);

            var existingEntities = await _context.AuditLogs.Select(l => l.EntityName).Distinct().ToListAsync();
            ViewBag.EntitiesList = new SelectList(existingEntities, entityName);

            ViewBag.ActionTypes = new SelectList(new[] { "Create", "Update", "Delete", "Login", "Approve", "Reject" }, actionType);

            return View(logs);
        }
    }
}