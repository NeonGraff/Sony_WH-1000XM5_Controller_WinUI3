using SonyHeadphonesNet.Core;

namespace SonyHeadphonesNet.Devices.Models;

/// <summary>
/// Headphone playback status and track metadata.
/// </summary>
public record PlaybackInfo(
    PlaybackStatus Status,
    string? Title = null,
    string? Artist = null,
    string? Album = null
)
{
    public bool IsPlaying => Status == PlaybackStatus.PLAY;
    public bool IsPaused => Status == PlaybackStatus.PAUSE;

    public override string ToString()
    {
        string statusStr = Status switch
        {
            PlaybackStatus.PLAY => "Playing",
            PlaybackStatus.PAUSE => "Paused",
            PlaybackStatus.STOP => "Stopped",
            _ => "Unknown"
        };

        if (!string.IsNullOrEmpty(Title))
        {
            string artistPart = !string.IsNullOrEmpty(Artist) ? $" by {Artist}" : "";
            return $"{statusStr}: \"{Title}\"{artistPart}";
        }

        return statusStr;
    }
}
