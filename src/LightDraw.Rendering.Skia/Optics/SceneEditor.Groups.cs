using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    public bool GroupSelection()
    {
        if (_selectedIds.Count < 2) return false;
        var members = _selectedIds.Where(id => SceneGeometry.Find(_scene, id) is not null)
            .Distinct().ToArray();
        if (members.Length < 2) return false;
        var primary = _activeElementId is { } active && members.Contains(active) ? active : members[0];
        var intersectingGroups = _scene.ElementGroups
            .Where(group => group.MemberIds.Any(members.Contains)).Select(group => group.Id).ToHashSet();
        var newGroup = new ElementGroup(Guid.NewGuid(), members, primary, NextElementName("Group"));
        UpdateScene(_scene with
        {
            Groups = [.. _scene.ElementGroups.Where(group => !intersectingGroups.Contains(group.Id)), newGroup]
        });
        _selectedGroupId = newGroup.Id;
        _activeElementId = primary;
        ClearLegacySelection();
        CommitSelectedEdit();
        return true;
    }

    public bool UngroupSelection()
    {
        if (_selectedGroupId is not { } groupId || FindGroup(groupId) is not { } group) return false;
        UpdateScene(_scene with { Groups = _scene.ElementGroups.Where(item => item.Id != groupId).ToArray() });
        _selectedGroupId = null;
        _selectedIds.Clear();
        _selectedIds.UnionWith(group.MemberIds);
        _activeElementId = group.PrimaryMemberId;
        ClearLegacySelection();
        CommitSelectedEdit();
        return true;
    }

    public bool SetActiveMemberAsPrimary()
    {
        if (_selectedGroupId is not { } groupId || _activeElementId is not { } active ||
            FindGroup(groupId) is not { } group || !group.MemberIds.Contains(active) ||
            group.PrimaryMemberId == active) return false;
        UpdateScene(_scene with
        {
            Groups = _scene.ElementGroups.Select(item => item.Id == groupId
                ? item with { PrimaryMemberId = active } : item).ToArray()
        });
        CommitSelectedEdit();
        return true;
    }

    private ElementGroup? FindGroup(Guid id) => _scene.ElementGroups.FirstOrDefault(group => group.Id == id);

    private ElementGroup? FindGroupContaining(Guid elementId) =>
        _scene.ElementGroups.FirstOrDefault(group => group.MemberIds.Contains(elementId));

    private Vector2D GroupRotationHandle(SceneItemRef primary) =>
        SceneGeometry.Origin(_scene, primary) +
        Vector2D.FromAngle(SceneGeometry.AngleDegrees(_scene, primary) * Math.PI / 180) * RotationHandleOffset;
}
