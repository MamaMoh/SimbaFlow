namespace SimbaFlow.Application.Common.Models;

/// <summary>A file sent along with an email.</summary>
public record EmailAttachment(string FileName, string ContentType, byte[] Content);
