using MediatR;
using Microsoft.AspNetCore.Http;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// Reads an uploaded Saudi contract and hands back what it says, so the desk can confirm the
/// details rather than retype them.
///
/// Nothing is saved. The file is read, the fields are offered for review, and the operator decides
/// what to keep — a contract the reader half-understands must not quietly overwrite a record. The
/// same file is uploaded separately once they are happy, by the usual document route.
///
/// An unreadable file succeeds with an empty result. The desk is handed scans often enough that
/// "nothing recognised, type it in" is a normal outcome rather than an error to surface.
/// </summary>
public record ReadContractCommand(IFormFile File)
    : IRequest<Result<SaudiContractDetails>>, IRequirePermission
{
    public string RequiredPermission => "candidate.update";
}

public class ReadContractHandler : IRequestHandler<ReadContractCommand, Result<SaudiContractDetails>>
{
    /// <summary>
    /// A contract is a handful of pages of text. Anything materially larger is not one, and
    /// reading it would hold a request open while it allocated.
    /// </summary>
    private const long MaxBytes = 20 * 1024 * 1024;

    private readonly IContractReaderService _reader;

    public ReadContractHandler(IContractReaderService reader) => _reader = reader;

    public Task<Result<SaudiContractDetails>> Handle(ReadContractCommand request, CancellationToken ct)
    {
        if (request.File.Length == 0)
            return Task.FromResult(Result<SaudiContractDetails>.Failure("The file is empty", 400));

        if (request.File.Length > MaxBytes)
            return Task.FromResult(Result<SaudiContractDetails>.Failure(
                "The file is too large to read — 20 MB is the limit", 400));

        using var stream = request.File.OpenReadStream();
        return Task.FromResult(Result<SaudiContractDetails>.Success(_reader.Read(stream)));
    }
}
