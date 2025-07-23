using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using mymcp.Core;

namespace mymcp.Analysis;

public class MEPPathfinder
{
    private readonly SpaceAnalyzer _spaceAnalyzer;
    private readonly RouteCalculator _routeCalculator;
    private readonly Document _document;

    public MEPPathfinder(SpaceAnalyzer spaceAnalyzer, RouteCalculator routeCalculator, Document document)
    {
        _spaceAnalyzer = spaceAnalyzer;
        _routeCalculator = routeCalculator;
        _document = document;
    }

    /// <summary>
    /// Находит оптимальный путь для воздуховода
    /// </summary>
    public MEPRouteResult FindDuctRoute(XYZ start, XYZ end, double ductWidth, double ductHeight, XYZ turnPoint = null, Level level = null)
    {
        try
        {
            Logger.Info($"Finding duct route from {start} to {end}, size: {ductWidth}x{ductHeight}");
            
            var clearance = Math.Max(ductWidth, ductHeight) * 0.5 + 0.3; // Зазор = половина максимального размера + 0.3 фута
            
            // Корректируем точки по высоте для воздуховодов
            var adjustedStart = AdjustPointForDuct(start, level);
            var adjustedEnd = AdjustPointForDuct(end, level);

            // Создаем базовый путь
            var basePath = new List<XYZ> { adjustedStart };

            // Добавляем промежуточную точку поворота, если указана
            if (turnPoint != null)
            {
                var adjustedTurnPoint = AdjustPointForDuct(turnPoint, level);
                basePath.Add(adjustedTurnPoint);
                Logger.Info($"Added turn point: {adjustedTurnPoint}");
            }

            // Добавляем конечную точку
            basePath.Add(adjustedEnd);

            Logger.Info($"Base path contains {basePath.Count} points");
            
            // Логируем все точки базового маршрута
            for (int i = 0; i < basePath.Count; i++)
            {
                Logger.Debug($"Base path point {i}: {basePath[i]}");
            }

            // Оптимизируем путь для воздуховодов (предпочитаем горизонтальные и вертикальные участки)
            var optimizedPath = OptimizeForDuct(basePath, ductWidth, ductHeight);
            
            Logger.Info($"Optimized path contains {optimizedPath.Count} points");
            
            // Логируем все точки оптимизированного маршрута
            for (int i = 0; i < optimizedPath.Count; i++)
            {
                Logger.Debug($"Optimized path point {i}: {optimizedPath[i]}");
            }
            
            // Добавляем фитинги
            var pathWithFittings = AddDuctFittings(optimizedPath, ductWidth, ductHeight);

            // Валидация маршрута (временно отключена для отладки)
            ValidateRoutePath(pathWithFittings.Path, adjustedStart, adjustedEnd);

            return new MEPRouteResult
            {
                Success = true,
                Path = pathWithFittings.Path,
                Fittings = pathWithFittings.Fittings,
                TotalLength = CalculatePathLength(pathWithFittings.Path),
                Message = $"Duct route found with {pathWithFittings.Fittings.Count} fittings, {pathWithFittings.Path.Count} path points"
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding duct route", ex);
            return new MEPRouteResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public MEPRouteResult FindPipeRoute(XYZ start, XYZ end, double pipeDiameter, Level level = null, PipeSystemType systemType = default)
    {
        try
        {
            Logger.Info($"Finding pipe route from {start} to {end}, diameter: {pipeDiameter}");

            var clearance = pipeDiameter * 0.5 + 0.2;
            
            var adjustedStart = AdjustPointForPipe(start, level, systemType);
            var adjustedEnd = AdjustPointForPipe(end, level, systemType);

            var basePath = _routeCalculator.FindOptimalPath(adjustedStart, adjustedEnd, clearance);

            if (!basePath.Any())
            {
                return new MEPRouteResult
                {
                    Success = false,
                    Message = "No valid path found for pipe"
                };
            }

            var optimizedPath = OptimizeForPipe(basePath, pipeDiameter, systemType);
            
            var pathWithFittings = AddPipeFittings(optimizedPath, pipeDiameter);

            return new MEPRouteResult
            {
                Success = true,
                Path = pathWithFittings.Path,
                Fittings = pathWithFittings.Fittings,
                TotalLength = CalculatePathLength(pathWithFittings.Path),
                Message = $"Pipe route found with {pathWithFittings.Fittings.Count} fittings"
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding pipe route", ex);
            return new MEPRouteResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private XYZ AdjustPointForDuct(XYZ point, Level level)
    {
        if (level == null)
        {
            level = _spaceAnalyzer.FindNearestLevel(point.Z);
        }

        if (level != null)
        {
            var ceilingHeight = level.Elevation + 12.0;
            return new XYZ(point.X, point.Y, ceilingHeight);
        }

        return point;
    }

    private XYZ AdjustPointForPipe(XYZ point, Level level, PipeSystemType systemType)
    {
        if (level == null)
        {
            level = _spaceAnalyzer.FindNearestLevel(point.Z);
        }

        if (level != null)
        {
            double height;
            
            if (systemType != null)
            {
                height = level.Elevation + 8.0;
            }
            else
            {
                height = level.Elevation + 8.0;
            }
            
            return new XYZ(point.X, point.Y, height);
        }

        return point;
    }

    private List<XYZ> OptimizeForDuct(List<XYZ> path, double ductWidth, double ductHeight)
    {
        if (path.Count <= 2) 
        {
            Logger.Debug("Path has 2 or fewer points, no optimization needed");
            return path;
        }

        Logger.Debug($"Optimizing path with {path.Count} points");
        
        var optimized = new List<XYZ> { path[0] };
        Logger.Debug($"Starting optimization from point: {path[0]}");

        for (int i = 1; i < path.Count; i++)
        {
            var currentPoint = path[i];
            var previousPoint = optimized.Last();
            
            Logger.Debug($"Processing point {i}: {currentPoint}");
            
            if (i == path.Count - 1 || currentPoint.IsAlmostEqualTo(previousPoint))
            {
                optimized.Add(currentPoint);
                Logger.Debug($"Added final or duplicate point: {currentPoint}");
                continue;
            }
            
            var rectangularSegment = CreateRectangularPath(previousPoint, currentPoint);
            
            Logger.Debug($"Created rectangular segment with {rectangularSegment.Count} points");
            
            for (int j = 1; j < rectangularSegment.Count; j++)
            {
                optimized.Add(rectangularSegment[j]);
                Logger.Debug($"Added rectangular point {j}: {rectangularSegment[j]}");
            }
        }

        Logger.Debug($"Optimization completed. Result has {optimized.Count} points");
        
        if (!optimized[0].IsAlmostEqualTo(path[0]))
        {
            Logger.Warning("Start point mismatch in optimization");
        }
        
        if (!optimized.Last().IsAlmostEqualTo(path.Last()))
        {
            Logger.Warning("End point mismatch in optimization");
        }
        
        return optimized;
    }

    private List<XYZ> OptimizeForPipe(List<XYZ> path, double diameter, PipeSystemType systemType)
    {
        if (path.Count <= 2) return path;

        var optimized = new List<XYZ>();
        
        for (int i = 0; i < path.Count - 1; i++)
        {
            optimized.Add(path[i]);
            
            if (i < path.Count - 2)
            {
                var angle = CalculateAngle(path[i], path[i + 1], path[i + 2]);
                if (angle < Math.PI / 3)
                {
                    var intermediate = CreateSmoothTransition(path[i], path[i + 1], path[i + 2]);
                    optimized.AddRange(intermediate);
                }
            }
        }
        
        optimized.Add(path.Last());
        return optimized;
    }

    private List<XYZ> CreateRectangularPath(XYZ start, XYZ end)
    {
        Logger.Debug($"Creating rectangular path from {start} to {end}");
        
        var path = new List<XYZ> { start };

        if (start.IsAlmostEqualTo(end))
        {
            Logger.Debug("Start and end points are the same");
            return new List<XYZ> { start };
        }

        var deltaX = Math.Abs(end.X - start.X);
        var deltaY = Math.Abs(end.Y - start.Y);
        var deltaZ = Math.Abs(end.Z - start.Z);
        
        Logger.Debug($"Deltas - X: {deltaX:F3}, Y: {deltaY:F3}, Z: {deltaZ:F3}");

        const double tolerance = 0.01;
        
        if (deltaX > tolerance && deltaY > tolerance && deltaZ > tolerance)
        {
            if (deltaX >= deltaY && deltaX >= deltaZ)
            {
                path.Add(new XYZ(end.X, start.Y, start.Z));
                path.Add(new XYZ(end.X, end.Y, start.Z));
            }
            else if (deltaY >= deltaZ)
            {
                path.Add(new XYZ(start.X, end.Y, start.Z));
                path.Add(new XYZ(end.X, end.Y, start.Z));
            }
            else
            {
                path.Add(new XYZ(start.X, start.Y, end.Z));
                path.Add(new XYZ(end.X, start.Y, end.Z));
            }
        }
        else if (deltaX > tolerance && deltaY > tolerance)
        {
            if (deltaX > deltaY)
            {
                path.Add(new XYZ(end.X, start.Y, start.Z));
            }
            else
            {
                path.Add(new XYZ(start.X, end.Y, start.Z));
            }
        }
        else if (deltaX > tolerance && deltaZ > tolerance)
        {
            if (deltaX > deltaZ)
            {
                path.Add(new XYZ(end.X, start.Y, start.Z));
            }
            else
            {
                path.Add(new XYZ(start.X, start.Y, end.Z));
            }
        }
        else if (deltaY > tolerance && deltaZ > tolerance)
        {
            if (deltaY > deltaZ)
            {
                path.Add(new XYZ(start.X, end.Y, start.Z));
            }
            else
            {
                path.Add(new XYZ(start.X, start.Y, end.Z));
            }
        }

        path.Add(end);
        
        Logger.Debug($"Rectangular path created with {path.Count} points");
        for (int i = 0; i < path.Count; i++)
        {
            Logger.Debug($"  Point {i}: {path[i]}");
        }
        
        return path;
    }

    private List<XYZ> CreateSmoothTransition(XYZ p1, XYZ p2, XYZ p3)
    {
        var transition = new List<XYZ>();
        
        var dir1 = (p2 - p1).Normalize();
        var dir2 = (p3 - p2).Normalize();
        
        var offset = Math.Min(p1.DistanceTo(p2), p2.DistanceTo(p3)) * 0.2;
        
        var transitionStart = p2 - (dir1 * offset);
        var transitionEnd = p2 + (dir2 * offset);
        
        transition.Add(transitionStart);
        transition.Add(transitionEnd);
        
        return transition;
    }

    private PathWithFittings AddDuctFittings(List<XYZ> path, double width, double height)
    {
        var fittings = new List<MEPFitting>();
        
        for (int i = 1; i < path.Count - 1; i++)
        {
            var fitting = new MEPFitting
            {
                Position = path[i],
                Type = DetermineDuctFittingType(path[i - 1], path[i], path[i + 1]),
                Size = $"{width}x{height}"
            };
            fittings.Add(fitting);
        }

        return new PathWithFittings
        {
            Path = path,
            Fittings = fittings
        };
    }

    private PathWithFittings AddPipeFittings(List<XYZ> path, double diameter)
    {
        var fittings = new List<MEPFitting>();
        
        for (int i = 1; i < path.Count - 1; i++)
        {
            var fitting = new MEPFitting
            {
                Position = path[i],
                Type = DeterminePipeFittingType(path[i - 1], path[i], path[i + 1]),
                Size = diameter.ToString("F2")
            };
            fittings.Add(fitting);
        }

        return new PathWithFittings
        {
            Path = path,
            Fittings = fittings
        };
    }

    private string DetermineDuctFittingType(XYZ p1, XYZ p2, XYZ p3)
    {
        var angle = CalculateAngle(p1, p2, p3);
        
        if (angle > Math.PI * 0.9) return "Straight";
        if (angle > Math.PI * 0.7) return "Elbow30";
        if (angle > Math.PI * 0.4) return "Elbow60";
        
        return "Elbow90";
    }

    private string DeterminePipeFittingType(XYZ p1, XYZ p2, XYZ p3)
    {
        var angle = CalculateAngle(p1, p2, p3);
        
        if (angle > Math.PI * 0.9) return "Straight";
        if (angle > Math.PI * 0.7) return "Elbow45";
        
        return "Elbow90";
    }

    private double CalculateAngle(XYZ p1, XYZ p2, XYZ p3)
    {
        var v1 = (p1 - p2).Normalize();
        var v2 = (p3 - p2).Normalize();
        
        var dot = v1.DotProduct(v2);
        return Math.Acos(Math.Max(-1, Math.Min(1, dot)));
    }

    private double CalculatePathLength(List<XYZ> path)
    {
        double length = 0;
        for (int i = 1; i < path.Count; i++)
        {
            length += path[i - 1].DistanceTo(path[i]);
        }
        return length;
    }

    private Level GetLevel(XYZ point)
    {
        return new FilteredElementCollector(_document)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => Math.Abs(l.Elevation - point.Z))
            .FirstOrDefault();
    }

    private MEPSystemType FindMechanicalSystemType()
    {
        return new FilteredElementCollector(_document)
            .OfClass(typeof(MEPSystemType))
            .Cast<MEPSystemType>()
            .FirstOrDefault(st => st is MechanicalSystemType);
    }

    private DuctType GetDuctType(Document doc, string preferredTypeName = null)
    {
        Logger.Info("Finding duct type");
        
        // Сначала пробуем найти предпочтительный тип
        if (!string.IsNullOrEmpty(preferredTypeName))
        {
            var preferredType = new FilteredElementCollector(doc)
                .OfClass(typeof(DuctType))
                .Cast<DuctType>()
                .FirstOrDefault(dt => dt.Name.Contains(preferredTypeName));
            
            if (preferredType != null)
            {
                Logger.Info($"Found preferred duct type: {preferredType.Name}");
                return preferredType;
            }
        }
        
        // Если предпочтительный не найден, берем любой доступный
        var ductType = new FilteredElementCollector(doc)
            .OfClass(typeof(DuctType))
            .Cast<DuctType>()
            .FirstOrDefault();
        
        if (ductType != null)
        {
            Logger.Info($"Found available duct type: {ductType.Name}");
            return ductType;
        }
        
        Logger.Error("No duct types found in document");
        return null;
    }

    public SmartCreationResult CreateDuctRoute(MEPRouteResult routeResult, string ductTypeName = "Rect_CommonSteel_Tap")
    {
        Logger.Info($"CreateDuctRoute called with ductTypeName: {ductTypeName}");
        
        if (routeResult == null || !routeResult.Success || routeResult.Path.Count < 2)
        {
            Logger.Error($"Invalid route result - Success: {routeResult?.Success}, Path count: {routeResult?.Path?.Count}");
            return new SmartCreationResult
            {
                Success = false,
                ElementsCreated = 0,
                Message = "Invalid route result"
            };
        }

        try
        {
            Logger.Info($"Getting level for point: {routeResult.Path[0]}");
            var level = GetLevel(routeResult.Path[0]);
            if (level == null)
            {
                Logger.Error("Could not find level");
                return new SmartCreationResult
                {
                    Success = false,
                    ElementsCreated = 0,
                    Message = "Could not find level"
                };
            }
            Logger.Info($"Found level: {level.Name}");

            Logger.Info("Finding mechanical system type");
            var systemType = FindMechanicalSystemType();
            Logger.Info("Finding duct type");
            var ductType = GetDuctType(_document, ductTypeName);

            if (systemType == null || ductType == null)
            {
                Logger.Error($"System type found: {systemType != null}, Duct type found: {ductType != null}");
                return new SmartCreationResult
                {
                    Success = false,
                    ElementsCreated = 0,
                    Message = $"Could not find system type or duct type '{ductTypeName}'"
                };
            }
            Logger.Info($"Found system type: {systemType.Name}, duct type: {ductType.Name}");

            Logger.Info("Creating lines from path");
            var ductLines = CreateLinesFromPath(routeResult.Path);
            Logger.Info($"Created {ductLines.Count} lines");
            
            Logger.Info("Creating duct segments");
            var ducts = CreateDuctSegments(level.Id, systemType.Id, ductType.Id, ductLines);
            
            if (ducts == null || !ducts.Any())
            {
                Logger.Error("Failed to create duct segments");
                return new SmartCreationResult
                {
                    Success = false,
                    ElementsCreated = 0,
                    Message = "Failed to create duct segments"
                };
            }

            Logger.Info($"Successfully created {ducts.Count} duct segments");
            return new SmartCreationResult
            {
                Success = true,
                ElementsCreated = ducts.Count,
                Message = $"Created {ducts.Count} duct segments"
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Error in CreateDuctRoute", ex);
            return new SmartCreationResult
            {
                Success = false,
                ElementsCreated = 0,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Создает воздуховод с использованием маршрута
    /// </summary>
    public SmartCreationResult CreateSmartDuct(XYZ start, XYZ end, MEPRouteResult routeResult)
    {
        Logger.Info($"CreateSmartDuct called with route from {start} to {end}");
        
        // Если маршрут не указан или пустой, пытаемся найти маршрут
        if (routeResult == null || !routeResult.Success || routeResult.Path.Count < 2)
        {
            Logger.Info("Route not provided or invalid. Finding route.");
            routeResult = FindDuctRoute(start, end, 1.0, 0.5);
        }
        
        // Используем существующий метод CreateDuctRoute
        return CreateDuctRoute(routeResult);
    }

    private List<Line> CreateLinesFromPath(List<XYZ> path)
    {
        var lines = new List<Line>();
        
        for (int i = 0; i < path.Count - 1; i++)
        {
            var startPoint = path[i];
            var endPoint = path[i + 1];
            
            if (!startPoint.IsAlmostEqualTo(endPoint))
            {
                var line = Line.CreateBound(startPoint, endPoint);
                lines.Add(line);
            }
        }
        
        return lines;
    }

    private List<Duct> CreateDuctSegments(ElementId levelId, ElementId systemTypeId, ElementId ductTypeId, List<Line> ductLines)
    {
        try
        {
            var ducts = new List<Duct>();
            
            foreach (var line in ductLines)
            {
                var startPoint = line.GetEndPoint(0);
                var endPoint = line.GetEndPoint(1);
                
                var duct = Duct.Create(_document, systemTypeId, ductTypeId, levelId, startPoint, endPoint);
                
                if (duct != null)
                {
                    ducts.Add(duct);
                    Logger.Info($"Created duct segment from {startPoint} to {endPoint}");
                }
                else
                {
                    Logger.Error($"Failed to create duct between {startPoint} and {endPoint}");
                    return null;
                }
            }
            
            return ducts;
        }
        catch (Exception ex)
        {
            Logger.Error("Error creating duct segments", ex);
            return null;
        }
    }

    private bool ConnectDuctsWithFittings(List<Duct> ducts)
    {
        try
        {
            for (int i = 0; i < ducts.Count - 1; i++)
            {
                var connectors1 = ducts[i].ConnectorManager.Connectors.Cast<Connector>().ToList();
                var connectors2 = ducts[i + 1].ConnectorManager.Connectors.Cast<Connector>().ToList();

                var currentCurve = ducts[i].Location as LocationCurve;
                var nextCurve = ducts[i + 1].Location as LocationCurve;

                if (currentCurve == null || nextCurve == null)
                {
                    Logger.Warning("Could not get duct location curves");
                    return false;
                }

                var firstConnector = connectors1.FirstOrDefault(c =>
                    c.Origin.IsAlmostEqualTo(nextCurve.Curve.GetEndPoint(0)) ||
                    c.Origin.IsAlmostEqualTo(nextCurve.Curve.GetEndPoint(1)));

                var secondConnector = connectors2.FirstOrDefault(c =>
                    c.Origin.IsAlmostEqualTo(currentCurve.Curve.GetEndPoint(0)) ||
                    c.Origin.IsAlmostEqualTo(currentCurve.Curve.GetEndPoint(1)));

                if (firstConnector == null || secondConnector == null)
                {
                    Logger.Warning($"Could not find suitable connectors for ducts {i} and {i + 1}");
                    return false;
                }

                var fitting = _document.Create.NewElbowFitting(firstConnector, secondConnector);
                if (fitting == null)
                {
                    Logger.Warning($"Failed to create fitting between ducts {i} and {i + 1}");
                    return false;
                }
                else
                {
                    Logger.Info($"Created fitting between ducts {i} and {i + 1}");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Error connecting ducts with fittings", ex);
            return false;
        }
    }

    /// <summary>
    /// Дополнительный метод для валидации маршрута
    /// </summary>
    private bool ValidateRoutePath(List<XYZ> path, XYZ expectedStart, XYZ expectedEnd)
    {
        if (path == null || path.Count < 2)
        {
            Logger.Warning("Path validation failed: path is null or too short");
            return false;
        }
        
        // Проверяем начальную точку с допуском
        if (!path[0].IsAlmostEqualTo(expectedStart, 0.1))
        {
            Logger.Warning($"Path validation warning: start point mismatch. Expected: {expectedStart}, Got: {path[0]}");
        }
        
        // Проверяем конечную точку с допуском
        if (!path.Last().IsAlmostEqualTo(expectedEnd, 0.1))
        {
            Logger.Warning($"Path validation warning: end point mismatch. Expected: {expectedEnd}, Got: {path.Last()}");
        }
        
        // Проверяем, что нет дубликатов точек подряд
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i].IsAlmostEqualTo(path[i - 1], 0.01))
            {
                Logger.Warning($"Path validation warning: duplicate consecutive points at index {i}: {path[i]}");
            }
        }
        
        Logger.Info($"Path validation passed: {path.Count} points from {expectedStart} to {expectedEnd}");
        return true;
    }
}

public class SmartCreationResult
{
    public bool Success { get; set; }
    public int ElementsCreated { get; set; }
    public string Message { get; set; }
}

public class MEPRouteResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<XYZ> Path { get; set; } = new List<XYZ>();
    public List<MEPFitting> Fittings { get; set; } = new List<MEPFitting>();
    public double TotalLength { get; set; }
}

public class MEPFitting
{
    public XYZ Position { get; set; }
    public string Type { get; set; }
    public string Size { get; set; }
}

public class PathWithFittings
{
    public List<XYZ> Path { get; set; } = new List<XYZ>();
    public List<MEPFitting> Fittings { get; set; } = new List<MEPFitting>();
}