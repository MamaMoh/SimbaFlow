using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Partners;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Partners;

public class PartnerModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/partners")
            .WithTags("Partners")
            .RequireAuthorization();

        // ──── Catalog (platform) ────
        group.MapGet("/", async (
            IPlatformDbContext context,
            ICurrentUserService user,
            string? country,
            bool? activeOnly,
            bool? linkedOnly,
            bool? usableOnly) =>
        {
            if (linkedOnly == true)
            {
                if (user.TenantId is not Guid tenantId)
                    return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

                // Deliberately NOT filtered to Active-only: the Partners page must surface expired
                // and suspended agreements so the agency can renew them. Intake filtering happens
                // via /links/mine?usableOnly=true instead.
                var linkRows = await (
                    from link in context.PartnerLinks.AsNoTracking()
                    join p in context.PartnerAgencies.AsNoTracking() on link.PartnerAgencyId equals p.Id
                    where link.TenantId == tenantId && !link.IsDeleted && !p.IsDeleted
                    orderby p.CountryName, p.Name
                    select new
                    {
                        p.Id,
                        p.Name,
                        Country = p.CountryName,
                        p.CountryCode,
                        p.ContactEmail,
                        p.ContactPhone,
                        p.Address,
                        p.ForeignLicenseId,
                        LinkId = link.Id,
                        link.AgreementStart,
                        link.AgreementEnd,
                        LinkStatus = link.Status,
                    }).ToListAsync();

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var linked = linkRows.Select(r =>
                {
                    var state = PartnerAgreementRules.Evaluate(
                        r.AgreementStart, r.AgreementEnd, r.LinkStatus, today);
                    var days = PartnerAgreementRules.DaysRemaining(r.AgreementEnd, today);
                    return new
                    {
                        r.Id,
                        r.Name,
                        r.Country,
                        r.CountryCode,
                        r.ContactEmail,
                        r.ContactPhone,
                        r.Address,
                        Status = r.LinkStatus.ToString(),
                        r.ForeignLicenseId,
                        r.LinkId,
                        AgreementStart = r.AgreementStart.ToString("yyyy-MM-dd"),
                        AgreementEnd = r.AgreementEnd.ToString("yyyy-MM-dd"),
                        AgreementState = state.ToString(),
                        DaysRemaining = days,
                        AgreementLabel = PartnerAgreementRules.Describe(state, days),
                        IsUsable = PartnerAgreementRules.IsUsableForIntake(state),
                    };
                })
                // Intake passes usableOnly=true so lapsed/suspended agreements never appear
                // in the candidate partner dropdown.
                .Where(l => usableOnly != true || l.IsUsable)
                .ToList();

                return Results.Ok(new { isSuccess = true, data = linked });
            }

            var query = context.PartnerAgencies.AsNoTracking().Where(p => !p.IsDeleted);
            if (activeOnly != false)
                query = query.Where(p => p.IsActive);
            if (!string.IsNullOrWhiteSpace(country))
            {
                var c = country.Trim();
                query = query.Where(p =>
                    p.CountryCode == c || p.CountryName.ToLower().Contains(c.ToLower()));
            }

            var partners = await query
                .OrderBy(p => p.CountryName).ThenBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    Country = p.CountryName,
                    p.CountryCode,
                    p.ContactEmail,
                    p.ContactPhone,
                    p.Address,
                    Status = p.IsActive ? "Active" : "Inactive",
                    p.ForeignLicenseId
                })
                .ToListAsync();

            return Results.Ok(new { isSuccess = true, data = partners });
        });

        group.MapGet("/{id:guid}", async (Guid id, IPlatformDbContext context) =>
        {
            var p = await context.PartnerAgencies.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (p is null)
                return Results.Json(new { isSuccess = false, error = "Partner not found" }, statusCode: 404);

            var linked = await context.PartnerLinks.CountAsync(l =>
                l.PartnerAgencyId == id && !l.IsDeleted && l.Status == PartnerLinkStatus.Active);

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    p.Id,
                    p.Name,
                    Country = p.CountryName,
                    p.CountryCode,
                    p.ContactEmail,
                    p.ContactPhone,
                    p.Address,
                    p.ForeignLicenseId,
                    ActiveLinks = linked,
                    Status = p.IsActive ? "Active" : "Inactive",
                    p.Notes
                }
            });
        });

        group.MapPost("/", async (
            CreatePartnerRequest body,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.create") && !user.HasPermission("system.admin"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            if (string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.CountryCode))
                return Results.Json(new { isSuccess = false, error = "Name and country code are required" }, statusCode: 400);

            var partner = new PartnerAgency
            {
                Name = body.Name.Trim(),
                CountryCode = body.CountryCode.Trim().ToUpperInvariant(),
                CountryName = string.IsNullOrWhiteSpace(body.CountryName) ? body.CountryCode.Trim() : body.CountryName.Trim(),
                ForeignLicenseId = body.ForeignLicenseId?.Trim(),
                ContactEmail = body.ContactEmail?.Trim(),
                ContactPhone = body.ContactPhone?.Trim(),
                Address = body.Address?.Trim(),
                Notes = body.Notes?.Trim(),
                IsActive = true,
            };

            context.PartnerAgencies.Add(partner);
            await context.SaveChangesAsync();
            return Results.Created($"/api/partners/{partner.Id}", new { isSuccess = true, data = partner.Id });
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdatePartnerRequest body,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.update") && !user.HasPermission("system.admin"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var partner = await context.PartnerAgencies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (partner is null)
                return Results.Json(new { isSuccess = false, error = "Partner not found" }, statusCode: 404);

            if (!string.IsNullOrWhiteSpace(body.Name)) partner.Name = body.Name.Trim();
            if (!string.IsNullOrWhiteSpace(body.CountryCode)) partner.CountryCode = body.CountryCode.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(body.CountryName)) partner.CountryName = body.CountryName.Trim();
            if (body.ForeignLicenseId is not null) partner.ForeignLicenseId = body.ForeignLicenseId.Trim();
            if (body.ContactEmail is not null) partner.ContactEmail = body.ContactEmail.Trim();
            if (body.ContactPhone is not null) partner.ContactPhone = body.ContactPhone.Trim();
            if (body.Address is not null) partner.Address = body.Address.Trim();
            if (body.Notes is not null) partner.Notes = body.Notes.Trim();
            if (body.IsActive is bool active) partner.IsActive = active;

            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true });
        });

        // ──── Tenant links (ትስስር) ────
        // This tenant's partner links. `usableOnly=true` (what intake uses) drops agreements that
        // have lapsed, are suspended, or have not started — an agency may not place candidates
        // through those, so they must never reach the intake dropdown.
        group.MapGet("/links/mine", async (
            IPlatformDbContext context,
            ICurrentUserService user,
            bool? usableOnly) =>
        {
            if (user.TenantId is not Guid tenantId && !user.IsSuperAdmin)
                return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

            var tid = user.TenantId ?? Guid.Empty;
            if (user.IsSuperAdmin && tid == Guid.Empty)
                return Results.Ok(new { isSuccess = true, data = Array.Empty<object>() });

            var rows = await (
                from link in context.PartnerLinks.AsNoTracking()
                join p in context.PartnerAgencies.AsNoTracking() on link.PartnerAgencyId equals p.Id
                where link.TenantId == tid && !link.IsDeleted && !p.IsDeleted
                orderby p.CountryName, p.Name
                select new
                {
                    link.Id,
                    PartnerAgencyId = p.Id,
                    PartnerName = p.Name,
                    Country = p.CountryName,
                    p.CountryCode,
                    LinkStatus = link.Status,
                    link.AgreementStart,
                    link.AgreementEnd,
                }).ToListAsync();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var links = rows
                .Select(r =>
                {
                    var state = PartnerAgreementRules.Evaluate(
                        r.AgreementStart, r.AgreementEnd, r.LinkStatus, today);
                    var days = PartnerAgreementRules.DaysRemaining(r.AgreementEnd, today);
                    return new
                    {
                        r.Id,
                        r.PartnerAgencyId,
                        r.PartnerName,
                        r.Country,
                        r.CountryCode,
                        Status = r.LinkStatus.ToString(),
                        AgreementStart = r.AgreementStart.ToString("yyyy-MM-dd"),
                        AgreementEnd = r.AgreementEnd.ToString("yyyy-MM-dd"),
                        AgreementState = state.ToString(),
                        DaysRemaining = days,
                        AgreementLabel = PartnerAgreementRules.Describe(state, days),
                        IsUsable = PartnerAgreementRules.IsUsableForIntake(state),
                    };
                })
                .Where(l => usableOnly != true || l.IsUsable)
                .ToList();

            return Results.Ok(new { isSuccess = true, data = links });
        });

        group.MapPost("/links", async (
            CreatePartnerLinkRequest body,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.create") && !user.HasPermission("partner.update") && !user.HasPermission("system.admin"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var tenantId = body.TenantId ?? user.TenantId;
            if (tenantId is null || tenantId == Guid.Empty)
                return Results.Json(new { isSuccess = false, error = "TenantId is required" }, statusCode: 400);

            var partner = await context.PartnerAgencies
                .FirstOrDefaultAsync(p => p.Id == body.PartnerAgencyId && !p.IsDeleted && p.IsActive);
            if (partner is null)
                return Results.Json(new { isSuccess = false, error = "Partner agency not found or inactive" }, statusCode: 404);

            var tenant = await context.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            var exists = await context.PartnerLinks.AnyAsync(l =>
                l.TenantId == tenantId && l.PartnerAgencyId == partner.Id && !l.IsDeleted);
            if (exists)
                return Results.Json(new { isSuccess = false, error = "This partner is already linked to the agency" }, statusCode: 409);

            // Agency level — partners per country + max countries
            var (maxPerCountry, maxCountries) = AgencyLevelRules.GetCaps(tenant.AgencyLevel);
            var sameCountryCount = await (
                from l in context.PartnerLinks
                join p in context.PartnerAgencies on l.PartnerAgencyId equals p.Id
                where l.TenantId == tenantId && !l.IsDeleted && l.Status == PartnerLinkStatus.Active
                      && !p.IsDeleted && p.CountryCode == partner.CountryCode
                select l.Id).CountAsync();
            if (!PartnerLinkRules.LevelHasPerCountryCapacity(sameCountryCount, tenant.AgencyLevel))
                return Results.Json(new
                {
                    isSuccess = false,
                    error = $"Level {tenant.AgencyLevel}: at most {maxPerCountry} partners in {partner.CountryName} (Arts. 18–22)."
                }, statusCode: 400);

            var countryCodes = await (
                from l in context.PartnerLinks
                join p in context.PartnerAgencies on l.PartnerAgencyId equals p.Id
                where l.TenantId == tenantId && !l.IsDeleted && l.Status == PartnerLinkStatus.Active && !p.IsDeleted
                select p.CountryCode).Distinct().ToListAsync();
            if (!PartnerLinkRules.LevelAllowsCountry(countryCodes, partner.CountryCode, tenant.AgencyLevel))
            {
                var countryCap = maxCountries!.Value;
                return Results.Json(new
                {
                    isSuccess = false,
                    error = $"Level {tenant.AgencyLevel}: at most {countryCap} destination countries (Arts. 18–22)."
                }, statusCode: 400);
            }

            // Licensed destination countries (when list is non-empty)
            if (!PartnerLinkRules.IsCountryLicensed(
                    tenant.LicensedCountries, partner.CountryCode, partner.CountryName))
            {
                var licensed = tenant.LicensedCountries ?? [];
                return Results.Json(new
                {
                    isSuccess = false,
                    error =
                        $"Partner country '{partner.CountryName}' ({partner.CountryCode}) is not in this agency's licensed destinations ({string.Join(", ", licensed)})."
                }, statusCode: 400);
            }

            if (!DateOnly.TryParse(body.AgreementStart, out var start))
                start = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!DateOnly.TryParse(body.AgreementEnd, out var end))
                end = start.AddYears(2);
            if (!PartnerLinkRules.AgreementDatesValid(start, end))
                return Results.Json(new { isSuccess = false, error = "Agreement end must be on or after start" }, statusCode: 400);

            var link = new PartnerLink
            {
                TenantId = tenantId.Value,
                PartnerAgencyId = partner.Id,
                AgreementStart = start,
                AgreementEnd = end,
                Status = PartnerLinkStatus.Active,
            };
            context.PartnerLinks.Add(link);
            await context.SaveChangesAsync();

            return Results.Created($"/api/partners/links/{link.Id}", new { isSuccess = true, data = link.Id });
        });

        group.MapPut("/links/{id:guid}/status", async (
            Guid id,
            UpdateLinkStatusRequest body,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.update") && !user.HasPermission("system.admin"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var link = await context.PartnerLinks.FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
            if (link is null)
                return Results.Json(new { isSuccess = false, error = "Link not found" }, statusCode: 404);

            if (!user.IsSuperAdmin && user.TenantId != link.TenantId)
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            if (!Enum.TryParse<PartnerLinkStatus>(body.Status, true, out var status))
                return Results.Json(new { isSuccess = false, error = "Invalid status" }, statusCode: 400);

            link.Status = status;
            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true });
        });

        // ──── Capacity: how many partner slots this agency's level allows, per country ────
        group.MapGet("/capacity", async (IPlatformDbContext context, ICurrentUserService user) =>
        {
            if (user.TenantId is not Guid tid || tid == Guid.Empty)
                return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

            var tenant = await context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Tenant not found" }, statusCode: 404);

            var (maxPerCountry, maxCountries) = AgencyLevelRules.GetCaps(tenant.AgencyLevel);

            var used = await (
                from link in context.PartnerLinks.AsNoTracking()
                join p in context.PartnerAgencies.AsNoTracking() on link.PartnerAgencyId equals p.Id
                where link.TenantId == tid && !link.IsDeleted && !p.IsDeleted
                      && link.Status == PartnerLinkStatus.Active
                group p by p.CountryName into g
                select new { Country = g.Key, Used = g.Count() }
            ).ToListAsync();

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    AgencyLevel = tenant.AgencyLevel,
                    LevelDescription = AgencyLevelRules.Describe(tenant.AgencyLevel),
                    MaxPartnersPerCountry = maxPerCountry,
                    MaxLicensedCountries = maxCountries,
                    LicensedCountries = tenant.LicensedCountries,
                    LicensedCountriesUsed = tenant.LicensedCountries.Count,
                    ByCountry = used
                        .OrderByDescending(u => u.Used)
                        .Select(u => new
                        {
                            u.Country,
                            u.Used,
                            Max = maxPerCountry,
                            Remaining = Math.Max(0, maxPerCountry - u.Used),
                        }),
                }
            });
        });

        // ──── Where did our candidates go: this tenant's candidates for one partner ────
        group.MapGet("/{partnerAgencyId:guid}/candidates", async (
            Guid partnerAgencyId,
            IPlatformDbContext platform,
            ITenantDbContext tenantDb,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.read") && !user.HasPermission("candidate.read"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var partner = await platform.PartnerAgencies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == partnerAgencyId && !p.IsDeleted);
            if (partner is null)
                return Results.Json(new { isSuccess = false, error = "Partner not found" }, statusCode: 404);

            // Tenant-scoped context: only this agency's candidates are visible.
            var candidates = await tenantDb.Candidates.AsNoTracking()
                .Where(c => !c.IsDeleted && c.PartnerAgencyId == partnerAgencyId)
                .OrderByDescending(c => c.RegisteredAt)
                .Select(c => new
                {
                    c.Id,
                    FullName = c.FirstName + " " + c.LastName,
                    c.PassportNumber,
                    Stage = c.CurrentStageName,
                    c.CountryOfTravel,
                    RegisteredAt = c.RegisteredAt,
                    Status = c.Status.ToString(),
                })
                .Take(500)
                .ToListAsync();

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    PartnerName = partner.Name,
                    Country = partner.CountryName,
                    TotalCandidates = candidates.Count,
                    Items = candidates,
                }
            });
        });

        // ──── Billing: commission rollup for one partner ────
        // Derived by joining commissions to candidates on PartnerAgencyId, so the partner is always
        // whatever the candidate record says (no denormalized copy to drift out of sync).
        group.MapGet("/{partnerAgencyId:guid}/billing", async (
            Guid partnerAgencyId,
            IPlatformDbContext platform,
            ITenantDbContext tenantDb,
            ICurrentUserService user) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("commission.read"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

            var partner = await platform.PartnerAgencies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == partnerAgencyId && !p.IsDeleted);
            if (partner is null)
                return Results.Json(new { isSuccess = false, error = "Partner not found" }, statusCode: 404);

            var rows = await (
                from commission in tenantDb.Commissions.AsNoTracking()
                join candidate in tenantDb.Candidates.AsNoTracking()
                    on commission.CandidateId equals candidate.Id
                where !commission.IsDeleted && !candidate.IsDeleted
                      && candidate.PartnerAgencyId == partnerAgencyId
                select new
                {
                    commission.Id,
                    commission.CandidateId,
                    CandidateName = candidate.FirstName + " " + candidate.LastName,
                    candidate.PassportNumber,
                    commission.Status,
                    commission.TotalFeesAmount,
                    commission.TotalPaidAmount,
                    commission.BalanceAmount,
                    commission.OpenedAt,
                }).ToListAsync();

            return Results.Ok(new
            {
                isSuccess = true,
                data = new
                {
                    PartnerName = partner.Name,
                    Country = partner.CountryName,
                    CommissionCount = rows.Count,
                    TotalFees = rows.Sum(r => r.TotalFeesAmount),
                    TotalPaid = rows.Sum(r => r.TotalPaidAmount),
                    Outstanding = rows.Sum(r => r.BalanceAmount),
                    ByStatus = rows
                        .GroupBy(r => r.Status.ToString())
                        .Select(g => new { Status = g.Key, Count = g.Count(), Balance = g.Sum(x => x.BalanceAmount) }),
                    Items = rows
                        .OrderByDescending(r => r.OpenedAt)
                        .Select(r => new
                        {
                            r.Id,
                            r.CandidateId,
                            r.CandidateName,
                            r.PassportNumber,
                            Status = r.Status.ToString(),
                            r.TotalFeesAmount,
                            r.TotalPaidAmount,
                            r.BalanceAmount,
                            r.OpenedAt,
                        }),
                }
            });
        });

        // ──── Agreement documents (signed contracts with the foreign partner) ────
        // Every query filters on TenantId as well as the link id. The link id is a public-schema
        // key, so on its own it is not a tenant boundary.

        group.MapGet("/links/{linkId:guid}/documents", async (
            Guid linkId,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.HasPermission("partner.read"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);
            if (user.TenantId is not Guid tenantId)
                return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

            var docs = await context.PartnerAgreementDocuments.AsNoTracking()
                .Where(d => d.PartnerLinkId == linkId && d.TenantId == tenantId && !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    d.OriginalFileName,
                    d.ContentType,
                    d.FileSizeBytes,
                    d.UploadedAt,
                    d.UploadedBy
                })
                .ToListAsync();

            return Results.Ok(new { isSuccess = true, data = docs });
        });

        group.MapPost("/links/{linkId:guid}/documents", async (
            Guid linkId,
            HttpRequest httpRequest,
            IPlatformDbContext context,
            IFileStorageService storage,
            ICurrentUserService user) =>
        {
            if (!user.HasPermission("partner.update") && !user.HasPermission("partner.create"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);
            if (user.TenantId is not Guid tenantId)
                return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

            var link = await context.PartnerLinks.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == linkId && l.TenantId == tenantId && !l.IsDeleted);
            if (link is null)
                return Results.Json(new { isSuccess = false, error = "Agreement not found" }, statusCode: 404);

            if (!httpRequest.HasFormContentType)
                return Results.Json(new { isSuccess = false, error = "Expected multipart form data" }, statusCode: 400);

            var form = await httpRequest.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.Json(new { isSuccess = false, error = "File is required" }, statusCode: 400);

            await using var stream = file.OpenReadStream();
            var storedPath = await storage.UploadAsync(
                tenantId.ToString(), linkId, file.FileName, file.ContentType, stream);

            var doc = new PartnerAgreementDocument
            {
                PartnerLinkId = linkId,
                TenantId = tenantId,
                Title = form["title"].FirstOrDefault(),
                FileName = Path.GetFileName(storedPath),
                OriginalFileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FilePath = storedPath,
                FileSizeBytes = file.Length,
                UploadedBy = user.UserName
            };
            context.PartnerAgreementDocuments.Add(doc);
            await context.SaveChangesAsync();

            return Results.Created($"/api/partners/links/{linkId}/documents/{doc.Id}",
                new { isSuccess = true, data = doc.Id });
        }).DisableAntiforgery();

        group.MapGet("/links/{linkId:guid}/documents/{documentId:guid}", async (
            Guid linkId,
            Guid documentId,
            IPlatformDbContext context,
            IFileStorageService storage,
            ICurrentUserService user) =>
        {
            if (!user.HasPermission("partner.read"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);
            if (user.TenantId is not Guid tenantId)
                return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

            var doc = await context.PartnerAgreementDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId && d.PartnerLinkId == linkId
                                          && d.TenantId == tenantId && !d.IsDeleted);
            if (doc is null)
                return Results.Json(new { isSuccess = false, error = "Document not found" }, statusCode: 404);

            var stream = await storage.DownloadAsync(doc.FilePath);
            if (stream is null)
                return Results.Json(new { isSuccess = false, error = "File is missing from storage" }, statusCode: 404);

            return Results.File(stream, doc.ContentType, doc.OriginalFileName);
        });

        group.MapDelete("/links/{linkId:guid}/documents/{documentId:guid}", async (
            Guid linkId,
            Guid documentId,
            IPlatformDbContext context,
            ICurrentUserService user) =>
        {
            if (!user.HasPermission("partner.update"))
                return Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);
            if (user.TenantId is not Guid tenantId)
                return Results.Json(new { isSuccess = false, error = "Tenant context required" }, statusCode: 400);

            var doc = await context.PartnerAgreementDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.PartnerLinkId == linkId
                                          && d.TenantId == tenantId && !d.IsDeleted);
            if (doc is null)
                return Results.Json(new { isSuccess = false, error = "Document not found" }, statusCode: 404);

            doc.IsDeleted = true;
            await context.SaveChangesAsync();
            return Results.Ok(new { isSuccess = true });
        });
    }
}

public record CreatePartnerRequest(
    string Name,
    string CountryCode,
    string? CountryName,
    string? ForeignLicenseId = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? Address = null,
    string? Notes = null);

public record UpdatePartnerRequest(
    string? Name,
    string? CountryCode,
    string? CountryName,
    string? ForeignLicenseId,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    string? Notes,
    bool? IsActive);

public record CreatePartnerLinkRequest(
    Guid PartnerAgencyId,
    Guid? TenantId,
    string? AgreementStart,
    string? AgreementEnd);

public record UpdateLinkStatusRequest(string Status);
