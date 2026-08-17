using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.API.Features.Accounting.Commands;

public record UpsertExchangeRateCommand(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly EffectiveDate,
    string? Source = null) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "accounting.post";
}

public class UpsertExchangeRateHandler : IRequestHandler<UpsertExchangeRateCommand, Result<Guid>>
{
    private readonly IPlatformDbContext _platform;

    public UpsertExchangeRateHandler(IPlatformDbContext platform) => _platform = platform;

    public async Task<Result<Guid>> Handle(UpsertExchangeRateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FromCurrency) || string.IsNullOrWhiteSpace(request.ToCurrency))
            return Result<Guid>.Failure("FromCurrency and ToCurrency are required", 400);

        if (request.Rate <= 0)
            return Result<Guid>.Failure("Rate must be > 0", 400);

        var from = request.FromCurrency.Trim().ToUpperInvariant();
        var to = request.ToCurrency.Trim().ToUpperInvariant();

        if (from.Length != 3 || to.Length != 3)
            return Result<Guid>.Failure("Currencies must be ISO 4217 (3 letters)", 400);

        var existing = await _platform.ExchangeRates
            .FirstOrDefaultAsync(r =>
                !r.IsDeleted
                && r.FromCurrency == from
                && r.ToCurrency == to
                && r.EffectiveDate == request.EffectiveDate, ct);

        if (existing is not null)
        {
            existing.Rate = request.Rate;
            existing.Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim();
            await _platform.SaveChangesAsync(ct);
            return Result<Guid>.Success(existing.Id);
        }

        var rate = new ExchangeRate
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = request.Rate,
            EffectiveDate = request.EffectiveDate,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim()
        };

        _platform.ExchangeRates.Add(rate);
        await _platform.SaveChangesAsync(ct);
        return Result<Guid>.Success(rate.Id, 201);
    }
}
