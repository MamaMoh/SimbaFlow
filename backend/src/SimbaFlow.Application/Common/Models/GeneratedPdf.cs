namespace SimbaFlow.Application.Common.Models;

/// <summary>
/// A document to hand back, and the name it should be saved under.
///
/// The generators used to hand back bare bytes, which left the endpoint to invent a filename from
/// the only thing it had — the candidate's id. The handler is the one that loaded the candidate,
/// so it is the one that can name the file after them; carrying the name out with the bytes is
/// what lets it.
///
/// The content type travels with them because not everything here is drawn. Asked for a contract
/// that was signed and uploaded, the handler returns that file, and the parties' scanner may well
/// have produced a JPEG — served as a PDF it downloads as something the browser cannot open.
/// </summary>
public sealed record GeneratedPdf(byte[] Bytes, string FileName, string ContentType = "application/pdf");
