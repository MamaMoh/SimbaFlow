using MediatR;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Services.Documents;

namespace SimbaFlow.API.Features.Embassy.Commands;

/// <summary>
/// The slip a candidate carries to their Tasheer appointment.
///
/// Generated at booking time and filed against the candidate, so the desk is not retyping the
/// same details into a Word template for every appointment.
/// </summary>
public record GenerateTasheerDocumentCommand(Guid CandidateId, DateOnly? AppointmentDate = null)
    : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "embassy.update";
}

public class GenerateTasheerDocumentHandler
    : IRequestHandler<GenerateTasheerDocumentCommand, Result<byte[]>>
{
    private readonly ITenantDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUser;

    public GenerateTasheerDocumentHandler(
        ITenantDbContext context,
        IFileStorageService fileStorage,
        ITenantContext tenantContext,
        ICurrentUserService currentUser)
    {
        _context = context;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Result<byte[]>> Handle(GenerateTasheerDocumentCommand request, CancellationToken ct)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null) return Result<byte[]>.Failure("Candidate not found", 404);

        var pdf = Compose(candidate, request.AppointmentDate);

        await Candidates.GeneratedDocuments.ReplaceAsync(
            _context, _fileStorage, _currentUser, _tenantContext.SchemaName ?? "default", candidate,
            DocumentType.TasheerDocument,
            $"tasheer_{candidate.PassportNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            $"Tasheer_{candidate.FullName.Replace(' ', '_')}.pdf",
            pdf,
            ct);
        await _context.SaveChangesAsync(ct);

        return Result<byte[]>.Success(pdf);
    }

    private static byte[] Compose(Candidate c, DateOnly? appointment)
    {
        DocumentFonts.EnsureInitialized();
        static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x =>
                    x.FontFamily(DocumentFonts.Chain).FontSize(10).FontColor(Colors.Black));

                page.Content().Column(root =>
                {
                    root.Item().AlignCenter().Text("TASHEER APPOINTMENT SLIP").FontSize(15).Bold();
                    root.Item().PaddingTop(2).AlignCenter()
                        .Text("تصريح موعد التأشير").FontSize(12).Bold();

                    root.Item().PaddingTop(18).Text(t =>
                    {
                        t.Span("Appointment: ").Bold();
                        t.Span(appointment?.ToString("dddd, dd MMMM yyyy") ?? "to be confirmed");
                    });

                    root.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(150);
                            cols.RelativeColumn();
                        });

                        void Row(string label, string value)
                        {
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .Background(Colors.Grey.Lighten4).PaddingVertical(5).PaddingHorizontal(8)
                                .Text(label).FontSize(9.5f);
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1)
                                .PaddingVertical(5).PaddingHorizontal(8)
                                .Text(value).FontSize(9.5f).Bold();
                        }

                        Row("Full name", c.FullName.ToUpperInvariant());
                        Row("Passport no.", V(c.PassportNumber));
                        Row("Passport expiry", c.PassportExpiryDate?.ToString("dd/MM/yyyy") ?? "—");
                        Row("Date of birth", c.DateOfBirth.ToString("dd/MM/yyyy"));
                        Row("Nationality", V(c.Nationality));
                        Row("Occupation", V(c.Occupation));
                        Row("Destination", V(c.CountryOfTravel));
                        Row("Visa no.", V(c.VisaNumber));
                        Row("Sponsor", V(c.SponsorName));
                        Row("Partner agency", V(c.PartnerName));
                    });

                    root.Item().PaddingTop(22)
                        .Text("Bring the original passport and this slip to the appointment.")
                        .FontSize(9).Italic();
                    root.Item().PaddingTop(2).AlignRight()
                        .Text("يُرجى إحضار جواز السفر الأصلي وهذه الورقة إلى الموعد.")
                        .FontSize(9).Italic();

                    root.Item().PaddingTop(34).Row(r =>
                    {
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().PaddingTop(16).BorderBottom(0.7f).BorderColor(Colors.Grey.Darken1);
                            col.Item().PaddingTop(3).Text("Candidate signature").FontSize(8.5f);
                        });
                        r.ConstantItem(40);
                        r.RelativeItem().Column(col =>
                        {
                            col.Item().PaddingTop(16).BorderBottom(0.7f).BorderColor(Colors.Grey.Darken1);
                            col.Item().PaddingTop(3).Text("Agency stamp").FontSize(8.5f);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
