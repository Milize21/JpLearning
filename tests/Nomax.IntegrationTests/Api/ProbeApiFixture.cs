using Nomax.IntegrationTests.Support;

namespace Nomax.IntegrationTests.Api;

public sealed class ProbeApiFixture : IAsyncLifetime
{
    private ProbeApiHost? _host;

    internal FakeAccountStatusReader AccountStatus { get; } = new();

    internal HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await ProbeApiHost.StartAsync(AccountStatus);
        Client = _host.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }
}
