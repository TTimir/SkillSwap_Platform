using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap_Platform.Models;
using SkillSwap_Platform.Models.ViewModels.AdminControl.BillingPlans;
using SkillSwap_Platform.Models.ViewModels.PaymentGatway;
using SkillSwap_Platform.Services.Email;
using SkillSwap_Platform.Services.Payment_Gatway;

namespace SkillSwap_Platform.Controllers.AdminDashboard
{
    [Authorize(AuthenticationSchemes = "SkillSwapAuth", Roles = "Admin")]
    [Route("Admin/[controller]")]
    [Route("Admin/[controller]/[action]")]
    public class AdminBillingController : Controller
    {
        private readonly SkillSwapDbContext _db;
        private readonly ISubscriptionService _subs;
        private readonly IEmailService _emailSender;

        public AdminBillingController(SkillSwapDbContext db,
                                      ISubscriptionService subs,
                                      IEmailService emailService)
        {
            _db = db;
            _subs = subs;
            _emailSender = emailService;
        }

        private bool IsFree(Subscription sub) =>
            string.Equals(sub.PlanName, "Free", StringComparison.OrdinalIgnoreCase);

        private IActionResult RedirectToIndexWithError(string message)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Index));
        }

        private IActionResult RedirectToIndexWithSuccess(string message)
        {
            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/AdminBilling
        public async Task<IActionResult> Index(string term, int page = 1, int pageSize = 20)
        {
            // 1) build the raw join
            var joined = from s in _db.Subscriptions
                         join u in _db.TblUsers
                           on s.UserId equals u.UserId into users
                         from u in users.DefaultIfEmpty()
                         select new { Subscription = s, User = u };

            // 2) apply search *before* projection
            if (!string.IsNullOrWhiteSpace(term))
            {
                joined = joined.Where(x =>
                     (x.User.FirstName + " " + x.User.LastName + " (" + x.User.UserName + ")").Contains(term)
                  || x.User.Email.Contains(term)
                  || x.Subscription.PlanName.Contains(term)
                );
            }

            // 3) count & page
            var total = await joined.CountAsync();
            var pageItems = await joined
                .OrderByDescending(x => x.Subscription.StartDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            // 4) project *after* filtering
            var subsPage = pageItems.Select(x => new AdminSubscriptionItem
            {
                Id = x.Subscription.Id,
                Name = $"{x.User.FirstName} {x.User.LastName} ({x.User.UserName})",
                UserEmail = x.User?.Email ?? "(unknown)",
                PlanName = x.Subscription.PlanName,
                BillingCycle = x.Subscription.BillingCycle,
                StartDate = x.Subscription.StartDate,
                EndDate = x.Subscription.EndDate,
                IsAutoRenew = x.Subscription.IsAutoRenew
            }).ToList();

            // 5) build VM
            var vm = new AdminBillingIndexVM
            {
                Term = term,
                Subscriptions = subsPage,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };
            return View(vm);
        }

        // GET: /Admin/AdminBilling/Cancellations
        public async Task<IActionResult> Cancellations(int page = 1, int pageSize = 20)
        {
            // 1) total count
            var total = await _db.CancellationRequests.CountAsync();

            // 2) single LINQ query that joins everything and pages
            var paged = from c in _db.CancellationRequests
                        join s in _db.Subscriptions
                            on c.SubscriptionId equals s.Id into subs
                        from s in subs.DefaultIfEmpty()
                        join u in _db.TblUsers
                            on s.UserId equals u.UserId into users
                        from u in users.DefaultIfEmpty()
                        orderby c.RequestedAt descending
                        select new AdminCancellationItem
                        {
                            Id = c.Id,
                            Name = $"{u.FirstName} {u.LastName} ({u.UserName})",
                            SubscriptionId = c.SubscriptionId,
                            UserEmail = u != null ? u.Email : "(unknown)",
                            PlanName = s != null ? s.PlanName : "(deleted)",
                            RequestedAt = c.RequestedAt,
                            Reason = c.Reason
                        };

            var items = await paged
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            // 3) build VM
            var vm = new AdminCancellationIndexVM
            {
                Requests = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            };

            return View(vm);
        }

        // POST: /Admin/AdminBilling/ToggleAutoRenew/5
        [HttpPost]
        public async Task<IActionResult> ToggleAutoRenew(int id)
        {
            var sub = await _db.Subscriptions.FindAsync(id);
            if (sub == null) return NotFound();

            if (IsFree(sub))
                return RedirectToIndexWithError("Free plan does not support auto-renew.");

            try
            {
                sub.IsAutoRenew = !sub.IsAutoRenew;
                await _db.SaveChangesAsync();

                // Send confirmation email
                var user = await _db.TblUsers.FindAsync(sub.UserId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] Your subscription auto-renew status has been updated";

                    // 3) build your body however you like
                    var status = sub.IsAutoRenew ? "enabled" : "disabled";
                    var message = $@"Hello {user.FirstName},
                        As per your request, our moderator team has {status} the auto-renew for your subscription (ID: {sub.Id}).
                        Your next billing date remains {sub.EndDate.ToLocalTime().ToString("MMMM d, yyyy")}.
                        
                        If you have any questions or need further assistance, please reply to this email.
                        
                        Warm regards,
                        The SkillSwap Admin Team";
                    await _emailSender.SendEmailAsync(user.Email, subject, message);
                }
                var uname = user?.UserName ?? "(unknown)";
                var fullname = user != null
                               ? $"{user.FirstName} {user.LastName}"
                               : "(unknown)";
                TempData["Success"] =
                $"Moderator has turned {(sub.IsAutoRenew ? "ON" : "OFF")} auto-renew for {uname} ({fullname})’s “{sub.PlanName}” plan.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToIndexWithError("Error updating auto-renew.");
            }
        }

        // POST: /Admin/AdminBilling/ForceRenew/5
        [HttpPost]
        public async Task<IActionResult> ForceRenew(int id)
        {
            var sub = await _db.Subscriptions.FindAsync(id);
            if (sub == null) return NotFound();

            if (IsFree(sub))
                return RedirectToIndexWithError("Cannot renew the Free plan.");

            try
            {
                var start = sub.EndDate;
                var end = sub.BillingCycle == "yearly"
                            ? start.AddYears(1)
                            : start.AddMonths(1);

                await _subs.UpsertAsync(sub.UserId, sub.PlanName, sub.BillingCycle, start, end);

                var user = await _db.TblUsers.FindAsync(sub.UserId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] Your subscription has been renewed";
                    var message = $@"Hello {user.FirstName},
                        At your request, our moderator team has manually renewed your subscription (ID: {sub.Id}).
                        Your new subscription period is from {start.ToLocalTime().ToString("MMMM d, yyyy")} to {end.ToLocalTime().ToString("MMMM d, yyyy")}.
                        
                        Thank you for continuing to choose SkillSwap.
                        
                        Best regards,
                        The SkillSwap Admin Team";
                    await _emailSender.SendEmailAsync(user.Email, subject, message);
                }
                var uname = user?.UserName ?? "(unknown)";
                var fullname = user != null
                               ? $"{user.FirstName} {user.LastName}"
                               : "(unknown)";
                TempData["Success"] =
                $"Moderator has manually renewed {uname} ({fullname})’s “{sub.PlanName}” plan until {end:yyyy-MM-dd}.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToIndexWithError("Error forcing renewal.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReducePeriod(int id)
        {
            var sub = await _db.Subscriptions.FindAsync(id);
            if (sub == null) return NotFound();

            if (IsFree(sub))
                return RedirectToIndexWithError("Cannot adjust period on the Free plan.");

            try
            {
                // Determine the new end date by subtracting one cycle
                var newEnd = sub.BillingCycle switch
                {
                    "yearly" => sub.EndDate.AddYears(-1),
                    "monthly" => sub.EndDate.AddMonths(-1),
                    _ => sub.EndDate.AddMonths(-1)
                };

                // Never allow end < start
                if (newEnd <= sub.StartDate)
                {
                    TempData["Error"] = "Cannot remove a cycle: subscription would end before it began.";
                }

                var oldEnd = sub.EndDate;
                sub.EndDate = newEnd;
                await _db.SaveChangesAsync();

                var user = await _db.TblUsers.FindAsync(sub.UserId);
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] Your subscription period has been adjusted";
                    var cycleText = sub.BillingCycle == "yearly" ? "year" : "month";
                    var message = $@"Hello {user.FirstName},

                        Per your request, our moderator team has reduced your subscription (ID: {sub.Id}) by one {cycleText}.
                        Previously set to end on {oldEnd.ToLocalTime().ToString("MMMM d, yyyy")}, it will now end on {newEnd.ToLocalTime().ToString("MMMM d, yyyy")}.
                        
                        If this isn’t correct, please let us know.
                        
                        Sincerely,
                        The SkillSwap Admin Team";
                    await _emailSender.SendEmailAsync(user.Email, subject, message);
                }
                var uname = user?.UserName ?? "(unknown)";
                var fullname = user != null
                               ? $"{user.FirstName} {user.LastName}"
                               : "(unknown)";
                TempData["Success"] =
                $"Moderator has reduced {uname} ({fullname})’s “{sub.PlanName}” plan by one " +
                $"{(sub.BillingCycle == "yearly" ? "year" : "month")} — now ending on {newEnd:yyyy-MM-dd}.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToIndexWithError("Error reducing period.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Downgrade(int id, string newPlan)
        {
            // Fetch the subscription
            var sub = await _db.Subscriptions.FindAsync(id);
            if (sub == null)
                return NotFound();

            // Define your tier order
            var rank = new Dictionary<string, int>
            {
                ["Free"] = 0,
                ["Plus"] = 1,
                ["Pro"] = 2,
                ["Growth"] = 3
            };

            // Only allow strictly lower tiers
            if (!rank.ContainsKey(newPlan) || rank[newPlan] >= rank[sub.PlanName])
            {
                TempData["Error"] = "Invalid downgrade.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var now = DateTime.UtcNow;
                var remaining = sub.EndDate > now ? sub.EndDate - now : TimeSpan.Zero;
                var oldPlan = sub.PlanName;

                var originalCycle = sub.BillingCycle; 
                if (newPlan == "Free")
                {
                    sub.PlanName = "Free";
                    sub.BillingCycle = "Free Cycle";
                    sub.IsAutoRenew = false;
                    sub.StartDate = now;
                    sub.EndDate = now;
                }
                else
                {
                    sub.PlanName = newPlan;
                    sub.BillingCycle = originalCycle;
                    sub.IsAutoRenew = false;
                    sub.StartDate = now;
                    sub.EndDate = now + remaining;
                }

                await _db.SaveChangesAsync();

                var user = await _db.TblUsers.FindAsync(sub.UserId);
                var newEnd = now + remaining;
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] Your subscription has been downgraded";
                    var message = $@"Hello {user.FirstName},

                    As requested, our moderator team has downgraded your subscription (ID: {sub.Id}) from {oldPlan} to {newPlan}.
                    Your subscription will now run until {newEnd.ToLocalTime().ToString("MMMM d, yyyy")}.
                    
                    If you need to make further changes, feel free to contact us.
                    
                    Regards,
                    The SkillSwap Admin Team";
                    await _emailSender.SendEmailAsync(user.Email, subject, message);
                }
                var uname = user?.UserName ?? "(unknown)";
                var fullname = user != null
                                   ? $"{user.FirstName} {user.LastName}"
                                   : "(unknown)";
                TempData["Success"] =
                $"Moderator has downgraded {uname} ({fullname})’s plan from “{oldPlan}” to “{newPlan}.”";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToIndexWithError("Error during downgrade.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSubscription(int id, string newPlan, string newCycle)
        {
            // 1) Fetch
            var sub = await _db.Subscriptions.FindAsync(id);
            if (sub == null) return NotFound();

            // 2) Validate plan
            var rank = new Dictionary<string, int>
            {
                ["Free"] = 0,
                ["Plus"] = 1,
                ["Pro"] = 2,
                ["Growth"] = 3
            };
            if (!rank.ContainsKey(newPlan) || !new[] { "monthly", "yearly" }.Contains(newCycle))
            {
                TempData["Error"] = "Invalid plan or cycle.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // 3) You may want to adjust dates for a plan switch:
                //    Here we simply keep the remaining time window intact.
                var now = DateTime.UtcNow;
                var remaining = sub.EndDate > now ? sub.EndDate - now : TimeSpan.Zero;
                var oldPlan = sub.PlanName;
                var oldCycle = sub.BillingCycle;

                if (newPlan == "Free")
                {
                    sub.PlanName = "Free";
                    sub.BillingCycle = null;
                    sub.IsAutoRenew = false;
                    sub.StartDate = now;
                    sub.EndDate = now;
                }
                else
                {
                    sub.PlanName = newPlan;
                    sub.BillingCycle = newCycle;
                    sub.StartDate = now;
                    sub.EndDate = now + remaining;
                    sub.IsAutoRenew = false;
                }

                await _db.SaveChangesAsync();

                var user = await _db.TblUsers.FindAsync(sub.UserId);
                var newEnd = now + remaining;
                if (user != null)
                {
                    // 1) figure out their active tier & SLA
                    var activeSub = await _subs.GetActiveAsync(user.UserId);
                    var (supportLabel, sla) = (activeSub?.PlanName ?? "Free") switch
                    {
                        "Plus" => ("Plus Support", "72h SLA"),
                        "Pro" => ("Pro Support", "48h SLA"),
                        "Growth" => ("Growth Support", "24h SLA"),
                        _ => ("Free Support", "120h SLA")
                    };

                    // 2) build a prefixed subject
                    var subject = $"[{supportLabel} · {sla}] Your subscription has been updated";
                    var message = $@"Hello {user.FirstName},

                        Per your request, our moderator team has updated your subscription (ID: {sub.Id}) from {oldPlan} ({oldCycle}) to {newPlan} ({newCycle}).
                        Your subscription will now expire on {newEnd.ToLocalTime().ToString("MMMM d, yyyy")}.
                        
                        Thank you for being part of SkillSwap.
                        
                        Best,
                        The SkillSwap Admin Team";
                    await _emailSender.SendEmailAsync(user.Email, subject, message);
                }
                var uname = user?.UserName ?? "(unknown)";
                var fullname = user != null
                                   ? $"{user.FirstName} {user.LastName}"
                                   : "(unknown)";
                TempData["Success"] =
                      $"Moderator has updated {uname} ({fullname})’s subscription from “{oldPlan} ({oldCycle})” " +
                      $"to “{newPlan} ({newCycle}).”";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return RedirectToIndexWithError("Error editing subscription.");
            }
        }

        [HttpGet, Route("Admin/AdminBilling/History/{userId}")]
        public async Task<IActionResult> History(int userId, int page = 1, int pageSize = 10)
        {
            var user = await _db.TblUsers
                        .Where(u => u.UserId == userId)
                        .Select(u => new { u.UserName, FullName = u.FirstName + " " + u.LastName })
                        .SingleOrDefaultAsync();
            if (user == null) return NotFound();

            // pull all periods
            var subs = await _db.Subscriptions
                               .Where(s => s.UserId == userId)
                               .OrderBy(s => s.StartDate)
                               .AsNoTracking()
                               .ToListAsync();

            var subIds = subs.Select(s => s.Id).ToArray();
            var cancels = await _db.CancellationRequests
                                .Where(c => _db.Subscriptions
                                    .Where(s => s.UserId == userId)
                                    .Select(s => s.Id)
                                    .Any(id => id == c.SubscriptionId))
                                .ToListAsync();

            var payments = await _db.PaymentLogs
                                    .Where(p => p.UserId == userId)
                                    .ToListAsync();

            // build full timeline
            var full = new List<AdminHistoryItem>();
            foreach (var s in subs)
            {
                full.Add(new AdminHistoryItem
                {
                    EventType = "Subscribed",
                    Timestamp = s.StartDate,
                    Description = $"Began {s.PlanName} ({s.BillingCycle})"
                });
                foreach (var p in payments
                                 .Where(p => p.ProcessedAt >= s.StartDate && p.ProcessedAt <= s.EndDate)
                                 .OrderBy(p => p.ProcessedAt))
                {
                    full.Add(new AdminHistoryItem
                    {
                        EventType = "Payment",
                        Timestamp = p.ProcessedAt,
                        Description = $"Payment ID {p.PaymentId.Substring(0, 8)}"
                    });
                }
                var c = cancels.FirstOrDefault(x => x.SubscriptionId == s.Id);
                if (c != null)
                {
                    full.Add(new AdminHistoryItem
                    {
                        EventType = "Cancelled Auto-Renew",
                        Timestamp = c.RequestedAt,
                        Description = c.Reason
                    });
                }
                full.Add(new AdminHistoryItem
                {
                    EventType = "Period Ended",
                    Timestamp = s.EndDate,
                    Description = ""
                });
            }

            // sort & page
            var sorted = full.OrderBy(x => x.Timestamp).ToList();
            var total = sorted.Count;
            var pageItems = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var vm = new AdminHistoryVM
            {
                UserId = userId,
                Username = user.UserName,
                FullName = user.FullName,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Timeline = pageItems
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Users(string term, int page = 1, int pageSize = 10)
        {
            // 1) base query: only users who have at least one subscription
            var usersQ = _db.TblUsers
                .Join(
                   _db.Subscriptions,
                   u => u.UserId,
                   s => s.UserId,
                   (u, s) => u
                )
                .Distinct()  // if a user has multiple subs, we still want them once
                .AsQueryable();

            // 2) optional search filter
            if (!string.IsNullOrWhiteSpace(term))
            {
                usersQ = usersQ.Where(u =>
                    u.UserName.Contains(term) ||
                    u.Email.Contains(term)
                );
            }

            // 3) let SQL do the count + paging
            var total = await usersQ.CountAsync();
            var pageUsers = await usersQ
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            // 4) map to VM, still async everywhere
            var subsTasks = pageUsers
                .Select(u => _subs.GetActiveAsync(u.UserId))
                .ToList();
            var subs = await Task.WhenAll(subsTasks);

            var items = pageUsers
                .Select((u, i) => new AdminUserItem
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    Email = u.Email,
                    CurrentPlan = subs[i]?.PlanName ?? "None",
                    CurrentCycle = subs[i]?.BillingCycle
                })
                .ToList();

            // 5) return your VM
            var vm = new AdminUserListVM
            {
                Term = term,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Users = items
            };
            return View(vm);
        }


        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var cutoff = DateTime.UtcNow.AddMonths(-11);

            // 1) Get raw year/month/count from the DB, no string formatting here
            var rawNewSubs = await _db.Subscriptions
                .Where(s => s.StartDate >= cutoff)
                .GroupBy(s => new { s.StartDate.Year, s.StartDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // 2) Turn raw data into DashboardPoints
            var newSubs = rawNewSubs
                .Select(x => new BillingDashboardPoint
                {
                    Period = $"{x.Year}-{x.Month:00}",
                    Count = x.Count
                })
                .ToList();

            // 3) Same for cancellations
            var rawCancels = await _db.CancellationRequests
                .Where(c => c.RequestedAt >= cutoff)
                .GroupBy(c => new { c.RequestedAt.Year, c.RequestedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            var cancels = rawCancels
                .Select(x => new BillingDashboardPoint
                {
                    Period = $"{x.Year}-{x.Month:00}",
                    Count = x.Count
                })
                .ToList();

            // 4) Active subscriptions at month‐end remains the same
            var active = new List<BillingDashboardPoint>();
            for (int i = 0; i < 12; i++)
            {
                var dt = cutoff.AddMonths(i);
                var monthEnd = new DateTime(dt.Year, dt.Month,
                    DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59);

                var count = await _db.Subscriptions
                    .CountAsync(s => s.StartDate <= monthEnd && s.EndDate > monthEnd);

                active.Add(new BillingDashboardPoint
                {
                    Period = $"{dt.Year}-{dt.Month:00}",
                    Count = count
                });
            }

            // 5) Renewals—raw grouping
            var rawRenewals = await _db.Subscriptions
                .Where(s => s.StartDate >= cutoff
                         && s.StartDate != s.EndDate.AddMonths(-1))
                .GroupBy(s => new { s.StartDate.Year, s.StartDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            var renewals = rawRenewals
                .Select(x => new BillingDashboardPoint
                {
                    Period = $"{x.Year}-{x.Month:00}",
                    Count = x.Count
                })
                .ToList();

            // 6) Assemble and return
            var vm = new AdminBillingDashboardVM
            {
                NewSubscriptions = newSubs,
                Cancellations = cancels,
                ActiveSubscriptions = active,
                Renewals = renewals
            };
            return View(vm);
        }
    }
}
