using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Asking for a candidate's contract.
///
/// A Saudi placement runs on a contract the parties sign, and the signed copy is uploaded when the
/// candidate is marked Ready. From that moment the contract exists, and drawing ours again is not
/// producing the document — it is producing a different one, unsigned, and differing from the real
/// one by whatever has been edited since. Everywhere else there is nothing signed to return and
/// ours is the contract.
/// </summary>
public class ContractDownloadTests : IDisposable
{
    private readonly TenantDbContext _tenant;
    private readonly PlatformDbContext _platform;
    private readonly IContractGenerationService _contracts = Substitute.For<IContractGenerationService>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly GenerateContractHandler _handler;
    private readonly Candidate _candidate;

    private static readonly byte[] Drawn = [0x25, 0x50, 0x44, 0x46, 0x44];
    private static readonly byte[] Signed = [0x25, 0x50, 0x44, 0x46, 0x53];

    public ContractDownloadTests()
    {
        _tenant = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        _platform = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        _candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = "SEADA",
            LastName = "MEKONNEN",
            PassportNumber = "EQ1030621",
            PartnerName = "Example Resources Company",
        };
        _tenant.Candidates.Add(_candidate);
        _tenant.SaveChanges();

        _contracts.GenerateAsync(Arg.Any<Candidate>(), Arg.Any<ContractParties>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Drawn));

        _storage.UploadAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("store/drawn.pdf"));

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.SchemaName.Returns("tenant_t");

        _handler = new GenerateContractHandler(
            _tenant, _platform, _contracts,
            Substitute.For<ICurrentUserService>(), _storage, tenantContext);
    }

    private void GivenSignedContractOnFile(
        string path = "store/signed.pdf",
        string originalName = "contract scan.pdf",
        string contentType = "application/pdf",
        DateTime? uploadedAt = null)
    {
        _tenant.CandidateDocuments.Add(new CandidateDocument
        {
            CandidateId = _candidate.Id,
            DocumentType = DocumentType.Contract,
            FileName = Path.GetFileName(path),
            OriginalFileName = originalName,
            ContentType = contentType,
            FilePath = path,
            IsGenerated = false,
            UploadedAt = uploadedAt ?? DateTime.UtcNow,
        });
        _tenant.SaveChanges();

        _storage.DownloadAsync(path, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(new MemoryStream(Signed)));
    }

    private Task<SimbaFlow.Application.Common.Models.Result<SimbaFlow.Application.Common.Models.GeneratedPdf>> Ask() =>
        _handler.Handle(new GenerateContractCommand(_candidate.Id), default);

    [Fact]
    public async Task TheSignedContractIsWhatComesBack()
    {
        GivenSignedContractOnFile();

        var result = await Ask();

        result.Data!.Bytes.Should().Equal(Signed);
        await _contracts.DidNotReceive().GenerateAsync(
            Arg.Any<Candidate>(), Arg.Any<ContractParties>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandingBackTheSignedCopyFilesNothingNew()
    {
        // Returning what is already on file must not write another row, or every download adds a
        // document and the Documents tab fills up with copies of the upload.
        GivenSignedContractOnFile();

        await Ask();

        (await _tenant.CandidateDocuments.CountAsync()).Should().Be(1);
        await _storage.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItIsStillNamedAfterTheCandidate()
    {
        GivenSignedContractOnFile();

        (await Ask()).Data!.FileName.Should().Be("SEADA MEKONNEN - Contract.pdf");
    }

    [Fact]
    public async Task AScannedContractKeepsItsOwnTypeAndExtension()
    {
        // These come back from the parties photographed as often as scanned. Served as a PDF it
        // downloads as a file the browser refuses to open.
        GivenSignedContractOnFile(
            path: "store/signed.jpg", originalName: "IMG_4821.JPG", contentType: "image/jpeg");

        var result = await Ask();

        result.Data!.FileName.Should().Be("SEADA MEKONNEN - Contract.JPG");
        result.Data.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task TheLatestSignedCopyWins()
    {
        // Re-signed after an amendment: the later upload is the one in force.
        GivenSignedContractOnFile(path: "store/old.pdf", uploadedAt: DateTime.UtcNow.AddDays(-30));
        _storage.DownloadAsync("store/old.pdf", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(new MemoryStream([0x25, 0x4F])));
        GivenSignedContractOnFile(path: "store/new.pdf");

        (await Ask()).Data!.Bytes.Should().Equal(Signed);
    }

    [Fact]
    public async Task WithNothingSignedOnFileOursIsDrawn()
    {
        var result = await Ask();

        result.Data!.Bytes.Should().Equal(Drawn);
        result.Data.ContentType.Should().Be("application/pdf");
        (await _tenant.CandidateDocuments.SingleAsync()).IsGenerated.Should().BeTrue();
    }

    [Fact]
    public async Task AnEarlierDrawingOfOursIsNotMistakenForASignedOne()
    {
        // Ours on file is still ours. Left unchecked, the first download after a generate would
        // start handing back the stored copy and the contract would stop reflecting the record.
        await Ask();
        _contracts.ClearReceivedCalls();

        await Ask();

        await _contracts.Received(1).GenerateAsync(
            Arg.Any<Candidate>(), Arg.Any<ContractParties>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARowWhoseFileHasGoneMissingFallsBackToDrawingOne()
    {
        GivenSignedContractOnFile(path: "store/gone.pdf");
        _storage.DownloadAsync("store/gone.pdf", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(null));

        // An error here is a dead end on a document the desk needs now; our version is not it,
        // but it is something.
        (await Ask()).Data!.Bytes.Should().Equal(Drawn);
    }

    public void Dispose()
    {
        _tenant.Dispose();
        _platform.Dispose();
    }
}
