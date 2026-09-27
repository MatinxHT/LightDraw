using LightDraw.Core.Electromagnetics;

namespace LightDraw.Core.Scene;

public static class ElectromagneticSceneNormalizer
{
    public static ElectrostaticScene Normalize(ElectrostaticScene scene) => scene with
    {
        Charges = (scene.Charges ?? []).Select((item, index) => item with
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Point Charge {index + 1}" : item.Name.Trim()
        }).ToArray(),
        Plates = scene.PlateElements.Select((item, index) => item with
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Charged Plate {index + 1}" : item.Name.Trim()
        }).ToArray()
    };

    public static MagnetostaticScene Normalize(MagnetostaticScene scene) => scene with
    {
        Conductors = (scene.Conductors ?? []).Select((item, index) => item with
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Planar Conductor {index + 1}" : item.Name.Trim()
        }).ToArray(),
        VerticalConductors = scene.VerticalConductorElements.Select((item, index) => item with
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Vertical Conductor {index + 1}" : item.Name.Trim()
        }).ToArray(),
        PlanarLoops = scene.PlanarLoopElements.Select((item, index) => item with
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Planar Current Loop {index + 1}" : item.Name.Trim()
        }).ToArray(),
        VerticalLoops = scene.VerticalLoopElements.Select((item, index) => item with
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? $"Vertical Current Loop {index + 1}" : item.Name.Trim()
        }).ToArray()
    };

}
