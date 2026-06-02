using Terminal.Gui;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.QueryExecution;

public sealed class QueryLoadingScreen : FrameView
{
    public QueryLoadingScreen(SkyEndpoint endpoint)
    {
        Title = "Running query";

        Add(new Label($"Running {endpoint.ApiName} {endpoint.Path}...")
        {
            X = 1,
            Y = 1
        });
    }
}
