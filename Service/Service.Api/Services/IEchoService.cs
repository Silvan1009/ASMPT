using Service.Api.Models;

namespace Service.Api.Services;

/// <summary>
/// Business logic behind the Echo endpoint. The controller depends on this interface, not a
/// concrete class, so it can be unit-tested with a mock/fake implementation instead of
/// exercising real logic end to end.
/// </summary>
public interface IEchoService
{
    EchoResponse Echo(string message);
}
