namespace Service.Api.Services;

/// <summary>The rendered order download: a file name and its JSON content.</summary>
public sealed record OrderExportFile(string FileName, byte[] Content);
