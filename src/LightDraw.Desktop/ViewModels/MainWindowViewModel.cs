using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightDraw.Core.Scene;
using LightDraw.Core.Simulation;
using LightDraw.Desktop.Services;
using LightDraw.Rendering.Skia.Optics;

namespace LightDraw.Desktop.ViewModels;

public sealed partial class MainWindowViewModel(ISceneStorageService sceneStorage) : ObservableObject
{
    private CancellationTokenSource? _rayDensityUpdate;
    private bool _updatingSelection;
    private CanvasSelection? _selection;
    private bool _isPlacing;

    [ObservableProperty]
    private int _selectedLanguageIndex = LocalizationService.Instance.IsEnglish ? 1 : 0;

    [ObservableProperty]
    private OpticalScene _currentScene = OpticalScene.CreateEmpty();

    [ObservableProperty]
    private int _rayDensity = 160;

    [ObservableProperty]
    private int _appliedRaysPerSource = 160;

    [ObservableProperty]
    private CanvasTool _activeTool = CanvasTool.Pan;

    [ObservableProperty]
    private string _statusText = LocalizationService.Instance.Get("Status.Ready");

    [ObservableProperty]
    private bool _hasSelectedElement;

    [ObservableProperty]
    private bool _canEditSelectedTransform;

    [ObservableProperty]
    private bool _canGroupSelection;

    [ObservableProperty]
    private bool _canUngroupSelection;

    [ObservableProperty]
    private bool _canSetPrimaryElement;

    [ObservableProperty]
    private bool _hasSelectedLightSource;

    [ObservableProperty]
    private bool _hasSelectedPointLight;

    [ObservableProperty]
    private bool _hasSelectedCentralAngle;

    [ObservableProperty]
    private bool _canRotateSelectedElement;

    [ObservableProperty]
    private bool _hasSelectedLens;

    [ObservableProperty]
    private bool _hasSelectedThinLens;

    [ObservableProperty]
    private bool _hasSelectedDispersiveLens;

    [ObservableProperty]
    private bool _hasSelectedSphericalMirror;

    [ObservableProperty]
    private bool _hasSelectedSecondOrigin;

    [ObservableProperty]
    private bool _hasSelectedAperture;

    [ObservableProperty]
    private bool _hasSelectedReflectionGrating;

    [ObservableProperty]
    private bool _hasSelectedLength;

    [ObservableProperty]
    private bool _canTemporarilyHideSelectedElement;

    [ObservableProperty]
    private bool _isSelectedElementTemporarilyHidden;

    [ObservableProperty]
    private string _selectedElementName = LocalizationService.Instance.Get("Selection.None");

    [ObservableProperty]
    private string _selectedElementTitle = string.Empty;

    [ObservableProperty]
    private bool _canRenameSelectedElement;

    [ObservableProperty]
    private string _selectedAngleText = LocalizationService.Instance.Get("Selection.AngleNone");

    [ObservableProperty]
    private decimal _selectedFocalLength = 100;

    [ObservableProperty]
    private int _selectedLensDispersionModeIndex;

    [ObservableProperty]
    private int _selectedLensDispersionLevel = 5;

    [ObservableProperty]
    private decimal _selectedSphericalMirrorRadius = 200;

    [ObservableProperty]
    private decimal _selectedSphericalMirrorArcAngle = 180;

    [ObservableProperty]
    private decimal _selectedPointLightEmissionAngle = 360;

    [ObservableProperty]
    private decimal _selectedCentralAngle = 360;

    [ObservableProperty]
    private decimal _selectedApertureOpening = 30;

    [ObservableProperty]
    private decimal _selectedGrooveDensity = 600;

    [ObservableProperty]
    private decimal _selectedWavelength = (decimal)LightSource.MonochromaticWavelengthNanometers;

    [ObservableProperty]
    private decimal _selectedLength = 100;

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        var normalized = value == 1 ? 1 : 0;
        if (value != normalized)
        {
            SelectedLanguageIndex = normalized;
            return;
        }

        LocalizationService.Instance.SetLanguage(normalized == 1 ? "en-US" : "zh-CN");
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        UpdateSelection(_selection);
        UpdateToolState(ActiveTool, _isPlacing);
    }

    [ObservableProperty]
    private decimal _rotationStep = 15;

    [ObservableProperty]
    private decimal _selectedOriginX;

    [ObservableProperty]
    private decimal _selectedOriginY;

    [ObservableProperty]
    private decimal _selectedSecondOriginX;

    [ObservableProperty]
    private decimal _selectedSecondOriginY;

    [ObservableProperty]
    private int _selectedStandardAngleIndex;

    public event EventHandler? ResetViewRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? OpenElectrostaticSimulationRequested;
    public event EventHandler? OpenMagnetostaticSimulationRequested;
    public event Action<double>? RotateSelectedRequested;
    public event Action<double>? SetSelectedAngleRequested;
    public event Action<double>? SetSelectedFocalLengthRequested;
    public event Action<LensDispersionMode>? SetSelectedLensDispersionModeRequested;
    public event Action<int>? SetSelectedLensDispersionLevelRequested;
    public event Action<double>? SetSelectedSphericalMirrorRadiusRequested;
    public event Action<double>? SetSelectedSphericalMirrorArcAngleRequested;
    public event Action<double>? SetSelectedPointLightEmissionAngleRequested;
    public event Action<double>? SetSelectedCentralAngleRequested;
    public event Action<double>? SetSelectedApertureOpeningRequested;
    public event Action<double>? SetSelectedGrooveDensityRequested;
    public event Action<double>? SetSelectedWavelengthRequested;
    public event Action<double>? SetSelectedLengthRequested;
    public event Action<bool>? SetSelectedTemporarilyHiddenRequested;
    public event Action<double, double>? SetSelectedOriginRequested;
    public event Action<double, double>? SetSelectedSecondOriginRequested;
    public event Action<string>? SetSelectedNameRequested;
    public event EventHandler? GroupSelectionRequested;
    public event EventHandler? UngroupSelectionRequested;
    public event EventHandler? SetPrimaryElementRequested;

    partial void OnIsSelectedElementTemporarilyHiddenChanged(bool value)
    {
        if (!_updatingSelection && CanTemporarilyHideSelectedElement)
        {
            SetSelectedTemporarilyHiddenRequested?.Invoke(value);
        }
    }

    partial void OnRayDensityChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 1000);
        if (clamped != value)
        {
            RayDensity = clamped;
            return;
        }

        _rayDensityUpdate?.Cancel();
        _rayDensityUpdate?.Dispose();
        _rayDensityUpdate = new CancellationTokenSource();
        _ = ApplyRayDensityAsync(clamped, _rayDensityUpdate.Token);
    }

    partial void OnSelectedFocalLengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 10000);
        if (clamped != value)
        {
            SelectedFocalLength = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedLens)
        {
            SetSelectedFocalLengthRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedSphericalMirrorRadiusChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 2, 20000);
        if (clamped != value)
        {
            SelectedSphericalMirrorRadius = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedSphericalMirror)
        {
            SetSelectedSphericalMirrorRadiusRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedLensDispersionModeIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
        {
            SelectedLensDispersionModeIndex = clamped;
            return;
        }

        var mode = (LensDispersionMode)clamped;
        HasSelectedDispersiveLens = HasSelectedThinLens && mode != LensDispersionMode.None;
        if (!_updatingSelection && HasSelectedThinLens)
        {
            SetSelectedLensDispersionModeRequested?.Invoke(mode);
        }
    }

    partial void OnSelectedLensDispersionLevelChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 10);
        if (clamped != value)
        {
            SelectedLensDispersionLevel = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedThinLens)
        {
            SetSelectedLensDispersionLevelRequested?.Invoke(clamped);
        }
    }

    partial void OnSelectedSphericalMirrorArcAngleChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 359.9m);
        if (clamped != value)
        {
            SelectedSphericalMirrorArcAngle = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedSphericalMirror)
        {
            SetSelectedSphericalMirrorArcAngleRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedPointLightEmissionAngleChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 360);
        if (clamped != value)
        {
            SelectedPointLightEmissionAngle = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedPointLight)
        {
            SetSelectedPointLightEmissionAngleRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedCentralAngleChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 360);
        if (clamped != value)
        {
            SelectedCentralAngle = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedCentralAngle)
        {
            SetSelectedCentralAngleRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedApertureOpeningChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0, 10000);
        if (clamped != value)
        {
            SelectedApertureOpening = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedAperture)
        {
            SetSelectedApertureOpeningRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedGrooveDensityChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 5000);
        if (clamped != value)
        {
            SelectedGrooveDensity = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedReflectionGrating)
        {
            SetSelectedGrooveDensityRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedWavelengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 1000000);
        if (clamped != value)
        {
            SelectedWavelength = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedLightSource)
        {
            SetSelectedWavelengthRequested?.Invoke((double)clamped);
        }
    }

    partial void OnSelectedLengthChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 1, 10000);
        if (clamped != value)
        {
            SelectedLength = clamped;
            return;
        }

        if (!_updatingSelection && HasSelectedLength)
        {
            SetSelectedLengthRequested?.Invoke((double)clamped);
        }
    }

    partial void OnRotationStepChanged(decimal value)
    {
        var clamped = Math.Clamp(value, 0.1m, 360);
        if (clamped != value)
        {
            RotationStep = clamped;
        }
    }

    partial void OnSelectedOriginXChanged(decimal value) => ApplySelectedOrigin(value, SelectedOriginY);

    partial void OnSelectedOriginYChanged(decimal value) => ApplySelectedOrigin(SelectedOriginX, value);

    partial void OnSelectedSecondOriginXChanged(decimal value) =>
        ApplySelectedSecondOrigin(value, SelectedSecondOriginY);

    partial void OnSelectedSecondOriginYChanged(decimal value) =>
        ApplySelectedSecondOrigin(SelectedSecondOriginX, value);

    partial void OnSelectedElementTitleChanged(string value)
    {
        if (!_updatingSelection && CanRenameSelectedElement &&
            !string.IsNullOrWhiteSpace(value) && value.Length <= 120)
        {
            SetSelectedNameRequested?.Invoke(value);
        }
    }

    partial void OnSelectedStandardAngleIndexChanged(int value)
    {
        if (_updatingSelection || value <= 0 || !CanRotateSelectedElement)
        {
            return;
        }

        var standardAngles = new[] { 0d, 90d, 180d, 270d };
        var angleIndex = value - 1;
        if (angleIndex < standardAngles.Length)
        {
            SetSelectedAngleRequested?.Invoke(standardAngles[angleIndex]);
        }
    }

    [RelayCommand]
    private void SelectTool(string? value)
    {
        if (Enum.TryParse<CanvasTool>(value, out var tool))
        {
            ActiveTool = tool;
            UpdateToolState(tool, false);
        }
    }

    [RelayCommand]
    private void RotateSelected(string? direction)
    {
        if (!CanRotateSelectedElement)
        {
            return;
        }

        var amount = (double)RotationStep;
        RotateSelectedRequested?.Invoke(direction == "Counterclockwise" ? -amount : amount);
    }

    [RelayCommand]
    private void GroupSelected() => GroupSelectionRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void UngroupSelected() => UngroupSelectionRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void SetPrimaryElement() => SetPrimaryElementRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ShowAbout() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenElectrostaticSimulation() =>
        OpenElectrostaticSimulationRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void OpenMagnetostaticSimulation() =>
        OpenMagnetostaticSimulationRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ResetScene()
    {
        CancelRayDensityUpdate();
        RayDensity = 160;
        AppliedRaysPerSource = 160;
        CurrentScene = OpticalScene.CreateEmpty();
        ActiveTool = CanvasTool.Pan;
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = T("Status.SceneReset");
    }

    [RelayCommand]
    private void ResetView()
    {
        ResetViewRequested?.Invoke(this, EventArgs.Empty);
        StatusText = T("Status.ViewReset");
    }

    [RelayCommand]
    private async Task OpenSceneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var opened = await sceneStorage.OpenAsync(cancellationToken);
            if (opened is null)
            {
                return;
            }

            CurrentScene = opened.Scene;
            ActiveTool = CanvasTool.Pan;
            StatusText = F("Status.Opened", opened.FileName);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StatusText = F("Status.OpenFailed", exception.Message);
        }
    }

    [RelayCommand]
    private async Task SaveSceneAsync(CancellationToken cancellationToken)
    {
        try
        {
            var fileName = await sceneStorage.SaveAsync(CurrentScene, cancellationToken);
            if (fileName is not null)
            {
                StatusText = F("Status.Saved", fileName);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StatusText = F("Status.SaveFailed", exception.Message);
        }
    }

    public void UpdateSimulation(SimulationResult result) =>
        StatusText = F("Status.OpticalSimulation", CurrentScene.Name, AppliedRaysPerSource,
            result.InitialRayCount, result.Segments.Count, result.DiffractedRayCount,
            result.Elapsed.TotalMilliseconds);

    public void UpdateToolState(CanvasTool tool, bool isPlacing)
    {
        ActiveTool = tool;
        _isPlacing = isPlacing;
        StatusText = tool switch
        {
            CanvasTool.Pan => T("Status.Pan"),
            CanvasTool.Move => T("Status.OpticalMove"),
            CanvasTool.Delete => T("Status.OpticalDelete"),
            _ => PlacementStatus(tool, isPlacing)
        };
    }

    public void UpdateSelection(CanvasSelection? selection)
    {
        _selection = selection;
        _updatingSelection = true;
        try
        {
            HasSelectedElement = selection is not null;
            CanEditSelectedTransform = selection is not null && selection.Kind != CanvasSelectionKind.Multiple;
            CanGroupSelection = selection?.CanGroup == true;
            CanUngroupSelection = selection?.CanUngroup == true;
            CanSetPrimaryElement = selection?.CanSetPrimary == true;
            CanRenameSelectedElement = selection?.CanRename == true;
            HasSelectedLightSource = selection?.WavelengthNanometers is not null;
            HasSelectedPointLight = selection?.Kind == CanvasSelectionKind.PointLight;
            HasSelectedCentralAngle = selection?.ArcAngleDegrees is not null ||
                                      selection?.EmissionAngleDegrees is not null;
            CanRotateSelectedElement = selection?.CanRotate == true;
            HasSelectedLens = selection?.FocalLength is not null;
            HasSelectedThinLens = selection?.DispersionMode is not null;
            HasSelectedDispersiveLens = selection?.DispersionMode is not null and not LensDispersionMode.None;
            HasSelectedSphericalMirror = selection?.Kind is CanvasSelectionKind.ConcaveSphericalMirror
                or CanvasSelectionKind.ConvexSphericalMirror or CanvasSelectionKind.ConcaveGrating;
            HasSelectedSecondOrigin = selection?.SecondOriginX is not null && selection.SecondOriginY is not null;
            HasSelectedAperture = selection?.ApertureOpening is not null;
            HasSelectedReflectionGrating = selection?.GrooveDensity is not null;
            HasSelectedLength = selection?.Length is not null;
            CanTemporarilyHideSelectedElement = selection?.CanTemporarilyHide == true;
            IsSelectedElementTemporarilyHidden = selection?.IsTemporarilyHidden == true;
            SelectedElementName = SelectionDisplayName(selection);
            SelectedElementTitle = selection?.ElementName ?? string.Empty;
            SelectedAngleText = selection?.CanRotate == true
                ? F("Selection.Angle", selection.AngleDegrees)
                : T("Selection.AngleNone");
            SelectedStandardAngleIndex = selection?.CanRotate == true
                ? GetStandardAngleIndex(selection.AngleDegrees)
                : 0;
            if (selection is not null)
            {
                SelectedOriginX = (decimal)Math.Round(selection.OriginX, 2);
                SelectedOriginY = (decimal)Math.Round(selection.OriginY, 2);
            }
            if (selection?.SecondOriginX is { } secondOriginX &&
                selection.SecondOriginY is { } secondOriginY)
            {
                SelectedSecondOriginX = (decimal)Math.Round(secondOriginX, 2);
                SelectedSecondOriginY = (decimal)Math.Round(secondOriginY, 2);
            }
            if (selection?.FocalLength is { } focalLength)
            {
                SelectedFocalLength = (decimal)focalLength;
            }
            if (selection?.DispersionMode is { } dispersionMode)
            {
                SelectedLensDispersionModeIndex = (int)dispersionMode;
            }
            if (selection?.DispersionLevel is { } dispersionLevel)
            {
                SelectedLensDispersionLevel = dispersionLevel;
            }
            if (selection?.Radius is { } radius)
            {
                SelectedSphericalMirrorRadius = (decimal)Math.Round(radius, 2);
            }
            if (selection?.ArcAngleDegrees is { } arcAngle)
            {
                SelectedSphericalMirrorArcAngle = (decimal)Math.Round(arcAngle, 2);
                SelectedCentralAngle = (decimal)Math.Round(arcAngle, 2);
            }
            if (selection?.EmissionAngleDegrees is { } emissionAngle)
            {
                SelectedPointLightEmissionAngle = (decimal)Math.Round(emissionAngle, 2);
                SelectedCentralAngle = (decimal)Math.Round(emissionAngle, 2);
            }
            if (selection?.ApertureOpening is { } apertureOpening)
            {
                SelectedApertureOpening = (decimal)Math.Round(apertureOpening, 2);
            }
            if (selection?.GrooveDensity is { } grooveDensity)
            {
                SelectedGrooveDensity = (decimal)Math.Round(grooveDensity, 2);
            }
            if (selection?.WavelengthNanometers is { } wavelength)
            {
                SelectedWavelength = (decimal)Math.Round(wavelength, 2);
            }
            if (selection?.Length is { } length)
            {
                SelectedLength = (decimal)Math.Round(length, 2);
            }
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void ApplySelectedOrigin(decimal x, decimal y)
    {
        if (!_updatingSelection && HasSelectedElement)
        {
            SetSelectedOriginRequested?.Invoke((double)x, (double)y);
        }
    }

    private void ApplySelectedSecondOrigin(decimal x, decimal y)
    {
        if (!_updatingSelection && HasSelectedSecondOrigin)
        {
            SetSelectedSecondOriginRequested?.Invoke((double)x, (double)y);
        }
    }

    private static int GetStandardAngleIndex(double angleDegrees)
    {
        var normalized = angleDegrees % 360;
        if (normalized < 0)
        {
            normalized += 360;
        }

        var standardAngles = new[] { 0d, 90d, 180d, 270d };
        for (var index = 0; index < standardAngles.Length; index++)
        {
            if (Math.Abs(normalized - standardAngles[index]) <= 1e-6)
            {
                return index + 1;
            }
        }

        return 0;
    }

    private async Task ApplyRayDensityAsync(int count, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            AppliedRaysPerSource = count;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelRayDensityUpdate()
    {
        _rayDensityUpdate?.Cancel();
        _rayDensityUpdate?.Dispose();
        _rayDensityUpdate = null;
    }

    private static string PlacementStatus(CanvasTool tool, bool isPlacing)
    {
        var name = tool switch
        {
            CanvasTool.PointLight => T("Placement.PointLight"),
            CanvasTool.ParallelLight => T("Placement.ParallelLight"),
            CanvasTool.CompositePointLight => T("Placement.CompositePointLight"),
            CanvasTool.CompositeParallelLight => T("Placement.CompositeParallelLight"),
            CanvasTool.Mirror => T("Selection.Mirror"),
            CanvasTool.ConcaveSphericalMirror => T("Placement.ConcaveMirror"),
            CanvasTool.ConvexSphericalMirror => T("Placement.ConvexMirror"),
            CanvasTool.BeamSplitter => T("Placement.BeamSplitter"),
            CanvasTool.Screen => T("Selection.Screen"),
            CanvasTool.Aperture => T("Selection.Aperture"),
            CanvasTool.ReflectionGrating => T("Selection.ReflectionGrating"),
            CanvasTool.ConcaveGrating => T("Placement.ConcaveGrating"),
            CanvasTool.ConvexLens => T("Selection.ConvexLens"),
            CanvasTool.ConcaveLens => T("Selection.ConcaveLens"),
            _ => T("Placement.Object")
        };
        if (tool is CanvasTool.PointLight or CanvasTool.CompositePointLight)
        {
            return name;
        }
        if (tool is CanvasTool.ConcaveSphericalMirror or CanvasTool.ConvexSphericalMirror
            or CanvasTool.ConcaveGrating)
        {
            var sphericalMirrorName = tool switch
            {
                CanvasTool.ConcaveSphericalMirror => T("Selection.ConcaveMirror"),
                CanvasTool.ConvexSphericalMirror => T("Selection.ConvexMirror"),
                _ => T("Selection.ConcaveGrating")
            };
            return isPlacing
                ? F("Placement.DrawingSpherical", sphericalMirrorName)
                : F("Placement.StartSpherical", sphericalMirrorName);
        }
        return isPlacing ? F("Placement.Drawing", name) : F("Placement.Start", name);
    }

    private static string SelectionDisplayName(CanvasSelection? selection)
    {
        if (selection is null) return T("Selection.None");
        if (selection.Kind == CanvasSelectionKind.Group) return F("Selection.Group", selection.MemberCount);
        if (selection.Kind == CanvasSelectionKind.Multiple) return F("Selection.Multiple", selection.MemberCount);

        return selection.Kind switch
        {
            CanvasSelectionKind.PointLight => T(selection.WavelengthNanometers is null
                ? "Selection.CompositePointLight" : "Selection.PointLight"),
            CanvasSelectionKind.ParallelLight => T(selection.WavelengthNanometers is null
                ? "Selection.CompositeParallelLight" : "Selection.ParallelLight"),
            CanvasSelectionKind.Mirror => T("Selection.Mirror"),
            CanvasSelectionKind.ConcaveSphericalMirror => T("Selection.ConcaveMirror"),
            CanvasSelectionKind.ConvexSphericalMirror => T("Selection.ConvexMirror"),
            CanvasSelectionKind.BeamSplitter => T("Selection.BeamSplitter"),
            CanvasSelectionKind.Screen => T("Selection.Screen"),
            CanvasSelectionKind.Aperture => T("Selection.Aperture"),
            CanvasSelectionKind.ReflectionGrating => T("Selection.ReflectionGrating"),
            CanvasSelectionKind.ConcaveGrating => T("Selection.ConcaveGrating"),
            CanvasSelectionKind.ConvexLens => T("Selection.ConvexLens"),
            CanvasSelectionKind.ConcaveLens => T("Selection.ConcaveLens"),
            _ => selection.DisplayName
        };
    }

    private static string T(string key) => LocalizationService.Instance.Get(key);

    private static string F(string key, params object[] values) => string.Format(T(key), values);
}
