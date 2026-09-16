using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Candidates;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Printing a CV twice leaves one CV.
///
/// It used to leave two, because each generator appended a row and a file rather than replacing
/// what was there. One candidate in production ended up with four byte-identical CVs — 851 kB of
/// the same PDF — and the Documents tab read as a log of button presses instead of a list of the
/// candidate's paperwork. Uploads are still additive; only the generated renderings replace.
/// </summary>
public class GeneratedDocumentsTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Candidate _candidate;
    private int _uploads;

    public GeneratedDocumentsTests()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TenantDbContext(options, Substitute.For<ICurrentUserService>());

        _candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = "SEADA",
            LastName = "MEKONNEN",
            PassportNumber = "EQ1030621",
        };

        _user.UserName.Returns("desk");
        _storage.UploadAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult($"tenants/t/candidates/{_candidate.Id}/file{++_uploads}.pdf"));
    }

    private async Task Print(DocumentType type = DocumentType.CV)
    {
        await GeneratedDocuments.ReplaceAsync(
            _context, _storage, _user, "tenant_t", _candidate, type,
            "cv.pdf", "CV_SEADA.pdf", [1, 2, 3], default);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task PrintingTwiceLeavesOneCopy()
    {
        await Print();
        await Print();
        await Print();

        var docs = await _context.CandidateDocuments
            .Where(d => d.CandidateId == _candidate.Id && d.DocumentType == DocumentType.CV)
            .ToListAsync();

        docs.Should().HaveCount(1);
        docs[0].FilePath.Should().EndWith("file3.pdf", "the newest copy is the one kept");
    }

    [Fact]
    public async Task TheSupersededFilesAreDeletedFromStorage()
    {
        await Print();
        await Print();

        await _storage.Received(1).DeleteAsync(
            Arg.Is<string>(p => p.EndsWith("file1.pdf")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADocumentOfAnotherTypeIsNotTouched()
    {
        await Print(DocumentType.VisaForm);
        await Print(DocumentType.CV);

        // Replacing the CV must not take the visa form with it — they are separate documents that
        // happen to share a candidate.
        (await _context.CandidateDocuments.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task AnUploadedDocumentIsNotReplaced()
    {
        // Uploads are facts about the candidate, not renderings of them: three medical
        // certificates are three certificates, and nothing here may collapse them into one.
        for (var i = 0; i < 3; i++)
        {
            _context.CandidateDocuments.Add(new CandidateDocument
            {
                CandidateId = _candidate.Id,
                DocumentType = DocumentType.MedicalCertificate,
                FileName = $"medical{i}.pdf",
                OriginalFileName = $"medical{i}.pdf",
                ContentType = "application/pdf",
                FilePath = $"path/medical{i}.pdf",
            });
        }
        await _context.SaveChangesAsync();

        await Print();

        (await _context.CandidateDocuments
            .CountAsync(d => d.DocumentType == DocumentType.MedicalCertificate))
            .Should().Be(3);
    }

    [Fact]
    public async Task AFileThatCannotBeDeletedDoesNotFailThePrint()
    {
        await Print();
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("file is locked")));

        // Wasted disk space is not a reason to refuse someone their paperwork.
        var print = async () => await Print();
        await print.Should().NotThrowAsync();

        (await _context.CandidateDocuments.CountAsync()).Should().Be(1);
    }

    public void Dispose() => _context.Dispose();
}
