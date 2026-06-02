using Shared.Connections;
using skysurf.Features.SchemaCatalog;

namespace skysurf.App.Navigation;

public sealed class WizardState
{
    public ConnectionRecord? SelectedConnection { get; set; }
    public SkyEndpoint? SelectedEndpoint { get; set; }
}
