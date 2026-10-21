using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Models;
using System.Globalization;

namespace project.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly AppDbContext _context;

        public ScheduleController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(int emp_id, string password)
        {
            var user = _context.Employees.FirstOrDefault(e => e.emp_id == emp_id);

            if (user != null)
            {
                string dbPassword = (user.phone ?? "").Trim();
                string inputPassword = (password ?? "").Trim();

                if (dbPassword == inputPassword)
                {
                    HttpContext.Session.SetInt32("EmpId", user.emp_id);
                    HttpContext.Session.SetString("Name", user.name ?? "員工");

                    string currentRole = (user.job_title ?? "Staff").Trim();
                    HttpContext.Session.SetString("Role", currentRole);

                    if (currentRole == "Manager")
                    {
                        return RedirectToAction("ManagerDashboard");
                    }
                    else
                    {
                        return RedirectToAction("StaffDashboard");
                    }
                }
            }

            ViewBag.Error = "工號或密碼錯誤！";
            return View();
        }

        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(int emp_id, string name, string password)
        {
            var existEmp = _context.Employees.FirstOrDefault(e => e.emp_id == emp_id && e.name == name);
            if (existEmp == null)
            {
                ViewBag.Error = "認證失敗：找不到符合該工號與姓名的員工！";
                return View();
            }
            existEmp.phone = password;
            _context.SaveChanges();
            return RedirectToAction("Login");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public IActionResult ManagerDashboard(string? week, int? searchEmpId)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            DateTime startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            if (!string.IsNullOrEmpty(week))
            {
                int year = int.Parse(week.Split("-W")[0]);
                int weekNum = int.Parse(week.Split("-W")[1]);
                startOfWeek = ISOWeek.ToDateTime(year, weekNum, DayOfWeek.Monday);
            }

            var weekDates = Enumerable.Range(0, 7).Select(i => startOfWeek.AddDays(i)).ToList();
            ViewBag.WeekDates = weekDates;
            ViewBag.SelectedWeek = week ?? $"{DateTime.Today.Year}-W{ISOWeek.GetWeekOfYear(DateTime.Today)}";

            ViewBag.Employees = _context.Employees.ToList();
            ViewBag.SelectedEmpId = searchEmpId;
            ViewBag.TimeSlots = _context.TimeSlots.ToList();

            var endOfWeek = weekDates.Last();
            var query = _context.Schedules.Include(s => s.Employee).Include(s => s.TimeSlot)
                                        .Where(s => s.sche_date >= startOfWeek && s.sche_date <= endOfWeek);

            if (searchEmpId.HasValue)
            {
                query = query.Where(s => s.emp_id == searchEmpId.Value);
            }

            var schedules = query.ToList();
            return View(schedules);
        }
        public IActionResult AttendanceReport(int? searchEmpId, string? searchMonth)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var emps = _context.Employees.ToList();
            ViewBag.Employees = emps;
            ViewBag.SelectedEmpId = searchEmpId;
            var availableMonths = _context.Schedules
                .Select(s => s.sche_date)
                .AsEnumerable()
                .Select(d => d.ToString("yyyy-MM"))
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            if (!availableMonths.Any()) availableMonths.Add(DateTime.Today.ToString("yyyy-MM"));
            ViewBag.AvailableMonths = availableMonths;

            if (string.IsNullOrEmpty(searchMonth))
            {
                searchMonth = availableMonths.First();
            }
            ViewBag.SelectedMonth = searchMonth;

            var allSchedules = _context.Schedules
                .Include(s => s.Employee)
                .Include(s => s.TimeSlot)
                .OrderBy(s => s.sche_date)
                .ToList();

            var filteredSchedules = allSchedules;
            filteredSchedules = filteredSchedules.Where(s => s.sche_date.ToString("yyyy-MM") == searchMonth).ToList();

            if (searchEmpId.HasValue)
            {
                filteredSchedules = filteredSchedules.Where(s => s.emp_id == searchEmpId.Value).ToList();
            }

            double totalHours = 0;
            decimal totalSalary = 0;
            int onTimeCount = 0;
            int lateCount = 0;
            int leaveCount = 0;

            foreach (var s in filteredSchedules)
            {
                string currentStatus = s.status ?? "";

                if (currentStatus.Contains("遲到")) { lateCount++; }
                else if (currentStatus.Contains("上班") || currentStatus == "Scheduled") { onTimeCount++; }
                else if (currentStatus.Contains("Leave") || currentStatus.Contains("請假")) { leaveCount++; }

                var inMatch = System.Text.RegularExpressions.Regex.Match(currentStatus, @"(?:上班|遲到)(?:於)?(\d{2}:\d{2})");
                var outMatch = System.Text.RegularExpressions.Regex.Match(currentStatus, @"下班(?:於)?(\d{2}:\d{2})");

                if (inMatch.Success && outMatch.Success)
                {
                    try
                    {
                        DateTime tIn = DateTime.ParseExact(inMatch.Groups[1].Value, "HH:mm", null);
                        DateTime tOut = DateTime.ParseExact(outMatch.Groups[1].Value, "HH:mm", null);
                        if (tOut < tIn) tOut = tOut.AddDays(1);

                        TimeSpan duration = tOut - tIn;
                        double actualHours = duration.TotalHours;

                        totalHours += actualHours;
                        totalSalary += Convert.ToDecimal(actualHours) * (s.Employee?.salary ?? 180);
                    }
                    catch { }
                }
            }

            ViewBag.TotalHours = Math.Round(totalHours, 1);
            ViewBag.TotalSalary = Math.Round(totalSalary, 2);
            ViewBag.OnTimeCount = onTimeCount;
            ViewBag.LateCount = lateCount;
            ViewBag.LeaveCount = leaveCount;

            return View(filteredSchedules);
        }

        public IActionResult LeaveReview()
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var pendingLeaves = _context.LeaveRequests.Include(l => l.Employee)
                                                    .Where(l => l.status == "Pending" || l.status == "待審核").ToList();

            ViewBag.HistoryLeaves = _context.LeaveRequests.Include(l => l.Employee)
                                                        .Where(l => l.status != "Pending" && l.status != "待審核").ToList();

            return View(pendingLeaves);
        }

        [HttpPost]
        public IActionResult AssignSchedule([FromBody] ScheduleDto data)
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return Json(new { success = false, message = "權限不足！" });

            try
            {
                var targetDate = DateTime.Parse(data.Date);

                var existing = _context.Schedules.FirstOrDefault(s => s.emp_id == data.EmpId && s.sche_date == targetDate && s.slot_id == data.SlotId);

                if (existing == null)
                {
                    var emp = _context.Employees.FirstOrDefault(e => e.emp_id == data.EmpId);
                    if (emp == null)
                    {
                        return Json(new { success = false, message = "排班失敗：找不到該員工資料！" });
                    }

                    int validPosId = _context.Positions
                        .Where(p => p.pos_name == emp.job_title)
                        .Select(p => p.pos_id)
                        .FirstOrDefault();

                    if (validPosId == 0)
                    {
                        validPosId = _context.Positions.Where(p => p.pos_id == data.SlotId).Select(p => p.pos_id).FirstOrDefault();
                    }
                    if (validPosId == 0)
                    {
                        validPosId = _context.Positions.Select(p => p.pos_id).FirstOrDefault();
                    }

                    if (validPosId == 0)
                    {
                        return Json(new { success = false, message = "排班失敗：系統內無任何有效的崗位設定(dbo.Position)！" });
                    }

                    var sched = new Schedule
                    {
                        emp_id = data.EmpId,
                        pos_id = validPosId,
                        slot_id = data.SlotId,
                        manager_id = HttpContext.Session.GetInt32("EmpId") ?? 1001,
                        sche_date = targetDate,
                        status = "Scheduled"
                    };

                    _context.Schedules.Add(sched);
                    _context.SaveChanges();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "資料庫寫入失敗：" + innerMsg });
            }
        }

        [HttpPost]
        public IActionResult UpdateScheduleStatus([FromBody] StatusDto data)
        {
            if (HttpContext.Session.GetString("Role") != "Manager")
                return Json(new { success = false, message = "權限不足！" });

            var sched = _context.Schedules.Find(data.ScheId);
            if (sched != null)
            {
                sched.status = data.Status;
                _context.SaveChanges();
            }
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult ReviewLeave(int leaveId, string action)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var leave = _context.LeaveRequests.Find(leaveId);
            if (leave != null)
            {
                leave.status = action == "Approve" ? "Approved" : "Rejected";

                if (action == "Approve")
                {
                    var sched = _context.Schedules.FirstOrDefault(s => s.emp_id == leave.emp_id && s.sche_date.Date == leave.leave_date.Date);
                    if (sched != null)
                    {
                        sched.status = "Leave";

                        var busyEmpIds = _context.Schedules
                            .Where(s => s.sche_date.Date == leave.leave_date.Date)
                            .Select(s => s.emp_id)
                            .ToList();

                        var leaveUserJob = _context.Employees
                            .Where(e => e.emp_id == leave.emp_id)
                            .Select(e => e.job_title)
                            .FirstOrDefault();

                        var luckyBackupEmp = _context.Employees
                            .Where(e => e.job_title == leaveUserJob && !busyEmpIds.Contains(e.emp_id))
                            .AsEnumerable()
                            .OrderBy(e => Guid.NewGuid())
                            .FirstOrDefault();

                        if (luckyBackupEmp != null)
                        {
                            var backupSched = new Schedule
                            {
                                emp_id = luckyBackupEmp.emp_id,
                                pos_id = sched.pos_id,
                                slot_id = sched.slot_id,
                                manager_id = HttpContext.Session.GetInt32("EmpId") ?? 1001,
                                sche_date = leave.leave_date,
                                status = "Scheduled"
                            };
                            _context.Schedules.Add(backupSched);
                        }
                    }
                }
                _context.SaveChanges();
            }
            return RedirectToAction("LeaveReview");
        }

        public IActionResult DeleteSchedule(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var s = _context.Schedules.Find(id);
            if (s != null) { _context.Schedules.Remove(s); _context.SaveChanges(); }
            return RedirectToAction("ManagerDashboard");
        }

        public IActionResult EmployeeIndex()
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            ViewBag.Positions = _context.Positions.Select(p => p.pos_name).ToList();

            return View(_context.Employees.ToList());
        }

        [HttpPost]
        public IActionResult EmployeeCreate(Employee emp)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var exist = _context.Employees.Find(emp.emp_id);
            if (exist != null)
            {
                TempData["Error"] = "新增失敗：工號已存在！";
                return RedirectToAction("EmployeeIndex");
            }

            _context.Employees.Add(emp);
            _context.SaveChanges();
            return RedirectToAction("EmployeeIndex");
        }

        [HttpPost]
        public IActionResult EmployeeUpdate(Employee emp)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var existing = _context.Employees.Find(emp.emp_id);
            if (existing != null)
            {
                existing.name = emp.name;
                existing.job_title = emp.job_title;
                existing.employment_type = emp.employment_type;
                existing.salary = emp.salary;
                _context.SaveChanges();
            }
            return RedirectToAction("EmployeeIndex");
        }

        public IActionResult EmployeeDelete(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Manager") return RedirectToAction("Login");

            var emp = _context.Employees.Find(id);
            if (emp != null)
            {
                var relativeLeaves = _context.LeaveRequests.Where(l => l.emp_id == id);
                _context.LeaveRequests.RemoveRange(relativeLeaves);
                var relativeSchedules = _context.Schedules.Where(s => s.emp_id == id);
                _context.Schedules.RemoveRange(relativeSchedules);
                _context.Employees.Remove(emp);
                _context.SaveChanges();
            }
            return RedirectToAction("EmployeeIndex");
        }

        public IActionResult StaffDashboard(string? week)
        {
            int? empId = HttpContext.Session.GetInt32("EmpId");
            if (empId == null) return RedirectToAction("Login");

            DateTime startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
            if (!string.IsNullOrEmpty(week))
            {
                int year = int.Parse(week.Split("-W")[0]);
                int weekNum = int.Parse(week.Split("-W")[1]);
                startOfWeek = ISOWeek.ToDateTime(year, weekNum, DayOfWeek.Monday);
            }

            var weekDates = Enumerable.Range(0, 7).Select(i => startOfWeek.AddDays(i)).ToList();
            ViewBag.WeekDates = weekDates;
            ViewBag.SelectedWeek = week ?? $"{DateTime.Today.Year}-W{ISOWeek.GetWeekOfYear(DateTime.Today)}";

            DateTime endOfWeek = weekDates.Last();

            var myHistorySchedules = _context.Schedules
                .Include(s => s.TimeSlot)
                .Where(s => s.emp_id == empId)
                .AsEnumerable()
                .Where(s => s.sche_date.Date == DateTime.Today.Date ||
                            (!s.status.Contains("下班") && !s.status.Contains("Leave") && !s.status.Contains("請假")))
                .OrderByDescending(s => s.sche_date)
                .ToList();

            ViewBag.MyLeaves = _context.LeaveRequests.Where(l => l.emp_id == empId).ToList();

            ViewBag.TimeSlots = _context.TimeSlots.ToList();
            var myWeeklySchedules = _context.Schedules.Include(s => s.TimeSlot)
                .Where(s => s.emp_id == empId && s.sche_date >= startOfWeek && s.sche_date <= endOfWeek)
                .ToList();
            ViewBag.MyWeeklySchedules = myWeeklySchedules;

            return View(myHistorySchedules);
        }

        [HttpPost]
        public IActionResult PunchClock(int scheId, string type, double lat, double lon)
        {
            var sched = _context.Schedules.Include(s => s.TimeSlot).FirstOrDefault(s => s.sche_id == scheId);
            if (sched != null)
            {
                string timeStr = DateTime.Now.ToString("HH:mm");
                string newStatus = "";

                if (type == "上班")
                {
                    bool isLate = false;
                    if (sched.TimeSlot != null)
                    {
                        TimeSpan currentTime = DateTime.Now.TimeOfDay;

                        if (currentTime > sched.TimeSlot.start_time)
                        {
                            isLate = true;
                        }
                    }

                    newStatus = isLate ? $"遲到{timeStr}" : $"上班{timeStr}";
                }
                else
                {
                    newStatus = $"下班{timeStr}(元智一館)";
                }

                string currentStatus = (sched.status ?? "").Trim();
                currentStatus = currentStatus.Replace("(地點:元智一館)", "").Trim();

                if (string.IsNullOrEmpty(currentStatus) || currentStatus == "Scheduled")
                {
                    sched.status = newStatus;
                }
                else
                {
                    if (!currentStatus.Contains(type))
                    {
                        sched.status = currentStatus + " " + newStatus;
                    }
                }

                _context.SaveChanges();
            }
            return RedirectToAction("StaffDashboard");
        }

        [HttpPost]
        public IActionResult ApplyLeave(DateTime leave_date, string reason)
        {
            int? empId = HttpContext.Session.GetInt32("EmpId");
            if (empId == null) return RedirectToAction("Login");

            try
            {
                bool hasSchedule = _context.Schedules.Any(s => s.emp_id == empId.Value && s.sche_date.Date == leave_date.Date);

                if (!hasSchedule)
                {
                    return Content($"<script>alert('申請失敗：{leave_date.ToString("yyyy-MM-dd")} 當天您並沒有排班，毋須請假！'); window.location.href='{Url.Action("StaffDashboard")}';</script>", "text/html; charset=utf-8");
                }

                var leave = new LeaveRequest();

                int maxId = _context.LeaveRequests.Any() ? _context.LeaveRequests.Max(l => l.leave_id) : 0;
                leave.leave_id = maxId + 1;

                leave.emp_id = empId.Value;
                leave.leave_date = leave_date;
                leave.leave_type = string.IsNullOrEmpty(reason) ? "事假" : reason;
                leave.status = "待審核";

                _context.LeaveRequests.Add(leave);
                _context.SaveChanges();

                return Content($"<script>alert('請假申請已送出，等待主管審核！'); window.location.href='{Url.Action("StaffDashboard")}';</script>", "text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Content($"<script>alert('執行失敗：{innerMsg}'); window.location.href='{Url.Action("StaffDashboard")}';</script>", "text/html; charset=utf-8");
            }
        }

        public class ScheduleDto
        {
            public int EmpId { get; set; }
            public string Date { get; set; } = null!;
            public int SlotId { get; set; }
        }

        public class StatusDto
        {
            public int ScheId { get; set; }
            public string Status { get; set; } = null!;
        }
    }
}