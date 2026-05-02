namespace DokkanDaily.Services.Interfaces;

public interface IBannerService
{
    string GetText();

    bool ShouldShow();

    Task SetBannerAsync(string text, bool shouldShow);
}
