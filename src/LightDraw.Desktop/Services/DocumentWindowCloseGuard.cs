using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using LightDraw.Desktop.ViewModels;

namespace LightDraw.Desktop.Services;

/// <summary>Reviews every affected document before closing any of its windows.</summary>
public sealed class DocumentWindowCloseGuard : IDisposable
{
    private static readonly List<DocumentWindowCloseGuard> Guards = [];
    private static bool _reviewing;
    private static bool _requestPending;
    private static bool _shutdownApproved;
    private readonly Window _window;
    private readonly SceneDocumentViewModel _document;
    private readonly bool _includeAllWindows;
    private bool _approved;

    public DocumentWindowCloseGuard(Window window, SceneDocumentViewModel document, bool includeAllWindows = false)
    {
        _window = window; _document = document; _includeAllWindows = includeAllWindows;
        Guards.Add(this);
        window.Closing += OnClosing;
    }

    public static void AttachLifetime(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        lifetime.ShutdownRequested += (_, e) =>
        {
            if (_shutdownApproved) return;
            e.Cancel = true;
            if (_requestPending || _reviewing) return;
            _requestPending = true;
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (await ReviewAsync(Guards.ToArray()))
                    {
                        _shutdownApproved = true;
                        lifetime.Shutdown();
                    }
                }
                finally { _requestPending = false; }
            });
        };
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_approved) return;
        e.Cancel = true;
        if (_requestPending || _reviewing) return;

        // Avalonia asks owned windows to close before raising Closing on their owner.
        // Route that first request to the owner so no child closes before the whole review succeeds.
        var target = this;
        if (e.CloseReason == WindowCloseReason.OwnerWindowClosing)
        {
            for (var owner = _window.Owner; owner is not null; owner = owner.Owner)
                target = Guards.FirstOrDefault(guard => ReferenceEquals(guard._window, owner)) ?? target;
        }
        var reviewAll = target._includeAllWindows || e.CloseReason is
            WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown;
        if (reviewAll) target = Guards.FirstOrDefault(guard => guard._includeAllWindows) ?? target;
        _requestPending = true;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var targets = reviewAll ? Guards.ToArray() : [target];
                if (await ReviewAsync(targets)) target._window.Close();
            }
            finally { _requestPending = false; }
        });
    }

    private static async Task<bool> ReviewAsync(DocumentWindowCloseGuard[] targets)
    {
        if (_reviewing || targets.Any(guard => guard._document.IsBusy)) return false;
        _reviewing = true;
        try
        {
            foreach (var guard in targets)
            {
                guard._document.FinishEditing?.Invoke();
                guard._document.SetCloseReview(true);
            }
            foreach (var guard in targets)
            {
                guard._window.Activate();
                if (!await guard._document.RequestCloseAsync()) return false;
            }
            foreach (var guard in targets) guard._approved = true;
            return true;
        }
        finally
        {
            foreach (var guard in targets) guard._document.SetCloseReview(false);
            _reviewing = false;
        }
    }

    public void Dispose()
    {
        _window.Closing -= OnClosing;
        Guards.Remove(this);
    }
}
