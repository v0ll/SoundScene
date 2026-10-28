namespace SoundScene.Core.Audio;

/// <summary>Raised when a backend call fails. Carries the raw HRESULT when there is one.</summary>
public sealed class AudioBackendException : Exception
{
    public AudioBackendException(string message) : base(message)
    {
    }

    public AudioBackendException(string message, int hresult) : base(message)
    {
        HResult = hresult;
    }

    public AudioBackendException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
