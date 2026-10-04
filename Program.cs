using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

// Listen on the port provided by the hosting platform (e.g. Railway sets PORT).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=ComfortGymDB;User=root;Password=;";

builder.Services.AddDbContext<GymDbContext>(o =>
    o.UseMySql(cs, ServerVersion.AutoDetect(cs)));
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
if (app.Environment.IsDevelopment())
{
    // Only redirect to HTTPS locally; Railway terminates TLS in front of the app.
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

// Ensure DB created and seed sample data
using (var scope = app.Services.CreateScope())
{
    // Ensure DB created safely without cra // Ensure DB created safely without naming conflict
try
{
    using (var dbScope = app.Services.CreateScope())
    {
        var db = dbScope.ServiceProvider.GetRequiredService<GymDbContext>();
        db.Database.EnsureCreated();
    }
}
catch (Exception)
{
    // Ignores temporary DB startup lag on Railway
}shing startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        db.Database.EnsureCreated();
    }
}
catch (Exception)
{
    // Ignores temporary DB startup lag on Railway
}
}

static async Task EnsureMemberCreatorColumnsAsync(GymDbContext db)
{
    var tables = await db.Database.SqlQueryRaw<string>("SHOW TABLES LIKE 'Members'").ToListAsync();
    if (!tables.Any()) return;

    var columns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `Members` LIKE 'CreatedByName'").ToListAsync();
    if (!columns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Members` ADD COLUMN `CreatedByName` VARCHAR(255) NULL;");
    }

    var userColumns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `Members` LIKE 'CreatedByUserId'").ToListAsync();
    if (!userColumns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Members` ADD COLUMN `CreatedByUserId` INT NULL;");
    }
}

static async Task EnsureGymClassScheduleColumnsAsync(GymDbContext db)
{
    var tables = await db.Database.SqlQueryRaw<string>("SHOW TABLES LIKE 'GymClasses'").ToListAsync();
    if (!tables.Any()) return;

    var sessionColumns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `GymClasses` LIKE 'SessionType'").ToListAsync();
    if (!sessionColumns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `GymClasses` ADD COLUMN `SessionType` VARCHAR(50) NULL;");
    }

    var startTimeColumns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `GymClasses` LIKE 'StartTime'").ToListAsync();
    if (!startTimeColumns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `GymClasses` ADD COLUMN `StartTime` VARCHAR(20) NULL;");
    }

    var endTimeColumns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `GymClasses` LIKE 'EndTime'").ToListAsync();
    if (!endTimeColumns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `GymClasses` ADD COLUMN `EndTime` VARCHAR(20) NULL;");
    }

    var statusColumns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `GymClasses` LIKE 'Status'").ToListAsync();
    if (!statusColumns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `GymClasses` ADD COLUMN `Status` VARCHAR(20) NULL DEFAULT 'Pending';");
    }
}

static async Task EnsureTrainerNullValuesAsync(GymDbContext db)
{
    var tables = await db.Database.SqlQueryRaw<string>("SHOW TABLES LIKE 'Trainers'").ToListAsync();
    if (!tables.Any()) return;

    await db.Database.ExecuteSqlRawAsync("UPDATE `Trainers` SET `FullName` = '' WHERE `FullName` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `Trainers` SET `Email` = '' WHERE `Email` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `Trainers` SET `Phone` = '' WHERE `Phone` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `Trainers` SET `Specialization` = 'General Fitness' WHERE `Specialization` IS NULL;");
}

static async Task EnsureGymClassNullValuesAsync(GymDbContext db)
{
    var tables = await db.Database.SqlQueryRaw<string>("SHOW TABLES LIKE 'GymClasses'").ToListAsync();
    if (!tables.Any()) return;

    await db.Database.ExecuteSqlRawAsync("UPDATE `GymClasses` SET `Name` = '' WHERE `Name` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `GymClasses` SET `Description` = '' WHERE `Description` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `GymClasses` SET `SessionType` = 'Morning' WHERE `SessionType` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `GymClasses` SET `StartTime` = '08:00 AM' WHERE `StartTime` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `GymClasses` SET `EndTime` = '10:00 AM' WHERE `EndTime` IS NULL;");
    await db.Database.ExecuteSqlRawAsync("UPDATE `GymClasses` SET `Status` = 'Pending' WHERE `Status` IS NULL;");
}

static async Task EnsureEquipmentColumnsAsync(GymDbContext db)
{
    var tables = await db.Database.SqlQueryRaw<string>("SHOW TABLES LIKE 'Equipment'").ToListAsync();
    if (!tables.Any()) return;

    var statusColumns = await db.Database.SqlQueryRaw<string>("SHOW COLUMNS FROM `Equipment` LIKE 'Status'").ToListAsync();
    if (!statusColumns.Any())
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Equipment` ADD COLUMN `Status` VARCHAR(20) NULL DEFAULT 'Active';");
    }

    await db.Database.ExecuteSqlRawAsync("UPDATE `Equipment` SET `Status` = 'Active' WHERE `Status` IS NULL OR TRIM(`Status`) = ''; ");
}

// Helper to get effective expiry date
static DateTime GetEffectiveExpiry(Member m)
{
    if (m.ExpiryDate.HasValue) return m.ExpiryDate.Value;
    var reg = m.RegistrationDate ?? m.CreatedAt;
    return reg.AddDays(30);
}

static string GetMembershipStatus(DateTime today, DateTime expiryDate)
{
    int daysLeft = (expiryDate.Date - today.Date).Days;
    if (daysLeft < 0) return "Expired";
    if (daysLeft <= 7) return "Expiring Soon";
    return "Active";
}

// Helper to format member with computed status
static object FormatMemberDto(Member m, DateTime today)
{
    var regDate = m.RegistrationDate ?? m.CreatedAt.Date;
    var expDate = GetEffectiveExpiry(m);
    int daysLeft = (expDate.Date - today.Date).Days;
    string status = GetMembershipStatus(today, expDate);

    return new
    {
        id = m.Id,
        fullName = m.FullName,
        email = m.Email,
        phone = m.Phone,
        gender = m.Gender,
        address = m.Address,
        registrationDate = regDate.ToString("yyyy-MM-dd"),
        expiryDate = expDate.ToString("yyyy-MM-dd"),
        membershipPlanId = m.MembershipPlanId,
        planName = m.MembershipPlan?.Name ?? "Monthly Standard",
        status = status,
        daysLeft = daysLeft,
        emergencyContact = m.EmergencyContact ?? "",
        notes = m.Notes ?? "",
        createdByUserId = m.CreatedByUserId,
        createdByName = m.CreatedByName ?? "System",
        createdAt = m.CreatedAt
    };
}

/* DASHBOARD */
app.MapGet("/api/dashboard", async (GymDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var startOfMonth = new DateTime(today.Year, today.Month, 1);
    var members = await db.Members.Include(x => x.MembershipPlan).ToListAsync();

    var activeCount = members.Count(m => GetMembershipStatus(today, GetEffectiveExpiry(m)) != "Expired");
    var expiredCount = members.Count(m => GetMembershipStatus(today, GetEffectiveExpiry(m)) == "Expired");
    var expiringSoonCount = members.Count(m => GetMembershipStatus(today, GetEffectiveExpiry(m)) == "Expiring Soon");

    var totalPayments = await db.Payments.SumAsync(x => (decimal?)x.Amount) ?? 0;
    var monthlyIncome = await db.Payments.Where(x => x.PaymentDate >= startOfMonth).SumAsync(x => (decimal?)x.Amount) ?? 0;
    var trainers = await db.Trainers.CountAsync();

    var recentMembers = members
        .OrderByDescending(x => x.Id)
        .Take(5)
        .Select(m => FormatMemberDto(m, today))
        .ToList();

    var expiringMembers = members
        .Where(m =>
        {
            var exp = GetEffectiveExpiry(m).Date;
            return exp <= today.AddDays(7);
        })
        .OrderBy(m => GetEffectiveExpiry(m))
        .Take(6)
        .Select(m => FormatMemberDto(m, today))
        .ToList();

    return Results.Ok(new
    {
        members = members.Count,
        activeMembers = activeCount,
        expiredMembers = expiredCount,
        expiringSoon = expiringSoonCount,
        activeMemberships = activeCount,
        trainers,
        totalPayments,
        monthlyIncome,
        recentMembers,
        expiringMembers
    });
});

/* MEMBERS */
app.MapGet("/api/members", async (GymDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var list = await db.Members.Include(x => x.MembershipPlan).OrderByDescending(x => x.Id).ToListAsync();
    return Results.Ok(list.Select(m => FormatMemberDto(m, today)));
});

app.MapGet("/api/members/{id:int}", async (int id, GymDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var x = await db.Members.Include(m => m.MembershipPlan).FirstOrDefaultAsync(m => m.Id == id);
    return x is null ? Results.NotFound() : Results.Ok(FormatMemberDto(x, today));
});

app.MapPost("/api/members", async (MemberCreateDto dto, GymDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var regDate = dto.RegistrationDate ?? today;
    var expDate = dto.ExpiryDate ?? regDate.AddDays(30); // Default 1 month (Bille)!

    var member = new Member
    {
        FullName = dto.FullName.Trim(),
        Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
        Phone = dto.Phone?.Trim() ?? "",
        Gender = dto.Gender ?? "",
        Address = dto.Address?.Trim() ?? "",
        DateOfBirth = dto.DateOfBirth,
        RegistrationDate = regDate,
        ExpiryDate = expDate,
        MembershipPlanId = dto.MembershipPlanId,
        EmergencyContact = dto.EmergencyContact?.Trim() ?? "",
        Notes = dto.Notes?.Trim() ?? "",
        CreatedByUserId = dto.CreatedByUserId,
        CreatedByName = string.IsNullOrWhiteSpace(dto.CreatedByName) ? "System" : dto.CreatedByName.Trim(),
        CreatedAt = DateTime.UtcNow,
        Status = GetMembershipStatus(today, expDate)
    };

    db.Members.Add(member);
    await db.SaveChangesAsync();

    // Create a membership record
    var planId = dto.MembershipPlanId ?? (await db.MembershipPlans.Select(p => p.Id).FirstOrDefaultAsync());
    if (planId > 0)
    {
        db.Memberships.Add(new Membership
        {
            MemberId = member.Id,
            MembershipPlanId = planId,
            StartDate = regDate,
            EndDate = expDate,
            Status = member.Status
        });
    }

    // If amount paid upon registration, record payment
    if (dto.AmountPaid.HasValue && dto.AmountPaid.Value > 0)
    {
        db.Payments.Add(new Payment
        {
            MemberId = member.Id,
            Amount = dto.AmountPaid.Value,
            PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod,
            PaymentDate = DateTime.UtcNow,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{member.Id:D3}",
            Notes = $"Initial Registration Fee ({dto.PaymentMethod ?? "Cash"})"
        });
    }

    await db.SaveChangesAsync();
    return Results.Created($"/api/members/{member.Id}", FormatMemberDto(member, today));
});

app.MapPut("/api/members/{id:int}", async (int id, MemberUpdateDto input, GymDbContext db) =>
{
    var x = await db.Members.FindAsync(id);
    if (x is null) return Results.NotFound();

    if (input.RequestedByUserId.HasValue && x.CreatedByUserId.HasValue && input.RequestedByUserId.Value != x.CreatedByUserId.Value)
        return Results.Forbid();

    x.FullName = input.FullName.Trim();
    x.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
    x.Phone = input.Phone?.Trim() ?? "";
    x.Gender = input.Gender ?? "";
    x.Address = input.Address?.Trim() ?? "";
    x.EmergencyContact = input.EmergencyContact?.Trim() ?? "";
    x.Notes = input.Notes?.Trim() ?? "";

    if (input.RegistrationDate.HasValue)
        x.RegistrationDate = input.RegistrationDate.Value;

    if (input.ExpiryDate.HasValue)
        x.ExpiryDate = input.ExpiryDate.Value;

    var today = DateTime.UtcNow.Date;
    x.Status = GetMembershipStatus(today, GetEffectiveExpiry(x));

    await db.SaveChangesAsync();
    return Results.Ok(FormatMemberDto(x, today));
});

app.MapPost("/api/members/{id:int}/renew", async (int id, RenewDto req, GymDbContext db) =>
{
    var member = await db.Members.FindAsync(id);
    if (member is null) return Results.NotFound(new { message = "Member not found." });

    var today = DateTime.UtcNow.Date;
    int days = req.DurationDays ?? 30; // Default 1 month renewal!

    // If current expiry date is in future, add from current expiry date, else from today
    var currentExp = member.ExpiryDate ?? today;
    var baseDate = currentExp > today ? currentExp : today;
    var newExpiry = baseDate.AddDays(days);

    member.ExpiryDate = newExpiry;
    member.Status = "Active";

    // Record Payment
    decimal amount = req.Amount ?? 30m;
    var payment = new Payment
    {
        MemberId = member.Id,
        Amount = amount,
        PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Cash" : req.PaymentMethod,
        PaymentDate = DateTime.UtcNow,
        InvoiceNumber = $"RNW-{DateTime.UtcNow:yyyyMMdd}-{member.Id:D3}",
        Notes = req.Notes ?? $"Membership Renewal (+{days} days)"
    };
    db.Payments.Add(payment);

    // Also update/add membership entry
    var planId = member.MembershipPlanId ?? (await db.MembershipPlans.Select(p => p.Id).FirstOrDefaultAsync());
    if (planId > 0)
    {
        db.Memberships.Add(new Membership
        {
            MemberId = member.Id,
            MembershipPlanId = planId,
            StartDate = baseDate,
            EndDate = newExpiry,
            Status = "Active"
        });
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        success = true,
        message = $"Membership renewed until {newExpiry:yyyy-MM-dd}",
        member = FormatMemberDto(member, today),
        payment = new
        {
            payment.Id,
            payment.Amount,
            payment.PaymentMethod,
            payment.InvoiceNumber,
            paymentDate = payment.PaymentDate.ToString("yyyy-MM-dd HH:mm")
        }
    });
});

app.MapDelete("/api/members/{id:int}", async (int id, int? requestedByUserId, GymDbContext db) =>
{
    var x = await db.Members.FindAsync(id);
    if (x is null) return Results.NotFound();

    if (requestedByUserId.HasValue && x.CreatedByUserId.HasValue && requestedByUserId.Value != x.CreatedByUserId.Value)
        return Results.Forbid();

    db.Members.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* TRAINERS */
app.MapGet("/api/trainers", async (GymDbContext db) => Results.Ok(await db.Trainers.OrderByDescending(x => x.Id).ToListAsync()));
app.MapPost("/api/trainers", async (Trainer x, GymDbContext db) =>
{
    db.Trainers.Add(x);
    await db.SaveChangesAsync();
    return Results.Created($"/api/trainers/{x.Id}", x);
});
app.MapPut("/api/trainers/{id:int}", async (int id, Trainer input, GymDbContext db) =>
{
    var x = await db.Trainers.FindAsync(id);
    if (x is null) return Results.NotFound();
    x.FullName = input.FullName; x.Email = input.Email; x.Phone = input.Phone; x.Specialization = input.Specialization;
    await db.SaveChangesAsync();
    return Results.Ok(x);
});
app.MapDelete("/api/trainers/{id:int}", async (int id, GymDbContext db) =>
{
    var x = await db.Trainers.FindAsync(id);
    if (x is null) return Results.NotFound();
    db.Trainers.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* PLANS */
app.MapGet("/api/membership-plans", async (GymDbContext db) => Results.Ok(await db.MembershipPlans.OrderBy(x => x.Price).ToListAsync()));
app.MapPost("/api/membership-plans", async (MembershipPlan x, GymDbContext db) =>
{
    db.MembershipPlans.Add(x);
    await db.SaveChangesAsync();
    return Results.Created($"/api/membership-plans/{x.Id}", x);
});
app.MapPut("/api/membership-plans/{id:int}", async (int id, MembershipPlan input, GymDbContext db) =>
{
    var x = await db.MembershipPlans.FindAsync(id);
    if (x is null) return Results.NotFound();
    x.Name = input.Name; x.DurationDays = input.DurationDays; x.Price = input.Price; x.Description = input.Description;
    await db.SaveChangesAsync();
    return Results.Ok(x);
});
app.MapDelete("/api/membership-plans/{id:int}", async (int id, GymDbContext db) =>
{
    var x = await db.MembershipPlans.FindAsync(id);
    if (x is null) return Results.NotFound();
    db.MembershipPlans.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* MEMBERSHIPS */
app.MapGet("/api/memberships", async (GymDbContext db) => Results.Ok(await db.Memberships.Include(x => x.Member).Include(x => x.MembershipPlan).OrderByDescending(x => x.Id).ToListAsync()));
app.MapPost("/api/memberships", async (Membership x, GymDbContext db) =>
{
    var plan = await db.MembershipPlans.FindAsync(x.MembershipPlanId);
    if (plan is null || !await db.Members.AnyAsync(m => m.Id == x.MemberId)) return Results.BadRequest();
    x.StartDate = x.StartDate == default ? DateTime.UtcNow.Date : x.StartDate;
    x.EndDate = x.StartDate.AddDays(plan.DurationDays);
    db.Memberships.Add(x);
    await db.SaveChangesAsync();
    return Results.Created($"/api/memberships/{x.Id}", x);
});
app.MapDelete("/api/memberships/{id:int}", async (int id, GymDbContext db) =>
{
    var x = await db.Memberships.FindAsync(id);
    if (x is null) return Results.NotFound();
    db.Memberships.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* PAYMENTS */
app.MapGet("/api/payments", async (GymDbContext db) => Results.Ok(await db.Payments.Include(x => x.Member).OrderByDescending(x => x.PaymentDate).ToListAsync()));
app.MapPost("/api/payments", async (PaymentCreateDto dto, GymDbContext db) =>
{
    var member = await db.Members.FindAsync(dto.MemberId);
    if (member is null) return Results.BadRequest(new { message = "Member not found." });

    var payment = new Payment
    {
        MemberId = dto.MemberId,
        Amount = dto.Amount,
        PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod,
        PaymentDate = dto.PaymentDate ?? DateTime.UtcNow,
        Notes = dto.Notes ?? "",
        InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{dto.MemberId:D3}"
    };

    db.Payments.Add(payment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/payments/{payment.Id}", payment);
});
app.MapDelete("/api/payments/{id:int}", async (int id, GymDbContext db) =>
{
    var x = await db.Payments.FindAsync(id);
    if (x is null) return Results.NotFound();
    db.Payments.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* CLASSES */
app.MapGet("/api/classes", async (GymDbContext db) => Results.Ok(await db.GymClasses.Include(x => x.Trainer).OrderBy(x => x.ScheduleDate).ToListAsync()));
app.MapPost("/api/classes", async (GymClass x, GymDbContext db) =>
{
    if (x.TrainerId.HasValue && x.TrainerId.Value > 0 && !await db.Trainers.AnyAsync(t => t.Id == x.TrainerId.Value))
    {
        return Results.BadRequest();
    }

    x.SessionType = string.IsNullOrWhiteSpace(x.SessionType) ? "Morning" : x.SessionType;
    x.StartTime = string.IsNullOrWhiteSpace(x.StartTime) ? "08:00 AM" : x.StartTime;
    x.EndTime = string.IsNullOrWhiteSpace(x.EndTime) ? "10:00 AM" : x.EndTime;
    x.TrainerId = x.TrainerId is > 0 ? x.TrainerId : null;
    x.Status = !x.TrainerId.HasValue || string.IsNullOrWhiteSpace(x.Status) ? "Pending" : x.Status;

    db.GymClasses.Add(x);
    await db.SaveChangesAsync();
    return Results.Created($"/api/classes/{x.Id}", x);
});
app.MapPut("/api/classes/{id:int}", async (int id, GymClass input, GymDbContext db) =>
{
    var x = await db.GymClasses.FindAsync(id);
    if (x is null) return Results.NotFound();

    x.Name = input.Name;
    x.Description = input.Description;
    x.TrainerId = input.TrainerId is > 0 ? input.TrainerId : null;
    if (x.TrainerId.HasValue && x.TrainerId.Value > 0 && !await db.Trainers.AnyAsync(t => t.Id == x.TrainerId.Value))
    {
        return Results.BadRequest();
    }

    x.ScheduleDate = input.ScheduleDate;
    x.DurationMinutes = input.DurationMinutes;
    x.Capacity = input.Capacity;
    x.SessionType = string.IsNullOrWhiteSpace(input.SessionType) ? x.SessionType : input.SessionType;
    x.StartTime = string.IsNullOrWhiteSpace(input.StartTime) ? x.StartTime : input.StartTime;
    x.EndTime = string.IsNullOrWhiteSpace(input.EndTime) ? x.EndTime : input.EndTime;
    x.Status = !x.TrainerId.HasValue ? "Pending" : (string.IsNullOrWhiteSpace(input.Status) ? x.Status : input.Status);
    await db.SaveChangesAsync();
    return Results.Ok(x);
});
app.MapDelete("/api/classes/{id:int}", async (int id, GymDbContext db) =>
{
    var x = await db.GymClasses.FindAsync(id);
    if (x is null) return Results.NotFound();
    db.GymClasses.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* ENROLLMENT */
app.MapGet("/api/enrollments", async (GymDbContext db) => Results.Ok(await db.Enrollments.Include(x => x.Member).Include(x => x.GymClass).OrderByDescending(x => x.Id).ToListAsync()));
app.MapPost("/api/enrollments", async (Enrollment x, GymDbContext db) =>
{
    var c = await db.GymClasses.FindAsync(x.GymClassId);
    if (c is null || !await db.Members.AnyAsync(m => m.Id == x.MemberId)) return Results.BadRequest();
    if (await db.Enrollments.AnyAsync(e => e.MemberId == x.MemberId && e.GymClassId == x.GymClassId)) return Results.Conflict();
    if (await db.Enrollments.CountAsync(e => e.GymClassId == x.GymClassId) >= c.Capacity) return Results.BadRequest(new { message = "Class is full." });
    x.EnrolledAt = DateTime.UtcNow;
    db.Enrollments.Add(x);
    await db.SaveChangesAsync();
    return Results.Created($"/api/enrollments/{x.Id}", x);
});

/* EQUIPMENT */
app.MapGet("/api/equipment", async (GymDbContext db) => Results.Ok(await db.Equipment.OrderBy(x => x.Name).ToListAsync()));
app.MapPost("/api/equipment", async (Equipment x, GymDbContext db) =>
{
    x.Name = string.IsNullOrWhiteSpace(x.Name) ? "" : x.Name.Trim();
    x.Quantity = x.Quantity <= 0 ? 1 : x.Quantity;
    x.Status = string.IsNullOrWhiteSpace(x.Status) ? "Active" : x.Status.Trim();
    x.LastUpdated = DateTime.UtcNow;
    db.Equipment.Add(x);
    await db.SaveChangesAsync();
    return Results.Created($"/api/equipment/{x.Id}", x);
});
app.MapPut("/api/equipment/{id:int}", async (int id, Equipment input, GymDbContext db) =>
{
    var x = await db.Equipment.FindAsync(id);
    if (x is null) return Results.NotFound();
    x.Name = string.IsNullOrWhiteSpace(input.Name) ? x.Name : input.Name.Trim();
    x.Quantity = input.Quantity <= 0 ? x.Quantity : input.Quantity;
    x.Status = string.IsNullOrWhiteSpace(input.Status) ? x.Status : input.Status.Trim();
    x.LastUpdated = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(x);
});
app.MapDelete("/api/equipment/{id:int}", async (int id, GymDbContext db) =>
{
    var x = await db.Equipment.FindAsync(id);
    if (x is null) return Results.NotFound();
    db.Equipment.Remove(x);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

/* REPORTS */
app.MapGet("/api/reports/summary", async (GymDbContext db) =>
{
    var now = DateTime.UtcNow;
    var today = now.Date;
    var start = new DateTime(now.Year, now.Month, 1);
    var members = await db.Members.ToListAsync();

    var activeCount = members.Count(x => GetMembershipStatus(today, GetEffectiveExpiry(x)) != "Expired");
    var expiredCount = members.Count(x => GetMembershipStatus(today, GetEffectiveExpiry(x)) == "Expired");
    var expiringSoonCount = members.Count(x => GetMembershipStatus(today, GetEffectiveExpiry(x)) == "Expiring Soon");

    return Results.Ok(new
    {
        members = members.Count,
        activeMemberships = activeCount,
        expiredMemberships = expiredCount,
        expiringSoon = expiringSoonCount,
        monthlyIncome = await db.Payments.Where(x => x.PaymentDate >= start).SumAsync(x => (decimal?)x.Amount) ?? 0,
        totalIncome = await db.Payments.SumAsync(x => (decimal?)x.Amount) ?? 0,
        trainers = await db.Trainers.CountAsync(),
        equipmentCount = await db.Equipment.SumAsync(x => (int?)x.Quantity) ?? 0
    });
});
app.MapGet("/api/reports/monthly-payments", async (GymDbContext db) =>
    Results.Ok(await db.Payments.GroupBy(x => new { x.PaymentDate.Year, x.PaymentDate.Month })
    .Select(g => new { year = g.Key.Year, month = g.Key.Month, total = g.Sum(x => x.Amount) })
    .OrderBy(x => x.year).ThenBy(x => x.month).ToListAsync()));

/* AUTH */
app.MapPost("/api/auth/login", async (LoginRequest r, GymDbContext db) =>
{
    var u = await db.Users.FirstOrDefaultAsync(x => x.Email == r.Email && x.Password == r.Password);
    return u is null ? Results.Unauthorized() : Results.Ok(new { u.Id, u.FullName, u.Email, u.Role });
});
app.MapPost("/api/auth/register", async (RegisterRequest r, GymDbContext db) =>
{
    if (await db.Users.AnyAsync(x => x.Email == r.Email)) return Results.Conflict(new { message = "Email already exists." });
    var u = new User { FullName = r.FullName, Email = r.Email, Password = r.Password, Role = string.IsNullOrWhiteSpace(r.Role) ? "Admin" : r.Role };
    db.Users.Add(u);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{u.Id}", new { u.Id, u.FullName, u.Email, u.Role });
});

app.MapRazorPages();
app.Run();

/* SEED METHOD */
static async Task SeedDataAsync(GymDbContext db)
{
    // 1. Seed Admin User
    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            FullName = "Comfort Gym Admin",
            Email = "admin@comfortgym.com",
            Password = "admin",
            Role = "Admin"
        });
    }

    // 2. Seed Membership Plans
    if (!await db.MembershipPlans.AnyAsync())
    {
        db.MembershipPlans.AddRange(
            new MembershipPlan { Name = "Monthly Standard (Bille)", DurationDays = 30, Price = 30.00m, Description = "Full gym access for 1 month (30 days)" },
            new MembershipPlan { Name = "Monthly VIP + Trainer", DurationDays = 30, Price = 50.00m, Description = "Gym access + personal trainer guidance for 1 month" },
            new MembershipPlan { Name = "Quarterly (3 Months)", DurationDays = 90, Price = 80.00m, Description = "Full gym access for 3 months (save $10)" },
            new MembershipPlan { Name = "Annual Membership (1 Year)", DurationDays = 365, Price = 280.00m, Description = "Unlimited gym access for a full year" }
        );
        await db.SaveChangesAsync();
    }

    // 3. Seed Trainers
    if (!await db.Trainers.AnyAsync())
    {
        db.Trainers.AddRange(
            new Trainer { FullName = "Ahmed Hassan", Email = "ahmed.trainer@comfortgym.com", Phone = "+252 61 5112233", Specialization = "Bodybuilding & Muscle Gain" },
            new Trainer { FullName = "Fatima Omar", Email = "fatima@comfortgym.com", Phone = "+252 61 7223344", Specialization = "Aerobics & Weight Loss" },
            new Trainer { FullName = "Abdirahman Ali", Email = "abdirahman@comfortgym.com", Phone = "+252 61 9334455", Specialization = "Cardio & Crossfit" }
        );
        await db.SaveChangesAsync();
    }

    // 4. Seed Members with varied dates if empty
    if (!await db.Members.AnyAsync())
    {
        var plan = await db.MembershipPlans.FirstOrDefaultAsync();
        int planId = plan?.Id ?? 1;
        var today = DateTime.UtcNow.Date;
        var adminUser = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync();

        var m1 = new Member
        {
            FullName = "Khadar Mohamed Warsame",
            Email = "khadar@example.com",
            Phone = "+252 61 8901234",
            Gender = "Male",
            Address = "Mogadishu, KM4",
            RegistrationDate = today,
            ExpiryDate = today.AddDays(30), // Active 1 month
            MembershipPlanId = planId,
            Status = "Active",
            EmergencyContact = "+252 61 5001122",
            CreatedByUserId = adminUser?.Id,
            CreatedByName = adminUser?.FullName ?? "Comfort Gym Admin"
        };
        var m2 = new Member
        {
            FullName = "Zahra Abdullahi Noor",
            Email = "zahra@example.com",
            Phone = "+252 61 6789012",
            Gender = "Female",
            Address = "Hodan District",
            RegistrationDate = today.AddDays(-26),
            ExpiryDate = today.AddDays(4), // Expiring Soon (4 days left)
            MembershipPlanId = planId,
            Status = "Active",
            EmergencyContact = "+252 61 5223344",
            CreatedByUserId = adminUser?.Id,
            CreatedByName = adminUser?.FullName ?? "Comfort Gym Admin"
        };
        var m3 = new Member
        {
            FullName = "Hassan Yasin Barre",
            Email = "hassan.yasin@example.com",
            Phone = "+252 61 4567890",
            Gender = "Male",
            Address = "Waberi District",
            RegistrationDate = today.AddDays(-45),
            ExpiryDate = today.AddDays(-15), // Expired 15 days ago
            MembershipPlanId = planId,
            Status = "Expired",
            EmergencyContact = "+252 61 5334455",
            CreatedByUserId = adminUser?.Id,
            CreatedByName = adminUser?.FullName ?? "Comfort Gym Admin"
        };
        var m4 = new Member
        {
            FullName = "Muna Shire Farah",
            Email = "muna.shire@example.com",
            Phone = "+252 61 3456789",
            Gender = "Female",
            Address = "Hamar Weyne",
            RegistrationDate = today.AddDays(-10),
            ExpiryDate = today.AddDays(20), // Active (20 days left)
            MembershipPlanId = planId,
            Status = "Active",
            EmergencyContact = "+252 61 5445566",
            CreatedByUserId = adminUser?.Id,
            CreatedByName = adminUser?.FullName ?? "Comfort Gym Admin"
        };

        db.Members.AddRange(m1, m2, m3, m4);
        await db.SaveChangesAsync();

        // Seed Payments
        db.Payments.AddRange(
            new Payment { MemberId = m1.Id, Amount = 30.00m, PaymentMethod = "EVC Plus", PaymentDate = today, InvoiceNumber = $"INV-{today:yyyyMMdd}-{m1.Id:D3}", Notes = "Monthly Plan Registration" },
            new Payment { MemberId = m2.Id, Amount = 30.00m, PaymentMethod = "Zaad", PaymentDate = today.AddDays(-26), InvoiceNumber = $"INV-{today.AddDays(-26):yyyyMMdd}-{m2.Id:D3}", Notes = "Monthly Plan Registration" },
            new Payment { MemberId = m4.Id, Amount = 30.00m, PaymentMethod = "Cash", PaymentDate = today.AddDays(-10), InvoiceNumber = $"INV-{today.AddDays(-10):yyyyMMdd}-{m4.Id:D3}", Notes = "Monthly Plan Registration" }
        );

    }

    // 5. Seed Equipment if empty
    if (!await db.Equipment.AnyAsync())
    {
        db.Equipment.AddRange(
            new Equipment { Name = "Commercial Treadmill X9", Quantity = 6, Status = "Active" },
            new Equipment { Name = "Olympic Barbell & Weights Set", Quantity = 8, Status = "Active" },
            new Equipment { Name = "Dumbbell Rack (2.5kg - 50kg)", Quantity = 2, Status = "Active" },
            new Equipment { Name = "Cable Crossover Machine", Quantity = 2, Status = "Active" },
            new Equipment { Name = "Stationary Spin Bikes", Quantity = 5, Status = "Active" },
            new Equipment { Name = "Incline & Flat Benches", Quantity = 6, Status = "Active" }
        );
    }

    // 6. Seed Classes if empty
    if (!await db.GymClasses.AnyAsync())
    {
        var trainer = await db.Trainers.FirstOrDefaultAsync();
        if (trainer != null)
        {
            db.GymClasses.AddRange(
                new GymClass { Name = "Morning Aerobics & HIIT", Description = "High intensity cardio and bodyweight burn", TrainerId = trainer.Id, ScheduleDate = DateTime.UtcNow.Date.AddHours(7), DurationMinutes = 60, Capacity = 20 },
                new GymClass { Name = "Evening Strength & Power", Description = "Heavy lifting, form training and core strength", TrainerId = trainer.Id, ScheduleDate = DateTime.UtcNow.Date.AddHours(18), DurationMinutes = 60, Capacity = 15 }
            );
        }
    }

    await db.SaveChangesAsync();
}

/* DTOs */
public record MemberCreateDto(
    [Required] string FullName,
    string? Email,
    string? Phone,
    string? Gender,
    string? Address,
    DateTime? DateOfBirth,
    DateTime? RegistrationDate,
    DateTime? ExpiryDate,
    int? MembershipPlanId,
    decimal? AmountPaid,
    string? PaymentMethod,
    string? EmergencyContact,
    string? Notes,
    int? CreatedByUserId,
    string? CreatedByName
);

public record MemberUpdateDto(
    [Required] string FullName,
    string? Email,
    string? Phone,
    string? Gender,
    string? Address,
    DateTime? RegistrationDate,
    DateTime? ExpiryDate,
    string? EmergencyContact,
    string? Notes,
    int? RequestedByUserId
);

public record RenewDto(
    int? DurationDays,
    decimal? Amount,
    string? PaymentMethod,
    string? Notes
);

public record PaymentCreateDto(
    int MemberId,
    decimal Amount,
    string? PaymentMethod,
    DateTime? PaymentDate,
    string? Notes
);

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FullName, string Email, string Password, string? Role);

/* EF CORE DB CONTEXT */
public class GymDbContext : DbContext
{
    public GymDbContext(DbContextOptions<GymDbContext> o) : base(o) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<GymClass> GymClasses => Set<GymClass>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<User>().HasIndex(x => x.Email).IsUnique();
        m.Entity<Member>().HasIndex(x => x.Email).IsUnique(false);
        m.Entity<Member>().HasOne(x => x.MembershipPlan).WithMany().HasForeignKey(x => x.MembershipPlanId).OnDelete(DeleteBehavior.SetNull);
        m.Entity<Membership>().HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Membership>().HasOne(x => x.MembershipPlan).WithMany().HasForeignKey(x => x.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Payment>().HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<GymClass>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Enrollment>().HasIndex(x => new { x.MemberId, x.GymClassId }).IsUnique();
        m.Entity<Enrollment>().HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Enrollment>().HasOne(x => x.GymClass).WithMany().HasForeignKey(x => x.GymClassId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class User
{
    public int Id { get; set; }
    [Required] public string FullName { get; set; } = "";
    [Required] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string Role { get; set; } = "Admin";
}

public class Member
{
    public int Id { get; set; }
    [Required] public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string Phone { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Address { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public DateTime? RegistrationDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ExpiryDate { get; set; }
    public int? MembershipPlanId { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }
    public string Status { get; set; } = "Active";
    public string? EmergencyContact { get; set; } = "";
    public string? Notes { get; set; } = "";
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Trainer
{
    public int Id { get; set; }
    [Required] public string? FullName { get; set; } = "";
    public string? Email { get; set; } = "";
    public string? Phone { get; set; } = "";
    public string? Specialization { get; set; } = "General Fitness";
}

public class MembershipPlan
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public int DurationDays { get; set; } = 30;
    public decimal Price { get; set; } = 30;
    public string Description { get; set; } = "";
}

public class Membership
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public int MembershipPlanId { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Active";
}

public class Payment
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = "";
    public string? InvoiceNumber { get; set; } = "";
}

public class GymClass
{
    public int Id { get; set; }
    [Required] public string? Name { get; set; } = "";
    public string? Description { get; set; } = "";
    public int? TrainerId { get; set; }
    public Trainer? Trainer { get; set; }
    public DateTime ScheduleDate { get; set; } = DateTime.UtcNow;
    public string? SessionType { get; set; } = "Morning";
    public string? StartTime { get; set; } = "08:00 AM";
    public string? EndTime { get; set; } = "10:00 AM";
    public string? Status { get; set; } = "Pending";
    public int DurationMinutes { get; set; } = 60;
    public int Capacity { get; set; } = 25;
}

public class Enrollment
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public Member? Member { get; set; }
    public int GymClassId { get; set; }
    public GymClass? GymClass { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}


public class Equipment
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public string Status { get; set; } = "Active";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
