using System.IO.Compression;
using FluentAssertions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Every CV layout renders, and renders on one sheet.
///
/// These are printed and handed to an embassy or a partner, who expect a single sheet. A box sized
/// by hand — and several of them are, because QuestPDF has no way to make one half of a row take
/// its height from the other half — is one careless number away from pushing the form onto a second
/// page, where the overflow is a mostly-empty sheet nobody notices until it is in an envelope. It
/// is also the ceiling on the type scale in FormType: these forms are dense, and a point of extra
/// height per row is thirty points down a long column.
///
/// The letterhead and the photographs are supplied because leaving them out is not the smaller
/// case, it is a different one. Without a logo the layouts draw a short fallback band instead of
/// the full-height image, and without a full-length photograph the column beside the applicant's
/// details collapses to a line of placeholder text — so a sheet that overflows in production fits
/// here. This renders what an agency actually prints.
///
/// One caveat on that. A row's height is set by whichever script in it is tallest, and Arabic
/// usually is; the Arabic font is whatever the machine has (see DocumentFonts) — Noto on the
/// server, Arial Unicode on a developer's Mac — and their line metrics differ by enough to change
/// the answer. So this catches a layout that is plainly too big, not one that is marginal.
///
/// Set SIMBAFLOW_CV_OUT to a directory and the PDFs are written there, which is how the layouts
/// were compared against the printed forms they are meant to match.
/// </summary>
public class CvRenderTests
{
    private sealed class StubBranding(string template) : IDocumentBrandingService
    {
        // A wide, short banner — the proportions of an agency letterhead, which is what decides
        // how much of the page is left for the form.
        public Task<byte[]?> GetHeaderLogoAsync(Candidate c, CancellationToken ct = default) =>
            Task.FromResult<byte[]?>(SolidPng(1200, 130));

        public Task<string> GetCvTemplateAsync(CancellationToken ct = default) =>
            Task.FromResult(template);
    }

    private static Candidate SampleCandidate() => new()
    {
        FirstName = "ZINET",
        MiddleName = "YIBRE",
        LastName = "ABEBAW",
        PassportNumber = "E00201748",
        PassportIssueDate = new DateOnly(2026, 3, 6),
        PassportExpiryDate = new DateOnly(2031, 3, 5),
        PassportPlaceOfIssue = "ETHIOPIA",
        DateOfBirth = new DateOnly(2005, 6, 18),
        PlaceOfBirth = "SOUTH WOLLO",
        Nationality = "Ethiopia",
        Religion = "Muslim",
        MaritalStatus = "Single",
        NumberOfChildren = 0,
        PhoneNumber = "+251926646949",
        Occupation = "HOUSE MAID",
        MonthlySalary = "1000",
        ContractPeriod = "2 Years",
        CountryOfTravel = "Saudi Arabia",
        Qualification = "SECONDARY LEVEL",
        ApplicationNo = "APP-20261007-0001",
        SkillCleaning = true,
        SkillWashing = true,
        SkillBabysitting = true,
        CookingLevel = "None",
    };

    /// <summary>
    /// Driven by the catalogue rather than a list written here, so a layout added later is covered
    /// without anyone remembering to add it.
    /// </summary>
    public static TheoryData<string, string> Layouts()
    {
        var data = new TheoryData<string, string>();
        foreach (var (value, name, _) in CvTemplates.All) data.Add(value, name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public async Task EveryLayoutRendersOneSheet(string template, string name)
    {
        var service = new CvGenerationService(new StubBranding(template));

        var pdf = await service.GenerateAsync(
            SampleCandidate(),
            SolidPng(300, 400),   // passport photograph
            SolidPng(400, 840));  // full length

        pdf.Should().NotBeNullOrEmpty();
        // "%PDF" — a renderer that fails quietly is worse than one that throws.
        pdf.Take(4).Should().Equal((byte)'%', (byte)'P', (byte)'D', (byte)'F');

        var outDir = Environment.GetEnvironmentVariable("SIMBAFLOW_CV_OUT");
        if (!string.IsNullOrWhiteSpace(outDir))
        {
            Directory.CreateDirectory(outDir);
            await File.WriteAllBytesAsync(Path.Combine(outDir, $"{template}.pdf"), pdf);
        }

        PageCount(pdf).Should().Be(1, $"the {name} CV is a single-sheet form");
    }

    /// <summary>Counts page objects in the PDF — /Type /Pages is the tree root, not a page.</summary>
    private static int PageCount(byte[] pdf) =>
        System.Text.RegularExpressions.Regex
            .Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type\s*/Page(?![s])")
            .Count;

    /// <summary>
    /// A solid PNG of the given size, written by hand.
    ///
    /// The layouts only care about an image's dimensions — they scale it into a box — so a real
    /// photograph would add nothing but a binary file in the repository. Encoding one here keeps
    /// the shape of the picture visible at the place it matters, which is the aspect ratio of a
    /// passport photograph against a full-length one.
    /// </summary>
    private static byte[] SolidPng(int width, int height)
    {
        // Raw scanlines: a leading filter byte (0 = none) then RGB per pixel.
        var raw = new byte[height * (1 + width * 3)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 3);
            raw[row] = 0;
            for (var x = 0; x < width * 3; x++) raw[row + 1 + x] = 0xC8;
        }

        using var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(raw);

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // colour type: truecolour
        // 10..12: compression, filter and interlace methods, all zero.

        using var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", ihdr);
        WriteChunk(png, "IDAT", deflated.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream to, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        to.Write(length);

        // The CRC covers the type and the data together, not the length.
        var typed = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(typed, 0);
        data.CopyTo(typed, 4);
        to.Write(typed);

        var crc = new byte[4];
        WriteBigEndian(crc, 0, unchecked((int)Crc32(typed)));
        to.Write(crc);
    }

    private static void WriteBigEndian(byte[] into, int at, int value)
    {
        into[at] = (byte)(value >> 24);
        into[at + 1] = (byte)(value >> 16);
        into[at + 2] = (byte)(value >> 8);
        into[at + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
