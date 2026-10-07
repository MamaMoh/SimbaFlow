namespace SimbaFlow.Application.Common.Models;

/// <summary>
/// A rendered document and the name it should be saved under.
///
/// The generators used to hand back bare bytes, which left the endpoint to invent a filename from
/// the only thing it had — the candidate's id. The handler is the one that loaded the candidate,
/// so it is the one that can name the file after them; carrying the name out with the bytes is
/// what lets it.
/// </summary>
public sealed record GeneratedPdf(byte[] Bytes, string FileName);
