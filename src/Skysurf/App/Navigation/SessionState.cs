using Shared.Connections;

namespace skysurf.App.Navigation;

/// <summary>Holds state that lives for the whole app session — currently just the
/// connection the main screen sends requests through.</summary>
public sealed class SessionState
{
    public ConnectionRecord? ActiveConnection { get; set; }
}
