using System;
using System.Collections.Generic;

namespace FileActivityNotifier.UI.Toast;

public class ToastStackManager
{
    private readonly List<ToastWindow> _activeToasts = new();

    public void ShowToast(Func<ToastViewModel> viewModelFactory, double durationSeconds, int maxStackCount)
    {
        var dispatcher = App.UiDispatcherQueue;
        if (dispatcher == null)
        {
            return;
        }

        if (dispatcher.HasThreadAccess)
        {
            ShowOnUiThread(viewModelFactory, durationSeconds, maxStackCount);
            return;
        }

        dispatcher.TryEnqueue(() => ShowOnUiThread(viewModelFactory, durationSeconds, maxStackCount));
    }

    public void DismissAll()
    {
        var dispatcher = App.UiDispatcherQueue;
        if (dispatcher == null)
        {
            return;
        }

        dispatcher.TryEnqueue(() =>
        {
            foreach (var toast in _activeToasts.ToArray())
            {
                toast.Dismissed -= Toast_Dismissed;
                toast.ForceDismiss();
            }

            _activeToasts.Clear();
        });
    }

    private void ShowOnUiThread(Func<ToastViewModel> viewModelFactory, double durationSeconds, int maxStackCount)
    {
        try
        {
            if (viewModelFactory == null)
            {
                return;
            }

            var viewModel = viewModelFactory();
            if (viewModel == null)
            {
                return;
            }

            var safeMaxStack = Math.Max(1, maxStackCount);

            if (_activeToasts.Count >= safeMaxStack)
            {
                _activeToasts[^1].ForceDismiss();
            }

            var toast = new ToastWindow(viewModel, Math.Max(0.5, durationSeconds));
            toast.Dismissed += Toast_Dismissed;

            _activeToasts.Insert(0, toast);
            Reflow(animate: false);

            toast.ShowToastWindow();
            Reflow(animate: true);
        }
        catch
        {
            // Keep notification pipeline resilient if toast creation fails.
            return;
        }
    }

    private void Toast_Dismissed(object? sender, EventArgs e)
    {
        if (sender is not ToastWindow toast)
        {
            return;
        }

        toast.Dismissed -= Toast_Dismissed;
        _activeToasts.Remove(toast);
        Reflow(animate: true);
    }

    private void Reflow(bool animate)
    {
        for (var index = 0; index < _activeToasts.Count; index++)
        {
            var toast = _activeToasts[index];
            toast.SetStackIndex(index);

            if (animate)
            {
                toast.RepositionAnimated(index);
            }
        }
    }
}
