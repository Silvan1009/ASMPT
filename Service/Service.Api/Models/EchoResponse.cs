namespace Service.Api.Models;

/// <summary>
/// Response payload returned by the Echo endpoint.
/// </summary>
/// <param name="Message">The message that was echoed back.</param>
public sealed record EchoResponse(string Message);
