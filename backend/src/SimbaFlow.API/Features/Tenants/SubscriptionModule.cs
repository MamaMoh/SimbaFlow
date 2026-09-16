using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Billing;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Tenants;

// Enums arrive as their names — "Monthly", "Suspended" — because that is what the interface
// shows and sends. The API has no string-enum converter registered, and adding one globally would
// change every existing response, so these are parsed here instead.
public record UpdateSubscriptionBody(
    string? Status,
    string? Cycle,
    decimal? Amount,
    string? Currency,
    DateOnly? NextPaymentDue);

public record GenerateInvoiceBody(
    DateOnly? PeriodStart,
    string? Cycle,
    decimal? Amount,
    int? DueInDays);

public record MarkPaidBody(DateOnly? PaidOn, string? PaymentReference);

/// <summary>
/// Subscription status and invoicing for agencies.
///
/// Platform-side: only SuperAdmin can suspend an agency or raise a bill. The one exception is the
/// agency's own view of where it stands, which has to keep working while they are suspended —
/// otherwise the people who need to pay us cannot see that that is what is wrong.
/// </summary>
public class SubscriptionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/subscriptions")
            .WithTags("Subscriptions")
            .RequireAuthorization("SuperAdmin");

        // ──── Where every agency stands ────

        admin.MapGet("/", async (IPlatformDbContext db, CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var tenants = await db.Tenants.AsNoTracking()
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    Status = t.SubscriptionStatus,
                    t.BillingCycle,
                    t.SubscriptionAmount,
                    t.SubscriptionCurrency,
                    t.NextPaymentDue,
                    Outstanding = db.SubscriptionInvoices
                        .Count(i => i.TenantId == t.Id && !i.IsDeleted
                                    && i.Status == InvoiceStatus.Issued),
                })
                .ToListAsync(ct);

            var data = tenants.Select(t => new
            {
                t.Id,
                t.Name,
                Status = t.Status.ToString(),
                HasAccess = SubscriptionRules.HasAccess(t.Status),
                Cycle = t.BillingCycle.ToString(),
                t.SubscriptionAmount,
                t.SubscriptionCurrency,
                t.NextPaymentDue,
                DaysUntilDue = SubscriptionRules.DaysUntilDue(t.NextPaymentDue, today),
                Notice = SubscriptionRules.NoticeFor(t.NextPaymentDue, t.BillingCycle, today).ToString(),
                t.Outstanding,
            });

            return Results.Ok(new { isSuccess = true, data });
        });

        admin.MapPut("/{tenantId:guid}", async (
            Guid tenantId,
            UpdateSubscriptionBody body,
            IPlatformDbContext db,
            ITenantSchemaResolver schemas,
            CancellationToken ct) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            if (!string.IsNullOrWhiteSpace(body.Status))
            {
                if (!Enum.TryParse<TenantStatus>(body.Status, ignoreCase: true, out var status))
                    return Results.Json(
                        new { isSuccess = false, error = $"'{body.Status}' is not a status this system has." },
                        statusCode: 400);
                tenant.SubscriptionStatus = status;
            }

            if (!string.IsNullOrWhiteSpace(body.Cycle))
            {
                if (!Enum.TryParse<BillingCycle>(body.Cycle, ignoreCase: true, out var cycle))
                    return Results.Json(
                        new { isSuccess = false, error = "Billing is either Monthly or Yearly." },
                        statusCode: 400);
                tenant.BillingCycle = cycle;
            }
            if (body.Amount is decimal amount)
            {
                if (amount < 0)
                    return Results.Json(
                        new { isSuccess = false, error = "A subscription cannot cost less than nothing." },
                        statusCode: 400);
                tenant.SubscriptionAmount = amount;
            }
            if (!string.IsNullOrWhiteSpace(body.Currency))
                tenant.SubscriptionCurrency = body.Currency.Trim().ToUpperInvariant();
            if (body.NextPaymentDue is DateOnly due) tenant.NextPaymentDue = due;

            await db.SaveChangesAsync(ct);

            // The schema lookup is what enforces access, and it caches its answer. Without this,
            // suspending an agency does nothing until that entry expires — they carry on working,
            // and reactivating would leave them locked out just as long.
            schemas.InvalidateCache(tenantId);

            return Results.Ok(new { isSuccess = true });
        });

        // ──── Invoices ────

        admin.MapGet("/{tenantId:guid}/invoices", async (
            Guid tenantId, IPlatformDbContext db, CancellationToken ct) =>
        {
            var invoices = await db.SubscriptionInvoices.AsNoTracking()
                .Where(i => i.TenantId == tenantId && !i.IsDeleted)
                .OrderByDescending(i => i.PeriodStart)
                .Select(i => new
                {
                    i.Id, i.Number, i.PeriodStart, i.PeriodEnd, i.IssuedOn, i.DueOn,
                    i.Amount, i.Currency, Status = i.Status.ToString(),
                    Cycle = i.Cycle.ToString(), i.PaidOn, i.PaymentReference,
                })
                .ToListAsync(ct);

            return Results.Ok(new { isSuccess = true, data = invoices });
        });

        admin.MapPost("/{tenantId:guid}/invoices", async (
            Guid tenantId, GenerateInvoiceBody body, IPlatformDbContext db, CancellationToken ct) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var cycle = tenant.BillingCycle;
            if (!string.IsNullOrWhiteSpace(body.Cycle))
            {
                if (!Enum.TryParse<BillingCycle>(body.Cycle, ignoreCase: true, out cycle))
                    return Results.Json(
                        new { isSuccess = false, error = "A period is either Monthly or Yearly." },
                        statusCode: 400);
            }
            var start = body.PeriodStart ?? tenant.NextPaymentDue ?? today;
            var amount = body.Amount ?? tenant.SubscriptionAmount;

            if (amount <= 0)
                return Results.Json(
                    new { isSuccess = false, error = "Set the subscription amount before raising an invoice." },
                    statusCode: 400);

            var (periodStart, periodEnd) = SubscriptionRules.PeriodFor(start, cycle);

            // Raising the same period twice is nearly always a slip, and a duplicate bill is
            // worse than a refused one.
            var existing = await db.SubscriptionInvoices.AsNoTracking().AnyAsync(
                i => i.TenantId == tenantId && !i.IsDeleted
                     && i.Status != InvoiceStatus.Void && i.PeriodStart == periodStart, ct);
            if (existing)
                return Results.Json(
                    new { isSuccess = false, error = $"An invoice already covers the period from {periodStart:dd MMM yyyy}." },
                    statusCode: 409);

            var invoice = new SubscriptionInvoice
            {
                TenantId = tenantId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Cycle = cycle,
                IssuedOn = today,
                DueOn = today.AddDays(body.DueInDays ?? 14),
                Amount = amount,
                Currency = tenant.SubscriptionCurrency,
                Status = InvoiceStatus.Issued,
            };

            // Reading the numbers and inserting are two steps, so two invoices raised at the same
            // moment can pick the same one — and Number is uniquely indexed, so the second insert
            // throws. Rather than surface that as a bare 500, look again and retry: the second
            // attempt reads the number the first one just committed.
            //
            // Soft-deleted and void rows are included deliberately. A number that has been used is
            // used, whatever later happened to the invoice that used it.
            const int attempts = 3;
            for (var attempt = 1; ; attempt++)
            {
                var usedThisYear = await db.SubscriptionInvoices
                    .AsNoTracking()
                    .Where(i => i.IssuedOn.Year == today.Year)
                    .Select(i => i.Number)
                    .ToListAsync(ct);

                invoice.Number = SubscriptionRules.NextInvoiceNumber(today.Year, usedThisYear);

                try
                {
                    if (attempt == 1) db.SubscriptionInvoices.Add(invoice);
                    await db.SaveChangesAsync(ct);
                    break;
                }
                catch (DbUpdateException) when (attempt < attempts)
                {
                    // Someone else took the number between the read and the write. Go round again.
                }
            }

            return Results.Ok(new { isSuccess = true, data = new { invoice.Id, invoice.Number } });
        });

        admin.MapPost("/invoices/{invoiceId:guid}/paid", async (
            Guid invoiceId, MarkPaidBody body, IPlatformDbContext db, CancellationToken ct) =>
        {
            var invoice = await db.SubscriptionInvoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, ct);
            if (invoice is null)
                return Results.Json(new { isSuccess = false, error = "Invoice not found." }, statusCode: 404);
            if (invoice.Status == InvoiceStatus.Paid)
                return Results.Ok(new { isSuccess = true });

            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidOn = body.PaidOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
            invoice.PaymentReference = body.PaymentReference?.Trim();

            // Settling the current period moves the agency's next payment on by one cycle, so
            // recording a payment and rescheduling are not two things to remember.
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == invoice.TenantId && !t.IsDeleted, ct);
            // "is null ||" matters: NextPaymentDue is a DateOnly?, and a comparison against null is
            // false rather than true. A newly provisioned agency has no due date yet, so without
            // this it pays its first invoice and still has none — the renewal notice stays None for
            // ever and nobody is reminded again.
            if (tenant is not null
                && (tenant.NextPaymentDue is null || tenant.NextPaymentDue <= invoice.PeriodEnd))
            {
                tenant.NextPaymentDue = invoice.PeriodEnd.AddDays(1);
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { isSuccess = true });
        });

        admin.MapPost("/invoices/{invoiceId:guid}/void", async (
            Guid invoiceId, IPlatformDbContext db, CancellationToken ct) =>
        {
            var invoice = await db.SubscriptionInvoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted, ct);
            if (invoice is null)
                return Results.Json(new { isSuccess = false, error = "Invoice not found." }, statusCode: 404);
            if (invoice.Status == InvoiceStatus.Paid)
                return Results.Json(
                    new { isSuccess = false, error = "This invoice has been paid. Refund it rather than voiding it." },
                    statusCode: 400);

            invoice.Status = InvoiceStatus.Void;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { isSuccess = true });
        });

        // ──── The agency's own view ────
        //
        // Reads only the platform schema, so it keeps answering while the agency is suspended.
        // That matters: this is the one screen that can tell them why nothing else works.
        app.MapGet("/api/subscription/mine", async (
            IPlatformDbContext db, ICurrentUserService user, CancellationToken ct) =>
        {
            if (user.TenantId is not Guid tenantId)
                return Results.Ok(new { isSuccess = true, data = (object?)null });

            var tenant = await db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => new
                {
                    t.Name, t.SubscriptionStatus, t.BillingCycle,
                    t.SubscriptionAmount, t.SubscriptionCurrency, t.NextPaymentDue,
                })
                .FirstOrDefaultAsync(ct);

            if (tenant is null)
                return Results.Ok(new { isSuccess = true, data = (object?)null });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var outstanding = await db.SubscriptionInvoices.AsNoTracking()
                .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.Status == InvoiceStatus.Issued)
                .OrderBy(i => i.DueOn)
                .Select(i => new { i.Number, i.DueOn, i.Amount, i.Currency, i.PeriodStart, i.PeriodEnd })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    agency = tenant.Name,
                    status = tenant.SubscriptionStatus.ToString(),
                    hasAccess = SubscriptionRules.HasAccess(tenant.SubscriptionStatus),
                    cycle = tenant.BillingCycle.ToString(),
                    amount = tenant.SubscriptionAmount,
                    currency = tenant.SubscriptionCurrency,
                    nextPaymentDue = tenant.NextPaymentDue,
                    daysUntilDue = SubscriptionRules.DaysUntilDue(tenant.NextPaymentDue, today),
                    notice = SubscriptionRules
                        .NoticeFor(tenant.NextPaymentDue, tenant.BillingCycle, today).ToString(),
                    outstanding,
                },
            });
        }).RequireAuthorization();
    }
}
