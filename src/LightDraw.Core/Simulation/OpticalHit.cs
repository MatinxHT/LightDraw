using LightDraw.Core.Scene;

namespace LightDraw.Core.Simulation;

internal enum OpticalHitKind
{
    Mirror,
    ConcaveSphericalMirror,
    ConvexSphericalMirror,
    Lens,
    Screen,
    Aperture,
    ReflectionGrating,
    BeamSplitter
}

internal readonly record struct OpticalHit(
    double Distance,
    OpticalHitKind Kind,
    object Element)
{
    public T GetElement<T>() where T : class =>
        Element as T ?? throw new InvalidOperationException(
            $"命中类型 {Kind} 不包含预期的 {typeof(T).Name} 元件。");
}
