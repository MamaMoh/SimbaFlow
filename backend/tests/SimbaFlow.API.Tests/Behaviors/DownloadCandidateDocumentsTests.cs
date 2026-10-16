using System.IO.Compression;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Collecting a batch of paperwork in one go.
///
/// An embassy run needs the passport, photo and CV for twenty people at once. Doing that a document
/// at a time is sixty clicks and twenty trips into a candidate's page, which is why the desk was
/// asking for it. What comes back is one PDF per candidate, in a ZIP when there are several,
/// because each candidate's file goes to a partner on its own.
///
/// The merge itself is qpdf's job and is not exercised here — these tests pin what goes into each
/// candidate's file and in what order, and how the files are handed back, which is the part this
/// handler decides.
/// </summary>
public class DownloadCandidateDocumentsTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly ICvGenerationService _cv = Substitute.For<ICvGenerationService>();
    private readonly IPdfBundleService _bundle = Substitute.For<IPdfBundleService>();
    private readonly DownloadCandidateDocumentsHandler _handler;

    /// <summary>Each merge the handler asked for — one per candidate — in the order it asked.</summary>
    private readonly List<IReadOnlyList<PdfBundleItem>> _merges = [];

    /// <summary>Everything that was merged, across every candidate's file, in order.</summary>
    private IReadOnlyList<PdfBundleItem> _merged => _merges.SelectMany(m => m).ToList();

    public DownloadCandidateDocumentsTests()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TenantDbContext(options, Substitute.For<ICurrentUserService>());

        // Every stored file reads back as its own path, so a page in the bundle can be traced to
        // the row that put it there.
        _storage.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<Stream?>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(call.Arg<string>()))));

        _cv.GenerateAsync(Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        // Stands in for qpdf, and keeps the real service's one contractual answer: nothing to page
        // means null, which is how the handler knows to say 404 rather than serve an empty file.
        _bundle.MergeAsync(Arg.Any<IReadOnlyList<PdfBundleItem>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var items = call.Arg<IReadOnlyList<PdfBundleItem>>();
                _merges.Add(items);
                // A stand-in PDF that says whose pages it holds, so a ZIP entry can be traced back.
                return Task.FromResult<byte[]?>(items.Any(i => !i.IsDivider)
                    ? System.Text.Encoding.UTF8.GetBytes("%PDF " + string.Join(" | ", items.Select(i => i.Title)))
                    : null);
            });

        _handler = new DownloadCandidateDocumentsHandler(
            _context, _storage, _cv, _bundle,
            Substitute.For<ILogger<DownloadCandidateDocumentsHandler>>());
    }

    private async Task<Guid> GivenCandidate(
        string passport, string firstName, string lastName, params DocumentType[] documents)
    {
        var id = Guid.NewGuid();
        _context.Candidates.Add(new Candidate
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            PassportNumber = passport,
        });

        foreach (var type in documents)
        {
            _context.CandidateDocuments.Add(new CandidateDocument
            {
                CandidateId = id,
                DocumentType = type,
                FileName = $"{type}.pdf",
                OriginalFileName = $"{type}.pdf",
                ContentType = "application/pdf",
                FilePath = $"store/{passport}/{type}.pdf",
            });
        }

        await _context.SaveChangesAsync();
        return id;
    }

    /// <summary>The titles of what was merged — dividers marked, so order is visible at a glance.</summary>
    private IReadOnlyList<string> MergedTitles() =>
        _merged.Select(i => i.IsDivider ? $"[{i.Title}]" : i.Title).ToList();

    /// <summary>The entries of a returned ZIP, by name, with what each stand-in PDF says it holds.</summary>
    private static Dictionary<string, string> Entries(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(
            e => e.FullName,
            e =>
            {
                using var reader = new StreamReader(e.Open());
                return reader.ReadToEnd();
            });
    }

    [Fact]
    public async Task SeveralCandidatesComeBackAsAZipWithOnePdfEach()
    {
        var seada = await GivenCandidate(
            "EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport, DocumentType.Photo);
        var hanan = await GivenCandidate(
            "EP7798713", "HANAN", "ABDI", DocumentType.Passport, DocumentType.Photo);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                [seada, hanan], [(int)DocumentType.Passport, (int)DocumentType.Photo]),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ContentType.Should().Be("application/zip");
        result.Data.FileName.Should().EndWith(".zip");

        // Each file is one person's paperwork, named after them, and holds nobody else's pages.
        var entries = Entries(result.Data.Content);
        entries.Keys.Should().BeEquivalentTo("HANAN ABDI - Documents.pdf", "SEADA MEKONNEN - Documents.pdf");
        entries["HANAN ABDI - Documents.pdf"].Should().Be("%PDF HANAN ABDI · Passport | HANAN ABDI · Photo");
        entries["SEADA MEKONNEN - Documents.pdf"].Should().Be("%PDF SEADA MEKONNEN · Passport | SEADA MEKONNEN · Photo");
    }

    [Fact]
    public async Task NoCandidatesFileCarriesADividerPage()
    {
        // The divider only existed to split one stack of everybody back apart by hand. Each
        // person is their own file now, so it would be a blank page at the front of every one.
        var seada = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);
        var hanan = await GivenCandidate("EP7798713", "HANAN", "ABDI", DocumentType.Passport);

        await _handler.Handle(
            new DownloadCandidateDocumentsCommand([seada, hanan], [(int)DocumentType.Passport]), default);

        _merges.Should().HaveCount(2);
        _merged.Should().NotContain(i => i.IsDivider);
    }

    [Fact]
    public async Task TwoCandidatesWithOneNameStillGetTwoFiles()
    {
        // A ZIP with two entries of the same name extracts to one file, and one person's paperwork
        // silently replaces the other's.
        var first = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);
        var second = await GivenCandidate("EP7798713", "SEADA", "MEKONNEN", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([first, second], [(int)DocumentType.Passport]), default);

        Entries(result.Data!.Content).Keys.Should().HaveCount(2)
            .And.Contain("SEADA MEKONNEN - Documents.pdf")
            .And.Contain(name => name.Contains("SEADA MEKONNEN E") && name.EndsWith(" - Documents.pdf"));
    }

    [Fact]
    public async Task ACandidateWithNothingToSendIsLeftOutOfTheZip()
    {
        var seada = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);
        var hanan = await GivenCandidate("EP7798713", "HANAN", "ABDI");

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([seada, hanan], [(int)DocumentType.Passport]), default);

        result.IsSuccess.Should().BeTrue();
        Entries(result.Data!.Content).Keys.Should().Equal("SEADA MEKONNEN - Documents.pdf");
    }

    [Fact]
    public async Task ThePacketCarriesTheSignedContractRatherThanBoth()
    {
        // Both exist whenever the contract was drawn before the parties sent theirs back. Two
        // contracts that disagree, in one stapled packet, with nothing to say which is in force.
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN");
        _context.CandidateDocuments.AddRange(
            Document(id, DocumentType.Contract, "store/ours.pdf", generated: true),
            Document(id, DocumentType.Contract, "store/signed.pdf", generated: false));
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.Contract]), default);

        _merged.Should().ContainSingle();
        await _storage.Received().DownloadAsync("store/signed.pdf", Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DownloadAsync("store/ours.pdf", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeveralUploadsOfOneKindAllGoInThePacket()
    {
        // The rule above is about choosing between a drawing and the real thing, not about
        // collapsing uploads: a passport has pages, and all of them are wanted.
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN");
        _context.CandidateDocuments.AddRange(
            Document(id, DocumentType.Passport, "store/p1.pdf", generated: false),
            Document(id, DocumentType.Passport, "store/p2.pdf", generated: false));
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.Passport]), default);

        _merged.Should().HaveCount(2);
    }

    private static CandidateDocument Document(
        Guid candidateId, DocumentType type, string path, bool generated) => new()
    {
        CandidateId = candidateId,
        DocumentType = type,
        FileName = Path.GetFileName(path),
        OriginalFileName = Path.GetFileName(path),
        ContentType = "application/pdf",
        FilePath = path,
        IsGenerated = generated,
    };

    [Fact]
    public async Task TheHeadshotIsPrintedAsThePortrait_ThePassportAndFullPhotoAsSnapshots()
    {
        var id = await GivenCandidate(
            "EQ1030621", "SEADA", "MEKONNEN",
            DocumentType.Passport, DocumentType.Photo, DocumentType.FullPhoto);

        await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                [id], [(int)DocumentType.Passport, (int)DocumentType.Photo, (int)DocumentType.FullPhoto]),
            default);

        _merged.Single(i => i.Title.EndsWith("· Photo")).IsPortrait.Should().BeTrue();
        _merged.Where(i => !i.Title.EndsWith("· Photo")).Should()
            .OnlyContain(i => i.IsSnapshot && !i.IsPortrait);
    }

    [Fact]
    public async Task OneCandidateIsOnePdf_NotAZipAroundOne()
    {
        // A ZIP holding a single file is one more thing to open before the print dialog.
        var id = await GivenCandidate(
            "EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport, DocumentType.Photo);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                [id], [(int)DocumentType.Passport, (int)DocumentType.Photo]),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ContentType.Should().Be("application/pdf");
        result.Data.FileName.Should().Be("SEADA MEKONNEN - Documents.pdf");
        _merged.Should().NotContain(i => i.IsDivider);
    }

    [Fact]
    public async Task OnlyTheKindsAskedForAreIncluded()
    {
        var id = await GivenCandidate(
            "EQ1030621", "SEADA", "MEKONNEN",
            DocumentType.Passport, DocumentType.Photo, DocumentType.MedicalCertificate);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.Passport]), default);

        result.IsSuccess.Should().BeTrue();
        MergedTitles().Should().Equal("SEADA MEKONNEN · Passport");
    }

    [Fact]
    public async Task ACvIsGeneratedWhenNoneIsOnFileYet()
    {
        // Every other document has to have been uploaded by someone. A CV is drawn from the
        // candidate's own record, so asking for one and getting nothing would be a strange answer.
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.CV]), default);

        result.IsSuccess.Should().BeTrue();
        MergedTitles().Should().Equal("SEADA MEKONNEN · CV");
        await _cv.Received(1).GenerateAsync(
            Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheCvIsDrawnFreshEvenWhenACopyIsOnFile()
    {
        // A stored CV is a snapshot of whichever layout was selected the day someone last pressed
        // the button. Serving it back means the packet is printed in whatever layouts happen to be
        // on file rather than the one the agency chose.
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.CV);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.CV]), default);

        result.IsSuccess.Should().BeTrue();
        MergedTitles().Should().Equal("SEADA MEKONNEN · CV");
        await _cv.Received(1).GenerateAsync(
            Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeveralCopiesOnFileStillMakeOneCvPage()
    {
        // Every press of the button used to file a fresh row, so candidates registered before that
        // changed have a pile of them — which arrived in the packet as several CVs in several
        // layouts, one per copy.
        var id = await GivenCandidate(
            "EQ1030621", "SEADA", "MEKONNEN",
            DocumentType.CV, DocumentType.CV, DocumentType.CV, DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                [id], [(int)DocumentType.CV, (int)DocumentType.Passport]),
            default);

        result.IsSuccess.Should().BeTrue();
        MergedTitles().Should().Equal(
            "SEADA MEKONNEN · CV",
            "SEADA MEKONNEN · Passport");
    }

    [Fact]
    public async Task ACvThatWillNotDrawFallsBackToTheCopyOnFile()
    {
        // A photo the renderer cannot open should cost this candidate their CV page at worst, and
        // not the other nineteen people theirs.
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.CV);
        _cv.GenerateAsync(Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("unreadable photo"));

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.CV]), default);

        result.IsSuccess.Should().BeTrue();
        MergedTitles().Should().Equal("SEADA MEKONNEN · CV");
    }

    [Fact]
    public async Task ThePagesComeOutInTheOrderTheDialogListsTheKinds()
    {
        // The order the desk assembles the packet in, and so the order the dialog offers. Sorting
        // on the DocumentType value instead orders it by the order the kinds were added to the
        // enum, which puts the passport (0) ahead of the CV (3) and scatters the rest.
        var id = await GivenCandidate(
            "EQ1030621", "SEADA", "MEKONNEN",
            DocumentType.Other,
            DocumentType.TicketBooking,
            DocumentType.TasheerDocument,
            DocumentType.LMIS,
            DocumentType.MedicalCertificate,
            DocumentType.VisaForm,
            DocumentType.Contract,
            DocumentType.FullPhoto,
            DocumentType.Photo,
            DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                [id],
                [.. Enum.GetValues<DocumentType>().Select(t => (int)t)]),
            default);

        result.IsSuccess.Should().BeTrue();
        MergedTitles().Should().Equal(
            "SEADA MEKONNEN · CV",
            "SEADA MEKONNEN · Passport",
            "SEADA MEKONNEN · Photo",
            "SEADA MEKONNEN · FullPhoto",
            "SEADA MEKONNEN · Contract",
            "SEADA MEKONNEN · VisaForm",
            "SEADA MEKONNEN · MedicalCertificate",
            "SEADA MEKONNEN · LMIS",
            "SEADA MEKONNEN · TasheerDocument",
            "SEADA MEKONNEN · TicketBooking",
            "SEADA MEKONNEN · Other");
    }

    [Fact]
    public async Task AMissingFileDoesNotSinkTheWholeBatch()
    {
        var seada = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);
        var hanan = await GivenCandidate("EP7798713", "HANAN", "ABDI", DocumentType.Passport);

        // One row outlived its file — the other nineteen people still need their paperwork.
        _storage.DownloadAsync(
                Arg.Is<string>(p => p.Contains("EQ1030621")), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream?>(null));

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                [seada, hanan], [(int)DocumentType.Passport]),
            default);

        result.IsSuccess.Should().BeTrue();
        _merged.Where(i => !i.IsDivider).Select(i => i.Title)
            .Should().Equal("HANAN ABDI · Passport");
    }

    [Fact]
    public async Task NothingToCollectIsSaidPlainly_NotHandedBackAsAnEmptyFile()
    {
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.MedicalCertificate]), default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Error.Should().Contain("documents");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BothHalvesOfTheRequestAreRequired(bool candidates, bool types)
    {
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand(
                candidates ? [id] : [],
                types ? [(int)DocumentType.Passport] : []),
            default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AnUnknownDocumentTypeIsIgnoredRatherThanTrusted()
    {
        var id = await GivenCandidate("EQ1030621", "SEADA", "MEKONNEN", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [4242]), default);

        // 4242 is not a DocumentType, so nothing was actually asked for.
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    public void Dispose() => _context.Dispose();
}
