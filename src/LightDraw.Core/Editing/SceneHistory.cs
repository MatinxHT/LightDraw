namespace LightDraw.Core.Editing;

/// <summary>Stores completed edits, with independent snapshots and a bounded undo history.</summary>
public sealed class SceneHistory<T> where T : class
{
    private readonly Func<T, T> _snapshot;
    private readonly Func<T, T, bool> _equals;
    private readonly int _capacity;
    private readonly List<T> _states = [];
    private int _index;
    private T _saved;

    public SceneHistory(T initial, Func<T, T> snapshot, Func<T, T, bool> equals, int capacity = 100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _snapshot = snapshot;
        _equals = equals;
        _capacity = capacity;
        _saved = snapshot(initial);
        _states.Add(snapshot(initial));
    }

    public bool CanUndo => _index > 0;
    public bool CanRedo => _index < _states.Count - 1;
    public bool IsModified(T scene) => !_equals(scene, _saved);

    public void Commit(T scene)
    {
        if (_equals(scene, _states[_index])) return;
        _states.RemoveRange(_index + 1, _states.Count - _index - 1);
        _states.Add(_snapshot(scene));
        if (_states.Count > _capacity + 1) _states.RemoveAt(0);
        _index = _states.Count - 1;
    }

    public T Undo()
    {
        if (CanUndo) _index--;
        return _snapshot(_states[_index]);
    }

    public T Redo()
    {
        if (CanRedo) _index++;
        return _snapshot(_states[_index]);
    }

    public void MarkSaved(T scene) => _saved = _snapshot(scene);

    public void Reset(T scene)
    {
        _states.Clear();
        _states.Add(_snapshot(scene));
        _index = 0;
        MarkSaved(scene);
    }
}
