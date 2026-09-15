using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Identity;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Services;

public class RefreshTokenServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RefreshTokenService _service;
    private readonly Guid _testUserId = Guid.NewGuid();

    public RefreshTokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var currentUser = Substitute.For<ICurrentUserService>();
        var domainEventDispatcher = Substitute.For<IDomainEventDispatcher>();
        _context = new ApplicationDbContext(options, currentUser, domainEventDispatcher);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiryDays"] = "7",
                ["Jwt:MaxActiveRefreshTokens"] = "5",
            })
            .Build();

        var logger = Substitute.For<ILogger<RefreshTokenService>>();
        _service = new RefreshTokenService(_context, config, logger);
    }

    [Fact]
    public async Task CreateAsync_GeneratesTokenAndStoresHash()
    {
        // Act
        var (token, rawValue) = await _service.CreateAsync(_testUserId, "127.0.0.1");

        // Assert
        token.Should().NotBeNull();
        token.UserId.Should().Be(_testUserId);
        token.TokenHash.Should().NotBeEmpty();
        token.IsActive.Should().BeTrue();
        rawValue.Should().NotBeEmpty();

        // Verify stored in DB
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.TokenHash.Should().Be(token.TokenHash);
    }

    [Fact]
    public async Task CreateAsync_EnforcesMaxTokenLimit()
    {
        // Arrange — create 5 tokens (max)
        for (int i = 0; i < 5; i++)
            await _service.CreateAsync(_testUserId, "127.0.0.1");

        // Act — create 6th token
        await _service.CreateAsync(_testUserId, "127.0.0.1");

        // Assert — oldest should be revoked
        var activeCount = await _context.RefreshTokens
            .CountAsync(t => t.UserId == _testUserId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow);
        activeCount.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task RotateAsync_RevokesOldAndCreatesNew()
    {
        // Arrange
        var (_, rawValue) = await _service.CreateAsync(_testUserId, "127.0.0.1");

        // Act
        var (newToken, newRaw, isTheft) = await _service.RotateAsync(rawValue, "127.0.0.1");

        // Assert
        isTheft.Should().BeFalse();
        newToken.Should().NotBeNull();
        newRaw.Should().NotBeEmpty();
        newRaw.Should().NotBe(rawValue);

        // Old token should be revoked
        var tokens = await _context.RefreshTokens.ToListAsync();
        tokens.Should().HaveCount(2);
        tokens.First(t => t.TokenHash != newToken.TokenHash).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RotateAsync_ServesAConcurrentRefresh_RatherThanCallingItTheft()
    {
        // Two of the app's own requests can refresh at the same time; the loser arrives holding a
        // token the winner rotated a moment earlier. Treating that as theft revoked every session
        // the user had and threw them out of the app mid-task.
        var (_, rawValue) = await _service.CreateAsync(_testUserId, "127.0.0.1");
        await _service.RotateAsync(rawValue, "127.0.0.1");

        var (_, raceRaw, isTheft) = await _service.RotateAsync(rawValue, "127.0.0.1");

        isTheft.Should().BeFalse();
        raceRaw.Should().NotBeNullOrEmpty("the caller needs a usable token back");

        var activeTokens = await _context.RefreshTokens
            .CountAsync(t => t.UserId == _testUserId && t.RevokedAt == null);
        activeTokens.Should().BeGreaterThan(0, "the user's session must survive");
    }

    [Fact]
    public async Task RotateAsync_DetectsTheft_WhenARotatedTokenComesBackLater()
    {
        // Outside the grace window the answer is unchanged: a replay has to be near-instant to
        // pass for a race, and this one is not.
        var (_, rawValue) = await _service.CreateAsync(_testUserId, "127.0.0.1");
        await _service.RotateAsync(rawValue, "127.0.0.1");

        var rotated = await _context.RefreshTokens
            .FirstAsync(t => t.UserId == _testUserId && t.RevokedAt != null);
        rotated.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        await _context.SaveChangesAsync();

        var (_, _, isTheft) = await _service.RotateAsync(rawValue, "192.168.1.100");

        isTheft.Should().BeTrue();

        var activeTokens = await _context.RefreshTokens
            .CountAsync(t => t.UserId == _testUserId && t.RevokedAt == null);
        activeTokens.Should().Be(0);
    }

    [Fact]
    public async Task RotateAsync_DetectsTheft_WhenATokenRevokedForAnotherReasonComesBack()
    {
        // A token revoked by signing out or a password change was never part of a rotation, so
        // its reappearance is not a race however recent it is.
        var (_, rawValue) = await _service.CreateAsync(_testUserId, "127.0.0.1");
        await _service.RevokeAsync(rawValue, "127.0.0.1", "LoggedOut");

        var (_, _, isTheft) = await _service.RotateAsync(rawValue, "192.168.1.100");

        isTheft.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAllForUserAsync_RevokesAllActiveTokens()
    {
        // Arrange
        await _service.CreateAsync(_testUserId, "127.0.0.1");
        await _service.CreateAsync(_testUserId, "127.0.0.1");
        await _service.CreateAsync(_testUserId, "127.0.0.1");

        // Act
        await _service.RevokeAllForUserAsync(_testUserId, "TestReason");

        // Assert
        var activeCount = await _context.RefreshTokens
            .CountAsync(t => t.UserId == _testUserId && t.RevokedAt == null);
        activeCount.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
