using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

public enum CanvasTool
{
    Pan, Move, Delete, PointLight, ParallelLight, CompositePointLight, CompositeParallelLight, Mirror,
    ConcaveSphericalMirror, ConvexSphericalMirror, BeamSplitter,
    Screen, Aperture, ReflectionGrating, ConcaveGrating, ConvexLens, ConcaveLens
}

public enum CanvasSelectionKind
{
    PointLight, ParallelLight, Mirror, ConcaveSphericalMirror,
    ConvexSphericalMirror, BeamSplitter, Screen, Aperture,
    ReflectionGrating, ConcaveGrating, ConvexLens, ConcaveLens, Group, Multiple
}

public sealed record CanvasSelection(
    CanvasSelectionKind Kind, string DisplayName, bool CanRotate,
    double OriginX, double OriginY, double AngleDegrees,
    double? FocalLength, double? Length,
    double? ApertureOpening = null, double? GrooveDensity = null,
    double? Radius = null, double? ArcAngleDegrees = null, double? SecondOriginX = null,
    double? SecondOriginY = null, double? EmissionAngleDegrees = null,
    double? WavelengthNanometers = null, LensDispersionMode? DispersionMode = null,
    int? DispersionLevel = null,
    int MemberCount = 1, bool CanGroup = false, bool CanUngroup = false,
    bool CanSetPrimary = false, string? ElementName = null, bool CanRename = false,
    bool CanTemporarilyHide = false, bool IsTemporarilyHidden = false);

internal enum SceneItemKind
{
    None, LightSource, Mirror, ConcaveSphericalMirror, ConvexSphericalMirror,
    BeamSplitter, Screen, Aperture, ReflectionGrating, ConcaveGrating, Lens
}

internal enum MoveDragMode
{
    None, Translate, DirectionHandle, RotationHandle
}

