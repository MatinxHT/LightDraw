using System.Diagnostics;
using LightDraw.Core.Geometry;
using LightDraw.Core.Scene;

namespace LightDraw.Rendering.Skia.Optics;

internal sealed partial class SceneEditor
{
    public bool TryBeginMove(Vector2D world)
    {
        if (TryBeginGroupInteraction(world, out var beganGroupMove))
        {
            return beganGroupMove;
        }

        var previouslySelectedKind = _selectedKind;
        var previouslySelectedIndex = _selectedIndex;
        _movingKind = SceneItemKind.None;
        _movingGroupId = null;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;

        var bestDistance = 12 / _zoom;
        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            if (source.Kind == LightSourceKind.Point)
            {
                SelectIfCloser((world - source.Position).Length,
                    SceneItemKind.LightSource, index, MoveDragMode.Translate, ref bestDistance);
                SelectIfCloser((world - PointLightRotationHandle(source)).Length,
                    SceneItemKind.LightSource, index, MoveDragMode.RotationHandle,
                    ref bestDistance);
                continue;
            }
            if (source.End is not { } end)
            {
                continue;
            }

            SelectIfCloser((world - (source.Position + end) / 2).Length,
                SceneItemKind.LightSource, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(source.Position, end)).Length,
                SceneItemKind.LightSource, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            SelectIfCloser((world - (mirror.Start + mirror.End) / 2).Length,
                SceneItemKind.Mirror, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(mirror.Start, mirror.End)).Length,
                SceneItemKind.Mirror, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConcaveSphericalMirrorElements[index];
            SelectIfCloser((world - mirror.Vertex).Length, SceneItemKind.ConcaveSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - mirror.CenterOfCurvature).Length,
                SceneItemKind.ConcaveSphericalMirror, index, MoveDragMode.DirectionHandle,
                ref bestDistance);
            SelectIfCloser((world - SphericalMirrorRotationHandle(
                    mirror.Vertex, mirror.CenterOfCurvature)).Length,
                SceneItemKind.ConcaveSphericalMirror, index, MoveDragMode.RotationHandle,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConvexSphericalMirrorElements[index];
            SelectIfCloser((world - mirror.Vertex).Length, SceneItemKind.ConvexSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - mirror.CenterOfCurvature).Length,
                SceneItemKind.ConvexSphericalMirror, index, MoveDragMode.DirectionHandle,
                ref bestDistance);
            SelectIfCloser((world - SphericalMirrorRotationHandle(
                    mirror.Vertex, mirror.CenterOfCurvature)).Length,
                SceneItemKind.ConvexSphericalMirror, index, MoveDragMode.RotationHandle,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = _scene.BeamSplitterElements[index];
            SelectIfCloser((world - (beamSplitter.Start + beamSplitter.End) / 2).Length,
                SceneItemKind.BeamSplitter, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(beamSplitter.Start, beamSplitter.End)).Length,
                SceneItemKind.BeamSplitter, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ScreenElements.Length; index++)
        {
            var screen = _scene.ScreenElements[index];
            SelectIfCloser((world - (screen.Start + screen.End) / 2).Length,
                SceneItemKind.Screen, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(screen.Start, screen.End)).Length,
                SceneItemKind.Screen, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ApertureElements.Length; index++)
        {
            var aperture = _scene.ApertureElements[index];
            SelectIfCloser((world - (aperture.Start + aperture.End) / 2).Length,
                SceneItemKind.Aperture, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(aperture.Start, aperture.End)).Length,
                SceneItemKind.Aperture, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.ReflectionGratingElements.Length; index++)
        {
            var grating = _scene.ReflectionGratingElements[index];
            SelectIfCloser((world - (grating.Start + grating.End) / 2).Length,
                SceneItemKind.ReflectionGrating, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(grating.Start, grating.End)).Length,
                SceneItemKind.ReflectionGrating, index, MoveDragMode.RotationHandle,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.ConcaveGratingElements.Length; index++)
        {
            var grating = _scene.ConcaveGratingElements[index];
            SelectIfCloser((world - grating.Vertex).Length, SceneItemKind.ConcaveGrating,
                index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - grating.CenterOfCurvature).Length,
                SceneItemKind.ConcaveGrating, index, MoveDragMode.DirectionHandle, ref bestDistance);
            SelectIfCloser((world - SphericalMirrorRotationHandle(
                    grating.Vertex, grating.CenterOfCurvature)).Length,
                SceneItemKind.ConcaveGrating, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            SelectIfCloser((world - (lens.Start + lens.End) / 2).Length,
                SceneItemKind.Lens, index, MoveDragMode.Translate, ref bestDistance);
            SelectIfCloser((world - RotationHandle(lens.Start, lens.End)).Length,
                SceneItemKind.Lens, index, MoveDragMode.RotationHandle, ref bestDistance);
        }

        if (_movingKind == SceneItemKind.None)
        {
            bestDistance = 10 / _zoom;
            FindTranslatableItem(world, ref bestDistance);
            if (_movingKind != SceneItemKind.None)
            {
                _selectedKind = _movingKind;
                _selectedIndex = _movingIndex;
                SelectSingleLegacyItem();
                _movingKind = SceneItemKind.None;
                _movingIndex = -1;
                _moveDragMode = MoveDragMode.None;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }

        if (_movingKind == SceneItemKind.None)
        {
            ClearSelection();
            return false;
        }

        if (_movingKind != previouslySelectedKind || _movingIndex != previouslySelectedIndex)
        {
            _selectedKind = _movingKind;
            _selectedIndex = _movingIndex;
            SelectSingleLegacyItem();
            _movingKind = SceneItemKind.None;
            _movingIndex = -1;
            _moveDragMode = MoveDragMode.None;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }

        _selectedKind = _movingKind;
        _selectedIndex = _movingIndex;
        SelectSingleLegacyItem();
        _lastMoveWorld = world;
        _moveChanged = false;
        _moveSimulationDirty = false;
        _lastMoveSimulationTimestamp = 0;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InteractionStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool EndMove()
    {
        if (!IsMoving) return false;
        _movingKind = SceneItemKind.None;
        _movingGroupId = null;
        _moveDragMode = MoveDragMode.None;
        _movingIndex = -1;
        var changed = _moveChanged;
        _moveChanged = false;
        if (changed)
        {
            if (_moveSimulationDirty)
            {
                PreviewRequested?.Invoke(this, EventArgs.Empty);
                _moveSimulationDirty = false;
            }
            SceneCommitted?.Invoke(this, EventArgs.Empty);
        }
        InteractionStateChanged?.Invoke(this, EventArgs.Empty);
        return changed;
    }

    private bool TryBeginGroupInteraction(Vector2D world, out bool beganMove)
    {
        beganMove = false;
        var tolerance = 12 / _zoom;
        if (_selectedGroupId is { } selectedGroupId && FindGroup(selectedGroupId) is { } selectedGroup &&
            SceneGeometry.Find(_scene, selectedGroup.PrimaryMemberId) is { } primary)
        {
            var origin = SceneGeometry.Origin(_scene, primary);
            var handle = GroupRotationHandle(primary);
            if ((world - origin).Length <= tolerance || (world - handle).Length <= tolerance)
            {
                _movingGroupId = selectedGroupId;
                _moveDragMode = (world - handle).Length <= tolerance
                    ? MoveDragMode.RotationHandle : MoveDragMode.Translate;
                _lastMoveWorld = world;
                _moveChanged = false;
                _moveSimulationDirty = false;
                _lastMoveSimulationTimestamp = 0;
                beganMove = true;
                InteractionStateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }

        if (FindItemAt(world) is not { } hit || FindGroupContaining(hit.Id) is not { } group)
            return false;

        _selectedIds.Clear();
        _selectedIds.UnionWith(group.MemberIds);
        _selectedGroupId = group.Id;
        _activeElementId = hit.Id;
        ClearLegacySelection();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void FindTranslatableItem(Vector2D world, ref double bestDistance)
    {

        for (var index = 0; index < _scene.LightSources.Length; index++)
        {
            var source = _scene.LightSources[index];
            var distance = source.Kind == LightSourceKind.ParallelLine && source.End is { } end
                ? DistanceToSegment(world, source.Position, end)
                : (world - source.Position).Length;
            SelectIfCloser(distance, SceneItemKind.LightSource, index, MoveDragMode.Translate,
                ref bestDistance);
        }

        for (var index = 0; index < _scene.Mirrors.Length; index++)
        {
            var mirror = _scene.Mirrors[index];
            SelectIfCloser(DistanceToSegment(world, mirror.Start, mirror.End), SceneItemKind.Mirror,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ConcaveSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConcaveSphericalMirrorElements[index];
            SelectIfCloser(DistanceToArc(world, mirror), SceneItemKind.ConcaveSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ConvexSphericalMirrorElements.Length; index++)
        {
            var mirror = _scene.ConvexSphericalMirrorElements[index];
            SelectIfCloser(DistanceToArc(world, mirror.Vertex, mirror.CenterOfCurvature,
                    mirror.ArcAngleDegrees), SceneItemKind.ConvexSphericalMirror,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.BeamSplitterElements.Length; index++)
        {
            var beamSplitter = _scene.BeamSplitterElements[index];
            SelectIfCloser(DistanceToSegment(world, beamSplitter.Start, beamSplitter.End),
                SceneItemKind.BeamSplitter, index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ScreenElements.Length; index++)
        {
            var screen = _scene.ScreenElements[index];
            SelectIfCloser(DistanceToSegment(world, screen.Start, screen.End), SceneItemKind.Screen,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ApertureElements.Length; index++)
        {
            var aperture = _scene.ApertureElements[index];
            SelectIfCloser(DistanceToSegment(world, aperture.Start, aperture.End), SceneItemKind.Aperture,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ReflectionGratingElements.Length; index++)
        {
            var grating = _scene.ReflectionGratingElements[index];
            SelectIfCloser(DistanceToSegment(world, grating.Start, grating.End),
                SceneItemKind.ReflectionGrating, index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.ConcaveGratingElements.Length; index++)
        {
            var grating = _scene.ConcaveGratingElements[index];
            SelectIfCloser(DistanceToArc(world, grating.Vertex, grating.CenterOfCurvature,
                    grating.ArcAngleDegrees), SceneItemKind.ConcaveGrating,
                index, MoveDragMode.Translate, ref bestDistance);
        }

        for (var index = 0; index < _scene.LensElements.Length; index++)
        {
            var lens = _scene.LensElements[index];
            SelectIfCloser(DistanceToSegment(world, lens.Start, lens.End), SceneItemKind.Lens,
                index, MoveDragMode.Translate, ref bestDistance);
        }
    }

    private void SelectIfCloser(double distance, SceneItemKind kind, int index, MoveDragMode dragMode,
        ref double bestDistance)
    {
        if (distance > bestDistance)
        {
            return;
        }

        bestDistance = distance;
        _movingKind = kind;
        _movingIndex = index;
        _moveDragMode = dragMode;
    }

    public void MoveSelectedItem(Vector2D world)
    {
        if (_movingGroupId is { } movingGroupId && FindGroup(movingGroupId) is { } movingGroup &&
            SceneGeometry.Find(_scene, movingGroup.PrimaryMemberId) is { } primary)
        {
            var memberIds = movingGroup.MemberIds.ToHashSet();
            if (_moveDragMode == MoveDragMode.Translate)
            {
                var groupDelta = world - _lastMoveWorld;
                if (groupDelta.LengthSquared <= 1e-12) return;
                _scene = SceneGeometry.Translate(_scene, memberIds, groupDelta);
            }
            else
            {
                var pivot = SceneGeometry.Origin(_scene, primary);
                var direction = world - pivot;
                if (direction.LengthSquared <= 1e-12) return;
                var targetAngle = DirectionDegrees(direction);
                var currentAngle = SceneGeometry.AngleDegrees(_scene, primary);
                var deltaDegrees = NormalizeSignedDegrees(targetAngle - currentAngle);
                if (Math.Abs(deltaDegrees) <= 1e-9) return;
                _scene = SceneGeometry.Rotate(_scene, memberIds, pivot, deltaDegrees * Math.PI / 180);
            }
            _lastMoveWorld = world;
            SceneUpdated?.Invoke(this, EventArgs.Empty);
            _moveChanged = true;
            _moveSimulationDirty = true;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RecalculateDuringMoveIfDue();
            return;
        }

        var delta = world - _lastMoveWorld;
        if (delta.LengthSquared <= 1e-12)
        {
            return;
        }

        _lastMoveWorld = world;
        var updated = false;
        switch (_movingKind)
        {
            case SceneItemKind.LightSource:
                var sources = (LightSource[])_scene.LightSources.Clone();
                var source = sources[_movingIndex];
                if (_moveDragMode == MoveDragMode.RotationHandle && source.Kind == LightSourceKind.Point)
                {
                    var direction = (world - source.Position).Normalized();
                    if (direction.LengthSquared <= 1e-12)
                    {
                        return;
                    }
                    sources[_movingIndex] = source with
                    {
                        DirectionDegrees = DirectionDegrees(direction)
                    };
                }
                else if (_moveDragMode == MoveDragMode.Translate || source.End is null)
                {
                    sources[_movingIndex] = source with
                    {
                        Position = source.Position + delta,
                        End = source.End is { } translatedEnd ? translatedEnd + delta : null
                    };
                }
                else
                {
                    var start = source.Position;
                    var end = source.End.Value;
                    if (_moveDragMode == MoveDragMode.RotationHandle &&
                        !TryRotateSegmentFromHandle(source.Position, source.End.Value, world,
                            out start, out end))
                    {
                        return;
                    }
                    if (!HasUsableLength(start, end))
                    {
                        return;
                    }
                    var direction = (end - start).Normalized().Perpendicular();
                    sources[_movingIndex] = source with
                    {
                        Position = start,
                        End = end,
                        DirectionDegrees = DirectionDegrees(direction)
                    };
                }
                UpdateScene(_scene with { LightSources = sources });
                updated = true;
                break;
            case SceneItemKind.Mirror:
                var mirrors = (MirrorSegment[])_scene.Mirrors.Clone();
                var mirror = mirrors[_movingIndex];
                var mirrorStart = mirror.Start;
                var mirrorEnd = mirror.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    mirrorStart += delta;
                    mirrorEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(mirror.Start, mirror.End, world,
                             out mirrorStart, out mirrorEnd))
                {
                    return;
                }
                if (!HasUsableLength(mirrorStart, mirrorEnd))
                {
                    return;
                }
                mirrors[_movingIndex] = mirror with { Start = mirrorStart, End = mirrorEnd };
                UpdateScene(_scene with { Mirrors = mirrors });
                updated = true;
                break;
            case SceneItemKind.ConcaveSphericalMirror:
                var sphericalMirrors = (ConcaveSphericalMirror[])_scene.ConcaveSphericalMirrorElements.Clone();
                var sphericalMirror = sphericalMirrors[_movingIndex];
                if (_moveDragMode is MoveDragMode.DirectionHandle or MoveDragMode.RotationHandle)
                {
                    var direction = (world - sphericalMirror.Vertex).Normalized();
                    if (direction.LengthSquared <= 1e-12)
                    {
                        return;
                    }
                    sphericalMirrors[_movingIndex] = sphericalMirror with
                    {
                        CenterOfCurvature = sphericalMirror.Vertex + direction * sphericalMirror.Radius
                    };
                }
                else
                {
                    sphericalMirrors[_movingIndex] = sphericalMirror with
                    {
                        Vertex = sphericalMirror.Vertex + delta,
                        CenterOfCurvature = sphericalMirror.CenterOfCurvature + delta
                    };
                }
                UpdateScene(_scene with { ConcaveSphericalMirrors = sphericalMirrors });
                updated = true;
                break;
            case SceneItemKind.ConvexSphericalMirror:
                var convexSphericalMirrors = (ConvexSphericalMirror[])_scene.ConvexSphericalMirrorElements.Clone();
                var convexSphericalMirror = convexSphericalMirrors[_movingIndex];
                if (_moveDragMode is MoveDragMode.DirectionHandle or MoveDragMode.RotationHandle)
                {
                    var direction = (world - convexSphericalMirror.Vertex).Normalized();
                    if (direction.LengthSquared <= 1e-12)
                    {
                        return;
                    }
                    convexSphericalMirrors[_movingIndex] = convexSphericalMirror with
                    {
                        CenterOfCurvature = convexSphericalMirror.Vertex +
                                            direction * convexSphericalMirror.Radius
                    };
                }
                else
                {
                    convexSphericalMirrors[_movingIndex] = convexSphericalMirror with
                    {
                        Vertex = convexSphericalMirror.Vertex + delta,
                        CenterOfCurvature = convexSphericalMirror.CenterOfCurvature + delta
                    };
                }
                UpdateScene(_scene with { ConvexSphericalMirrors = convexSphericalMirrors });
                updated = true;
                break;
            case SceneItemKind.BeamSplitter:
                var beamSplitters = (BeamSplitterSegment[])_scene.BeamSplitterElements.Clone();
                var beamSplitter = beamSplitters[_movingIndex];
                var beamSplitterStart = beamSplitter.Start;
                var beamSplitterEnd = beamSplitter.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    beamSplitterStart += delta;
                    beamSplitterEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(beamSplitter.Start, beamSplitter.End, world,
                             out beamSplitterStart, out beamSplitterEnd))
                {
                    return;
                }
                if (!HasUsableLength(beamSplitterStart, beamSplitterEnd))
                {
                    return;
                }
                beamSplitters[_movingIndex] = beamSplitter with
                {
                    Start = beamSplitterStart,
                    End = beamSplitterEnd
                };
                UpdateScene(_scene with { BeamSplitters = beamSplitters });
                updated = true;
                break;
            case SceneItemKind.Screen:
                var screens = (ScreenSegment[])_scene.ScreenElements.Clone();
                var screen = screens[_movingIndex];
                var screenStart = screen.Start;
                var screenEnd = screen.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    screenStart += delta;
                    screenEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(screen.Start, screen.End, world,
                             out screenStart, out screenEnd))
                {
                    return;
                }
                if (!HasUsableLength(screenStart, screenEnd))
                {
                    return;
                }
                screens[_movingIndex] = screen with { Start = screenStart, End = screenEnd };
                UpdateScene(_scene with { Screens = screens });
                updated = true;
                break;
            case SceneItemKind.Aperture:
                var apertures = (ApertureSegment[])_scene.ApertureElements.Clone();
                var aperture = apertures[_movingIndex];
                var apertureStart = aperture.Start;
                var apertureEnd = aperture.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    apertureStart += delta;
                    apertureEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(aperture.Start, aperture.End, world,
                             out apertureStart, out apertureEnd))
                {
                    return;
                }
                if (!HasUsableLength(apertureStart, apertureEnd))
                {
                    return;
                }
                var apertureLength = (apertureEnd - apertureStart).Length;
                apertures[_movingIndex] = aperture with
                {
                    Start = apertureStart,
                    End = apertureEnd,
                    OpeningSize = Math.Min(aperture.OpeningSize, apertureLength)
                };
                UpdateScene(_scene with { Apertures = apertures });
                updated = true;
                break;
            case SceneItemKind.ReflectionGrating:
                var gratings = (ReflectionGratingSegment[])_scene.ReflectionGratingElements.Clone();
                var grating = gratings[_movingIndex];
                var gratingStart = grating.Start;
                var gratingEnd = grating.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    gratingStart += delta;
                    gratingEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(grating.Start, grating.End, world,
                             out gratingStart, out gratingEnd))
                {
                    return;
                }
                if (!HasUsableLength(gratingStart, gratingEnd))
                {
                    return;
                }
                gratings[_movingIndex] = grating with { Start = gratingStart, End = gratingEnd };
                UpdateScene(_scene with { ReflectionGratings = gratings });
                updated = true;
                break;
            case SceneItemKind.ConcaveGrating:
                var concaveGratings = (ConcaveGrating[])_scene.ConcaveGratingElements.Clone();
                var concaveGrating = concaveGratings[_movingIndex];
                if (_moveDragMode is MoveDragMode.DirectionHandle or MoveDragMode.RotationHandle)
                {
                    var direction = (world - concaveGrating.Vertex).Normalized();
                    if (direction.LengthSquared <= 1e-12) return;
                    concaveGratings[_movingIndex] = concaveGrating with
                    {
                        CenterOfCurvature = concaveGrating.Vertex + direction * concaveGrating.Radius
                    };
                }
                else
                {
                    concaveGratings[_movingIndex] = concaveGrating with
                    {
                        Vertex = concaveGrating.Vertex + delta,
                        CenterOfCurvature = concaveGrating.CenterOfCurvature + delta
                    };
                }
                UpdateScene(_scene with { ConcaveGratings = concaveGratings });
                updated = true;
                break;
            case SceneItemKind.Lens:
                var lenses = (LensSegment[])_scene.LensElements.Clone();
                var lens = lenses[_movingIndex];
                var lensStart = lens.Start;
                var lensEnd = lens.End;
                if (_moveDragMode == MoveDragMode.Translate)
                {
                    lensStart += delta;
                    lensEnd += delta;
                }
                else if (_moveDragMode == MoveDragMode.RotationHandle &&
                         !TryRotateSegmentFromHandle(lens.Start, lens.End, world,
                             out lensStart, out lensEnd))
                {
                    return;
                }
                if (!HasUsableLength(lensStart, lensEnd))
                {
                    return;
                }
                lenses[_movingIndex] = lens with { Start = lensStart, End = lensEnd };
                UpdateScene(_scene with { Lenses = lenses });
                updated = true;
                break;
        }

        if (!updated)
        {
            return;
        }

        _moveChanged = true;
        _moveSimulationDirty = true;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        RecalculateDuringMoveIfDue();
    }

    private void RecalculateDuringMoveIfDue()
    {
        var now = Stopwatch.GetTimestamp();
        const int refreshMilliseconds = 33;
        var elapsedMilliseconds = _lastMoveSimulationTimestamp == 0
            ? double.PositiveInfinity
            : (now - _lastMoveSimulationTimestamp) * 1000d / Stopwatch.Frequency;
        if (elapsedMilliseconds < refreshMilliseconds)
        {
            return;
        }

        _lastMoveSimulationTimestamp = now;
        _moveSimulationDirty = false;
        PreviewRequested?.Invoke(this, EventArgs.Empty);
    }
}
