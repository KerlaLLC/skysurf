using System.Diagnostics;
using Shared.Authentication;
using Shared.Views;
using Terminal.Gui;

namespace Shared.Connections.Views;

public sealed class ConnectionManagerScreen : View
{
    private readonly IConnectionRepository _connectionRepository;
    private readonly SkyAuthenticationService _authService;
    private readonly Action<ConnectionRecord>? _onSelect;
    private readonly Action<ConnectionRecord>? _onGetToken;
    private readonly Action<ConnectionRecord>? _onRotateToken;

    private readonly ListView _listView;
    private readonly TextField _searchField;
    private readonly GroupBox _rightPanel;
    private readonly TextField _nameField;
    private readonly TextField _subscriptionKeyField;
    private readonly TextField _clientIdField;
    private readonly TextField _clientSecretField;
    private readonly TextView _authUrlText;
    private readonly TextField _authCodeField;
    private readonly Label _authCodeLabel;
    private readonly Label _formStatusLabel;
    private readonly CheckBox? _defaultCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    private List<ConnectionRecord> _allConnections = [];
    private List<ConnectionRecord> _filteredConnections = [];
    private ConnectionRecord? _editingRecord;
    private bool _isAdding;
    private bool _isSaving;
    private bool _suppressSelectionChange;

    public ConnectionManagerScreen(
        IConnectionRepository connectionRepository,
        SkyAuthenticationService authService,
        Action<ConnectionRecord>? onSelect = null,
        Action<ConnectionRecord>? onGetToken = null,
        Action<ConnectionRecord>? onRotateToken = null,
        bool showQuit = false,
        bool showDefaultOption = false)
    {
        _connectionRepository = connectionRepository;
        _authService = authService;
        _onSelect = onSelect;
        _onGetToken = onGetToken;
        _onRotateToken = onRotateToken;

        // ── Left panel ────────────────────────────────────────────────────
        var leftPanel = new GroupBox("_Connections")
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(40),
            Height = Dim.Fill(3)
        };

        leftPanel.Add(new Label("Search") { X = 1, Y = 0 });

        _searchField = new TextField(string.Empty)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2)
        };
        _searchField.TextChanged += _ => ApplyFilter();

        _listView = new ListView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1)
        };
        _listView.SelectedItemChanged += _ => OnListSelectionChanged();
        _listView.KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.CursorUp && _listView.SelectedItem == 0)
            {
                _searchField.SetFocus();
                e.Handled = true;
            }
            else if (!e.KeyEvent.IsAlt && !e.KeyEvent.IsCtrl
                     && e.KeyEvent.KeyValue >= 32 && e.KeyEvent.KeyValue < 127)
            {
                _searchField.Text = (_searchField.Text?.ToString() ?? string.Empty) + (char)e.KeyEvent.KeyValue;
                _searchField.CursorPosition = _searchField.Text?.ToString()?.Length ?? 0;
                _searchField.SetFocus();
                e.Handled = true;
            }
        };

        leftPanel.Add(_searchField, _listView);

        // ── Right panel ───────────────────────────────────────────────────
        _rightPanel = new GroupBox("Connection _Details")
        {
            X = Pos.Right(leftPanel),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        _rightPanel.Add(new Label("Name") { X = 1, Y = 1 });
        _nameField = new TextField(string.Empty) { X = 22, Y = 1, Width = Dim.Fill(2) };
        _rightPanel.Add(_nameField);

        _rightPanel.Add(new Label("Subscription Key") { X = 1, Y = 3 });
        _subscriptionKeyField = new TextField(string.Empty) { X = 22, Y = 3, Width = Dim.Fill(2), Secret = true };
        _rightPanel.Add(_subscriptionKeyField);

        _rightPanel.Add(new Label("Client ID") { X = 1, Y = 5 });
        _clientIdField = new TextField(string.Empty) { X = 22, Y = 5, Width = Dim.Fill(2) };
        _clientIdField.TextChanged += _ => UpdateAuthUrl();
        _rightPanel.Add(_clientIdField);

        _rightPanel.Add(new Label("Client Secret") { X = 1, Y = 7 });
        _clientSecretField = new TextField(string.Empty) { X = 22, Y = 7, Width = Dim.Fill(2), Secret = true };
        _rightPanel.Add(_clientSecretField);

        _rightPanel.Add(new Label("Authorization URL") { X = 1, Y = 9 });
        _authUrlText = new TextView
        {
            X = 22,
            Y = 9,
            Width = Dim.Fill(2),
            Height = 4,
            Text = SkyAuthenticationService.GetAuthorizationUrl(string.Empty),
            ReadOnly = true,
            WordWrap = true,
            CanFocus = false
        };
        _rightPanel.Add(_authUrlText);

        var openBrowserButton = new Button("Open in Browser") { X = 1, Y = 13 };
        openBrowserButton.Clicked += OpenBrowser;
        _rightPanel.Add(openBrowserButton);

        var copyButton = new Button("Copy to Clipboard")
        {
            X = Pos.Right(openBrowserButton) + 1,
            Y = 13
        };
        copyButton.Clicked += CopyToClipboard;
        _rightPanel.Add(copyButton);

        _authCodeLabel = new Label("OAuth2 Code") { X = 1, Y = 15 };
        _authCodeField = new TextField(string.Empty) { X = 22, Y = 15, Width = Dim.Fill(2) };
        _rightPanel.Add(_authCodeLabel, _authCodeField);

        _formStatusLabel = new Label(string.Empty) { X = 1, Y = 17, Width = Dim.Fill(2) };
        _rightPanel.Add(_formStatusLabel);

        if (showDefaultOption)
        {
            _defaultCheckBox = new CheckBox("Use as default connection") { X = 1, Y = 18 };
            _rightPanel.Add(_defaultCheckBox);
        }

        _saveButton = new Button("Save") { X = 1, Y = 19 };
        _saveButton.Clicked += Save;
        _cancelButton = new Button("Cancel") { X = Pos.Right(_saveButton) + 2, Y = 19 };
        _cancelButton.Clicked += Cancel;
        _rightPanel.Add(_saveButton, _cancelButton);

        // ── Bottom button bar ─────────────────────────────────────────────
        var addButton = new Button("Add") { X = 1, Y = Pos.AnchorEnd(2) };
        addButton.Clicked += StartAdd;

        var deleteButton = new Button("Delete") { X = Pos.Right(addButton) + 2, Y = Pos.AnchorEnd(2) };
        deleteButton.Clicked += DeleteSelected;

        Add(leftPanel, _rightPanel, addButton, deleteButton);

        Button lastBarButton = deleteButton;

        if (onSelect != null)
        {
            var selectButton = new Button("Se_lect")
            {
                X = Pos.Right(lastBarButton) + 2,
                Y = Pos.AnchorEnd(2)
            };
            selectButton.Clicked += ActivateSelection;
            Add(selectButton);
            lastBarButton = selectButton;

            // Enter (or double-click) on the focused list selects the connection.
            _listView.OpenSelectedItem += _ => ActivateSelection();
        }

        if (onGetToken != null)
        {
            var tokenButton = new Button("Get Token")
            {
                X = Pos.Right(lastBarButton) + 2,
                Y = Pos.AnchorEnd(2)
            };
            tokenButton.Clicked += InvokeGetToken;
            Add(tokenButton);
            lastBarButton = tokenButton;
        }

        if (onRotateToken != null)
        {
            var rotateButton = new Button("Rotate Refresh Token")
            {
                X = Pos.Right(lastBarButton) + 2,
                Y = Pos.AnchorEnd(2)
            };
            rotateButton.Clicked += InvokeRotateToken;
            Add(rotateButton);
            lastBarButton = rotateButton;
        }

        if (showQuit)
        {
            var quitButton = new Button("Quit (Ctrl+Q)")
            {
                X = Pos.AnchorEnd(18),
                Y = Pos.AnchorEnd(2)
            };
            quitButton.Clicked += () => Application.RequestStop();
            Add(quitButton);
        }

        ReloadConnections();
    }

    // ── List management ───────────────────────────────────────────────────

    private void ReloadConnections()
    {
        _allConnections = _connectionRepository.List().ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = _searchField.Text?.ToString() ?? string.Empty;
        _filteredConnections = _allConnections
            .Where(x => string.IsNullOrWhiteSpace(query)
                || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.ClientId.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _suppressSelectionChange = true;
        _listView.SetSource(_filteredConnections.Select(x => x.Name).Cast<object>().ToList());
        _suppressSelectionChange = false;

        if (_editingRecord != null)
        {
            var idx = _filteredConnections.FindIndex(x => x.Id == _editingRecord.Id);
            if (idx >= 0)
            {
                _editingRecord = _filteredConnections[idx];
                _listView.SelectedItem = idx;
            }
        }
    }

    private void OnListSelectionChanged()
    {
        if (_suppressSelectionChange || _isSaving) return;

        var selected = GetSelectedFromList();
        if (selected is null) return;

        // Skip if same record already loaded and not mid-add
        if (selected.Id == _editingRecord?.Id && !_isAdding) return;

        _editingRecord = selected;
        _isAdding = false;
        PopulateForm(selected);
        UpdateAuthCodeLabel();
    }

    private ConnectionRecord? GetSelectedFromList()
    {
        if (_filteredConnections.Count == 0
            || _listView.SelectedItem < 0
            || _listView.SelectedItem >= _filteredConnections.Count)
        {
            return null;
        }

        return _filteredConnections[_listView.SelectedItem];
    }

    // ── Form management ───────────────────────────────────────────────────

    private void PopulateForm(ConnectionRecord record)
    {
        _nameField.Text = record.Name;
        _subscriptionKeyField.Text = record.SubscriptionKey;
        _clientIdField.Text = record.ClientId;
        _clientSecretField.Text = record.ClientSecret;
        _authCodeField.Text = string.Empty;
        _formStatusLabel.Text = GetTokenStatus(record);
        if (_defaultCheckBox != null)
            _defaultCheckBox.Checked = record.IsDefault;
    }

    private void ClearForm()
    {
        _nameField.Text = string.Empty;
        _subscriptionKeyField.Text = string.Empty;
        _clientIdField.Text = string.Empty;
        _clientSecretField.Text = string.Empty;
        _authCodeField.Text = string.Empty;
        _formStatusLabel.Text = string.Empty;
        if (_defaultCheckBox != null)
            _defaultCheckBox.Checked = false;
    }

    private static string GetTokenStatus(ConnectionRecord record)
    {
        if (record.RefreshTokenValidToUtc <= DateTime.UtcNow)
            return "Token EXPIRED";
        if (record.RefreshTokenValidToUtc <= DateTime.UtcNow.AddMonths(6))
            return "Token expiring soon (auto-rotates on next use)";
        return string.Empty;
    }

    private void UpdateAuthCodeLabel()
    {
        _authCodeLabel.Text = _isAdding
            ? "OAuth2 Code"
            : "New OAuth2 Code (blank = keep)";
    }

    private void UpdateAuthUrl()
    {
        var clientId = _clientIdField.Text?.ToString() ?? string.Empty;
        _authUrlText.Text = SkyAuthenticationService.GetAuthorizationUrl(clientId);
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private void OpenBrowser()
    {
        var clientId = _clientIdField.Text?.ToString() ?? string.Empty;
        var url = SkyAuthenticationService.GetAuthorizationUrl(clientId);
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    private void CopyToClipboard()
    {
        var clientId = _clientIdField.Text?.ToString() ?? string.Empty;
        var url = SkyAuthenticationService.GetAuthorizationUrl(clientId);
        Clipboard.TrySetClipboardData(url);
    }

    private void StartAdd()
    {
        _editingRecord = null;
        _isAdding = true;
        ClearForm();
        UpdateAuthCodeLabel();
        _nameField.SetFocus();
    }

    private void Cancel()
    {
        _isAdding = false;

        if (_editingRecord != null)
        {
            PopulateForm(_editingRecord);
            UpdateAuthCodeLabel();
        }
        else
        {
            ClearForm();
            UpdateAuthCodeLabel();
        }
    }

    private void Save()
    {
        if (_isSaving) return;

        if (!_isAdding && _editingRecord == null)
        {
            _formStatusLabel.Text = "Select a connection from the list, or press Add.";
            return;
        }

        var name = _nameField.Text?.ToString() ?? string.Empty;
        var subscriptionKey = _subscriptionKeyField.Text?.ToString() ?? string.Empty;
        var clientId = _clientIdField.Text?.ToString() ?? string.Empty;
        var clientSecret = _clientSecretField.Text?.ToString() ?? string.Empty;
        var authCode = _authCodeField.Text?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(subscriptionKey)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret))
        {
            _formStatusLabel.Text = "Name, Subscription Key, Client ID, and Client Secret are required.";
            return;
        }

        if (_isAdding && string.IsNullOrWhiteSpace(authCode))
        {
            _formStatusLabel.Text = "OAuth2 Code is required for new connections.";
            return;
        }

        _isSaving = true;
        _saveButton.Enabled = false;
        _cancelButton.Enabled = false;
        _formStatusLabel.Text = !string.IsNullOrWhiteSpace(authCode) ? "Authenticating\u2026" : "Saving\u2026";

        var editingRecord = _editingRecord;
        var isAdding = _isAdding;
        var wantDefault = _defaultCheckBox?.Checked ?? false;

        _ = Task.Run(async () =>
        {
            try
            {
                string refreshToken;
                DateTime expiresAtUtc;

                if (!string.IsNullOrWhiteSpace(authCode))
                {
                    (refreshToken, expiresAtUtc) = await _authService.ExchangeAuthCodeAsync(
                        clientId, clientSecret, authCode);
                }
                else
                {
                    refreshToken = editingRecord!.RefreshToken;
                    expiresAtUtc = editingRecord.RefreshTokenValidToUtc;
                }

                Application.MainLoop.Invoke(() =>
                {
                    try
                    {
                        ConnectionRecord saved;

                        if (isAdding)
                        {
                            saved = new ConnectionRecord
                            {
                                Name = name,
                                SubscriptionKey = subscriptionKey,
                                ClientId = clientId,
                                ClientSecret = clientSecret,
                                RefreshToken = refreshToken,
                                RefreshTokenValidToUtc = expiresAtUtc,
                                IsDefault = wantDefault
                            };
                            _connectionRepository.Add(saved);
                        }
                        else
                        {
                            saved = new ConnectionRecord
                            {
                                Id = editingRecord!.Id,
                                CreatedUtc = editingRecord.CreatedUtc,
                                LastUsedUtc = editingRecord.LastUsedUtc,
                                Name = name,
                                SubscriptionKey = subscriptionKey,
                                ClientId = clientId,
                                ClientSecret = clientSecret,
                                RefreshToken = refreshToken,
                                RefreshTokenValidToUtc = expiresAtUtc,
                                IsDefault = wantDefault
                            };
                            _connectionRepository.Update(saved);
                        }

                        // Enforce single-default: clears the flag on every other connection.
                        if (wantDefault)
                            _connectionRepository.SetDefault(saved.Id);

                        _editingRecord = saved;
                        _isAdding = false;
                        _isSaving = false;
                        _saveButton.Enabled = true;
                        _cancelButton.Enabled = true;
                        _formStatusLabel.Text = "Saved.";
                        UpdateAuthCodeLabel();
                        ReloadConnections();
                    }
                    catch (Exception innerEx)
                    {
                        _isSaving = false;
                        _saveButton.Enabled = true;
                        _cancelButton.Enabled = true;
                        _formStatusLabel.Text = $"Error: {innerEx.Message}";
                    }
                });
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    _isSaving = false;
                    _saveButton.Enabled = true;
                    _cancelButton.Enabled = true;
                    _formStatusLabel.Text = $"Error: {ex.Message}";
                });
            }
        });
    }

    private void DeleteSelected()
    {
        var selected = GetSelectedFromList();
        if (selected is null) return;

        var answer = MessageBox.Query("Delete connection", $"Delete '{selected.Name}'?", "No", "Yes");
        if (answer != 1) return;

        _connectionRepository.Delete(selected.Id);

        if (_editingRecord?.Id == selected.Id)
        {
            _editingRecord = null;
            _isAdding = false;
            ClearForm();
            UpdateAuthCodeLabel();
        }

        ReloadConnections();
    }

    private void ActivateSelection()
    {
        var selected = GetSelectedFromList();
        if (selected is null) return;
        _onSelect!(selected);
    }

    private void InvokeGetToken()
    {
        var selected = GetSelectedFromList();
        if (selected is null) return;
        _onGetToken!(selected);
    }

    private void InvokeRotateToken()
    {
        var selected = GetSelectedFromList();
        if (selected is null) return;
        _onRotateToken!(selected);
    }
}
