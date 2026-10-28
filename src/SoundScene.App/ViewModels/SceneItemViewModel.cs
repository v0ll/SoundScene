using System.Windows.Input;
using SoundScene.Core.Scenes;

namespace SoundScene.App.ViewModels;

/// <summary>
/// One row in the scene list.
/// </summary>
/// <remarks>
/// Deleting takes two clicks. A scene is a thing the user deliberately built,
/// and the popup is too small for a confirmation dialog that would steal focus
/// from it and dismiss it.
/// </remarks>
public sealed class SceneItemViewModel : Mvvm.ObservableObject
{
    private readonly Func<Scene, Task> _apply;
    private readonly Action<Scene> _delete;
    private bool _isConfirmingDelete;

    public SceneItemViewModel(
        Scene scene,
        Func<Scene, Task> apply,
        Action<Scene> delete,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(delete);

        Scene = scene;
        _apply = apply;
        _delete = delete;

        ApplyCommand = new Mvvm.AsyncRelayCommand(() => _apply(Scene), onError);
        DeleteCommand = new Mvvm.RelayCommand(Delete);
    }

    public Scene Scene { get; }

    public string Name => Scene.Name;

    public string Summary => Scene.Summary;

    public ICommand ApplyCommand { get; }

    public ICommand DeleteCommand { get; }

    /// <summary>True after the first delete click; the second one actually removes the scene.</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set => SetProperty(ref _isConfirmingDelete, value);
    }

    private void Delete()
    {
        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return;
        }

        _delete(Scene);
    }
}
