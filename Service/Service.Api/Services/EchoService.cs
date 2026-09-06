using Service.Api.Models;

namespace Service.Api.Services;

public sealed class EchoService : IEchoService
{
    public EchoResponse Echo(string message) => new(message);
}
