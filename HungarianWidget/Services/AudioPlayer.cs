using System.Reflection;
using System.Windows.Media;
using HungarianWidget.Models;

namespace HungarianWidget.Services;

public sealed class AudioPlayer : IDisposable
{
    private readonly string _cacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MagyarWidget", "Audio");
    private MediaPlayer? _player;

    public event Action<string>? StatusChanged;

    public void Play(LearningCard card) => PlayClip(card.Id);

    public void PlayExample(LearningCard card) => PlayClip(card.ExampleAudioId);

    private void PlayClip(string audioId)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var fileName = $"{audioId}.wav";
            var audioPath = Path.Combine(_cacheDirectory, fileName);
            if (!File.Exists(audioPath))
                ExtractAudio(fileName, audioPath);

            StopCurrent();
            var player = new MediaPlayer();
            _player = player;
            player.MediaOpened += (_, _) =>
            {
                player.Play();
                StatusChanged?.Invoke("Playing Hungarian audio…");
            };
            player.MediaEnded += (_, _) => StatusChanged?.Invoke("Tap the speaker to hear it again.");
            player.MediaFailed += (_, args) => StatusChanged?.Invoke($"Audio could not play: {args.ErrorException.Message}");
            player.Open(new Uri(audioPath, UriKind.Absolute));
            StatusChanged?.Invoke("Loading pronunciation…");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Audio could not play: {ex.Message}");
        }
    }

    public void Dispose() => StopCurrent();

    private static void ExtractAudio(string fileName, string destination)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".Content.Audio.{fileName}", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new FileNotFoundException($"Pronunciation audio for {fileName[..^4]} is not included.");

        using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Pronunciation audio could not be opened.");
        using var output = File.Create(destination);
        resource.CopyTo(output);
    }

    private void StopCurrent()
    {
        if (_player is null) return;
        try
        {
            _player.Stop();
            _player.Close();
        }
        catch { }
        _player = null;
    }
}
