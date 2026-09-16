using System.IO.Compression;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
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
/// asking for it.
/// </summary>
public class DownloadCandidateDocumentsTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly ICvGenerationService _cv = Substitute.For<ICvGenerationService>();
    private readonly DownloadCandidateDocumentsHandler _handler;

    public DownloadCandidateDocumentsTests()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TenantDbContext(options, Substitute.For<ICurrentUserService>());

        // Every stored file reads back as its own path, so a ZIP entry can be traced to the row
        // that put it there.
        _storage.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<Stream?>(
                new MemoryStream(System.Text.Encoding.UTF8.GetBytes(call.Arg<string>()))));

        _cv.GenerateAsync(Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new byte[] { 0x25, 0x50, 0x44, 0x46 }));

        _handler = new DownloadCandidateDocumentsHandler(_context, _storage, _cv);
    }

    private async Task<Guid> GivenCandidate(string passport, params DocumentType[] documents)
    {
        var id = Guid.NewGuid();
        _context.Candidates.Add(new Candidate
        {
            Id = id,
            FirstName = "SEADA",
            LastName = "MEKONNEN",
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

    private static IReadOnlyList<string> EntriesOf(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    [Fact]
    public async Task EachCandidateGetsTheirOwnFolder()
    {
        var a = await GivenCandidate("EQ1030621", DocumentType.Passport, DocumentType.Photo);
        var b = await GivenCandidate("EP7798713", DocumentType.Passport, DocumentType.Photo);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([a, b], [(int)DocumentType.Passport, (int)DocumentType.Photo]),
            default);

        result.IsSuccess.Should().BeTrue();
        var entries = EntriesOf(result.Data!);

        entries.Should().HaveCount(4);
        entries.Should().OnlyContain(e => e.Contains('/'), "a flat ZIP of forty files is unusable");
        entries.Where(e => e.StartsWith("EQ1030621")).Should().HaveCount(2);
        entries.Where(e => e.StartsWith("EP7798713")).Should().HaveCount(2);
    }

    [Fact]
    public async Task OnlyTheKindsAskedForAreIncluded()
    {
        var id = await GivenCandidate(
            "EQ1030621", DocumentType.Passport, DocumentType.Photo, DocumentType.MedicalCertificate);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.Passport]), default);

        var entries = EntriesOf(result.Data!);
        entries.Should().HaveCount(1);
        entries[0].Should().Contain("Passport");
    }

    [Fact]
    public async Task ACvIsGeneratedWhenNoneIsOnFileYet()
    {
        // Every other document has to have been uploaded by someone. A CV is drawn from the
        // candidate's own record, so asking for one and getting nothing would be a strange answer.
        var id = await GivenCandidate("EQ1030621", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.CV]), default);

        result.IsSuccess.Should().BeTrue();
        EntriesOf(result.Data!).Should().ContainSingle(e => e.Contains("CV_"));
        await _cv.Received(1).GenerateAsync(
            Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AStoredCvIsUsedRatherThanRegenerated()
    {
        var id = await GivenCandidate("EQ1030621", DocumentType.CV);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [(int)DocumentType.CV]), default);

        result.IsSuccess.Should().BeTrue();
        await _cv.DidNotReceive().GenerateAsync(
            Arg.Any<Candidate>(), Arg.Any<byte[]?>(), Arg.Any<byte[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingFileDoesNotSinkTheWholeBatch()
    {
        var a = await GivenCandidate("EQ1030621", DocumentType.Passport);
        var b = await GivenCandidate("EP7798713", DocumentType.Passport);

        // One row outlived its file — the other nineteen people still need their paperwork.
        _storage.DownloadAsync(
                Arg.Is<string>(p => p.Contains("EQ1030621")), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream?>(null));

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([a, b], [(int)DocumentType.Passport]), default);

        result.IsSuccess.Should().BeTrue();
        EntriesOf(result.Data!).Should().ContainSingle(e => e.StartsWith("EP7798713"));
    }

    [Fact]
    public async Task NothingToCollectIsSaidPlainly_NotHandedBackAsAnEmptyZip()
    {
        var id = await GivenCandidate("EQ1030621", DocumentType.Passport);

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
        var id = await GivenCandidate("EQ1030621", DocumentType.Passport);

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
        var id = await GivenCandidate("EQ1030621", DocumentType.Passport);

        var result = await _handler.Handle(
            new DownloadCandidateDocumentsCommand([id], [4242]), default);

        // 4242 is not a DocumentType, so nothing was actually asked for.
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    public void Dispose() => _context.Dispose();
}
