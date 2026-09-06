using System.Net.Http.Headers;
using System.Text;
using UI.Web.Auth;

namespace UI.Web.ApiClient;

/// <summary>
/// Hand-written half of the NSwag-generated client (see the OpenApiReference in UI.Web.csproj): attaches the
/// current user's Firebase ID token to every Service API call.
/// </summary>
public partial class ServiceApiClient
{
    private readonly IUserTokenProvider? _tokenProvider;

    // AddHttpClient<IServiceApiClient, ServiceApiClient> activates the client through ActivatorUtilities in the
    // caller's DI scope (the request during static SSR, the circuit for interactive components), so the scoped
    // token provider is the right one. The attribute makes the constructor choice unambiguous.
    [ActivatorUtilitiesConstructor]
    public ServiceApiClient(HttpClient httpClient, IUserTokenProvider tokenProvider)
        : this(httpClient)
    {
        _tokenProvider = tokenProvider;
    }

    // With GeneratePrepareRequestAndProcessResponseAsAsyncMethods NSwag only emits the calls to these three
    // methods; the definitions are mandatory.
    private Task PrepareRequestAsync(HttpClient client, HttpRequestMessage request, StringBuilder urlBuilder, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private async Task PrepareRequestAsync(HttpClient client, HttpRequestMessage request, string url, CancellationToken cancellationToken)
    {
        if (_tokenProvider is null)
        {
            return;
        }

        var idToken = await _tokenProvider.GetIdTokenAsync(cancellationToken);
        if (idToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        }
    }

    private Task ProcessResponseAsync(HttpClient client, HttpResponseMessage response, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
