using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    private const double RotationHandleOffset = 100;
    private const double DefaultLensFocalLength = 300;
    private OpticalScene _scene = OpticalScene.CreateEmpty();
    private double _zoom = 1;
    private SceneItemKind _movingKind;
    private MoveDragMode _moveDragMode;
    private int _movingIndex = -1;
    private Vector2D _lastMoveWorld;
    private bool _moveChanged;
    private bool _moveSimulationDirty;
    private long _lastMoveSimulationTimestamp;
    private SceneItemKind _selectedKind;
    private int _selectedIndex = -1;
    private readonly HashSet<Guid> _selectedIds = [];
    private Guid? _selectedGroupId;
    private Guid? _activeElementId;
    private Guid? _movingGroupId;

    public OpticalScene Scene => _scene;
    public CanvasSelection? Selection => CreateSelection();
    public SceneItemKind SelectedKind => _selectedKind;
    public int SelectedIndex => _selectedIndex;
    public IReadOnlySet<Guid> SelectedIds => _selectedIds;
    public Guid? SelectedGroupId => _selectedGroupId;
    public Guid? ActiveElementId => _activeElementId;
    public bool IsMoving => _movingKind != SceneItemKind.None || _movingGroupId is not null;

    public event EventHandler? SceneUpdated;
    public event EventHandler? SceneCommitted;
    public event EventHandler? PreviewRequested;
    public event EventHandler? SelectionChanged;
    public event EventHandler? InteractionStateChanged;

    public void SetScene(OpticalScene scene)
    {
        _scene = OpticalSceneNormalizer.Normalize(scene);
        _movingKind = SceneItemKind.None;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;
        _moveChanged = false;
        _moveSimulationDirty = false;
        _selectedIds.Clear();
        _selectedGroupId = null;
        _activeElementId = null;
        _movingGroupId = null;
        ClearSelection();
    }

    public void SetZoom(double zoom) => _zoom = zoom;

    private void UpdateScene(OpticalScene scene)
    {
        _scene = scene;
        SceneUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void CommitSelectedEdit()
    {
        PreviewRequested?.Invoke(this, EventArgs.Empty);
        SceneCommitted?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
