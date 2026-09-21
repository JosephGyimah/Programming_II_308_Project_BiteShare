using System.Net.Http.Headers;

namespace BiteShare.Client.Services;

public class IdentityAuthHandler : DelegatingHandler
{
    private readonly AuthTokenStore _tokens;

    public IdentityAuthHandler(AuthTokenStore tokens) => _tokens = tokens;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tokens.IdentityToken is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}

public class ParticipantAuthHandler : DelegatingHandler
{
    private readonly AuthTokenStore _tokens;

    public ParticipantAuthHandler(AuthTokenStore tokens) => _tokens = tokens;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tokens.ParticipantToken is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
