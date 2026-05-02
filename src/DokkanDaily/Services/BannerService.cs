using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services;

public class BannerService : IBannerService
{
    private readonly object _lock = new();
    private string _text = string.Empty;
    private bool _shouldShow;

    public string GetText()
    {
        lock (_lock)
        {
            return _text;
        }
    }

    public bool ShouldShow()
    {
        lock (_lock)
        {
            return _shouldShow && !string.IsNullOrWhiteSpace(_text);
        }
    }

    public Task SetBannerAsync(string text, bool shouldShow)
    {
        lock (_lock)
        {
            _text = text?.Trim() ?? string.Empty;
            _shouldShow = shouldShow;
        }

        return Task.CompletedTask;
    }
}
