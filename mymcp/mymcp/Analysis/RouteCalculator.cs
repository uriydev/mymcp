using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using mymcp.Core;

namespace mymcp.Analysis;

/// <summary>
/// Калькулятор маршрутов с алгоритмами поиска пути
/// </summary>
public class RouteCalculator
{
    private readonly SpaceAnalyzer _spaceAnalyzer;
    private readonly double _gridSize;
    
    public RouteCalculator(SpaceAnalyzer spaceAnalyzer, double gridSize = 1.0)
    {
        _spaceAnalyzer = spaceAnalyzer;
        _gridSize = gridSize; // Размер сетки для дискретизации пространства
    }

    /// <summary>
    /// Находит оптимальный путь между двумя точками с обходом препятствий
    /// </summary>
    public List<XYZ> FindOptimalPath(XYZ start, XYZ end, double clearance = 0.5)
    {
        try
        {
            Logger.Info($"Calculating optimal path from {start} to {end}");

            // Сначала проверяем прямой путь
            var obstacles = _spaceAnalyzer.FindObstaclesOnPath(start, end, clearance);
            if (!obstacles.Any())
            {
                Logger.Info("Direct path available");
                return new List<XYZ> { start, end };
            }

            // Если есть препятствия, используем алгоритм A*
            var path = FindPathWithAStar(start, end, obstacles, clearance);
            
            if (path?.Any() == true)
            {
                Logger.Info($"Found path with {path.Count} waypoints");
                return OptimizePath(path);
            }

            Logger.Warning("No valid path found");
            return new List<XYZ>();
        }
        catch (Exception ex)
        {
            Logger.Error("Error calculating optimal path", ex);
            return new List<XYZ>();
        }
    }

    /// <summary>
    /// Находит несколько альтернативных маршрутов
    /// </summary>
    public List<List<XYZ>> FindAlternativePaths(XYZ start, XYZ end, double clearance = 0.5, int maxAlternatives = 3)
    {
        try
        {
            var alternatives = new List<List<XYZ>>();
            var obstacles = _spaceAnalyzer.FindObstaclesOnPath(start, end, clearance);

            // Создаем несколько вариантов, изменяя параметры поиска
            for (int i = 0; i < maxAlternatives; i++)
            {
                var adjustedClearance = clearance + (i * 0.2);
                var path = FindPathWithAStar(start, end, obstacles, adjustedClearance);
                
                if (path?.Any() == true)
                {
                    var optimizedPath = OptimizePath(path);
                    if (!PathAlreadyExists(optimizedPath, alternatives))
                    {
                        alternatives.Add(optimizedPath);
                    }
                }
            }

            Logger.Info($"Found {alternatives.Count} alternative paths");
            return alternatives.OrderBy(p => CalculatePathLength(p)).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding alternative paths", ex);
            return new List<List<XYZ>>();
        }
    }

    /// <summary>
    /// Алгоритм A* для поиска пути
    /// </summary>
    private List<XYZ> FindPathWithAStar(XYZ start, XYZ end, List<Element> obstacles, double clearance)
    {
        // Создаем сетку для дискретизации пространства
        var grid = CreateSearchGrid(start, end, obstacles, clearance);
        
        var openSet = new List<PathNode>();
        var closedSet = new HashSet<string>();
        
        var startNode = new PathNode
        {
            Position = GetNearestGridPoint(start),
            GCost = 0,
            HCost = CalculateHeuristic(start, end),
            Parent = null
        };
        
        openSet.Add(startNode);

        while (openSet.Any())
        {
            // Находим узел с наименьшей стоимостью F
            var currentNode = openSet.OrderBy(n => n.FCost).First();
            openSet.Remove(currentNode);
            
            var currentKey = GetNodeKey(currentNode.Position);
            if (closedSet.Contains(currentKey)) continue;
            closedSet.Add(currentKey);

            // Проверяем, достигли ли мы цели
            if (currentNode.Position.DistanceTo(end) < _gridSize)
            {
                return ReconstructPath(currentNode, end);
            }

            // Исследуем соседние узлы
            var neighbors = GetNeighbors(currentNode.Position, grid);
            
            foreach (var neighbor in neighbors)
            {
                var neighborKey = GetNodeKey(neighbor);
                if (closedSet.Contains(neighborKey)) continue;

                var gCost = currentNode.GCost + currentNode.Position.DistanceTo(neighbor);
                var hCost = CalculateHeuristic(neighbor, end);
                
                var existingNode = openSet.FirstOrDefault(n => 
                    GetNodeKey(n.Position) == neighborKey);
                
                if (existingNode == null)
                {
                    openSet.Add(new PathNode
                    {
                        Position = neighbor,
                        GCost = gCost,
                        HCost = hCost,
                        Parent = currentNode
                    });
                }
                else if (gCost < existingNode.GCost)
                {
                    existingNode.GCost = gCost;
                    existingNode.Parent = currentNode;
                }
            }
        }

        return null; // Путь не найден
    }

    /// <summary>
    /// Создает сетку для поиска пути
    /// </summary>
    private HashSet<string> CreateSearchGrid(XYZ start, XYZ end, List<Element> obstacles, double clearance)
    {
        var grid = new HashSet<string>();
        
        // Определяем границы области поиска
        var minX = Math.Min(start.X, end.X) - 10;
        var maxX = Math.Max(start.X, end.X) + 10;
        var minY = Math.Min(start.Y, end.Y) - 10;
        var maxY = Math.Max(start.Y, end.Y) + 10;
        var minZ = Math.Min(start.Z, end.Z) - 5;
        var maxZ = Math.Max(start.Z, end.Z) + 5;

        // Создаем узлы сетки
        for (double x = minX; x <= maxX; x += _gridSize)
        {
            for (double y = minY; y <= maxY; y += _gridSize)
            {
                for (double z = minZ; z <= maxZ; z += _gridSize)
                {
                    var point = new XYZ(x, y, z);
                    
                    // Проверяем, не находится ли точка в препятствии
                    if (_spaceAnalyzer.IsSpaceAvailable(point, clearance))
                    {
                        grid.Add(GetNodeKey(point));
                    }
                }
            }
        }

        Logger.Debug($"Created search grid with {grid.Count} valid nodes");
        return grid;
    }

    /// <summary>
    /// Получает соседние узлы для данной позиции
    /// </summary>
    private List<XYZ> GetNeighbors(XYZ position, HashSet<string> grid)
    {
        var neighbors = new List<XYZ>();
        
        // 26 направлений в 3D пространстве (включая диагонали)
        var offsets = new[]
        {
            new XYZ(-1, -1, -1), new XYZ(-1, -1, 0), new XYZ(-1, -1, 1),
            new XYZ(-1, 0, -1), new XYZ(-1, 0, 0), new XYZ(-1, 0, 1),
            new XYZ(-1, 1, -1), new XYZ(-1, 1, 0), new XYZ(-1, 1, 1),
            new XYZ(0, -1, -1), new XYZ(0, -1, 0), new XYZ(0, -1, 1),
            new XYZ(0, 0, -1), new XYZ(0, 0, 1),
            new XYZ(0, 1, -1), new XYZ(0, 1, 0), new XYZ(0, 1, 1),
            new XYZ(1, -1, -1), new XYZ(1, -1, 0), new XYZ(1, -1, 1),
            new XYZ(1, 0, -1), new XYZ(1, 0, 0), new XYZ(1, 0, 1),
            new XYZ(1, 1, -1), new XYZ(1, 1, 0), new XYZ(1, 1, 1)
        };

        foreach (var offset in offsets)
        {
            var neighbor = position + (offset * _gridSize);
            var key = GetNodeKey(neighbor);
            
            if (grid.Contains(key))
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Восстанавливает путь из узлов
    /// </summary>
    private List<XYZ> ReconstructPath(PathNode endNode, XYZ targetEnd)
    {
        var path = new List<XYZ>();
        var current = endNode;

        while (current != null)
        {
            path.Add(current.Position);
            current = current.Parent;
        }

        path.Reverse();
        path.Add(targetEnd); // Добавляем точную конечную точку

        return path;
    }

    /// <summary>
    /// Оптимизирует путь, удаляя избыточные точки
    /// </summary>
    private List<XYZ> OptimizePath(List<XYZ> path)
    {
        if (path.Count <= 2) return path;

        var optimized = new List<XYZ> { path[0] };

        for (int i = 1; i < path.Count - 1; i++)
        {
            var prev = optimized.Last();
            var current = path[i];
            var next = path[i + 1];

            // Проверяем, можно ли пропустить текущую точку
            var obstacles = _spaceAnalyzer.FindObstaclesOnPath(prev, next, 0.3);
            if (obstacles.Any())
            {
                optimized.Add(current);
            }
        }

        optimized.Add(path.Last());
        
        Logger.Debug($"Optimized path from {path.Count} to {optimized.Count} points");
        return optimized;
    }

    /// <summary>
    /// Вычисляет эвристическую функцию (манхэттенское расстояние)
    /// </summary>
    private double CalculateHeuristic(XYZ from, XYZ to)
    {
        return Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y) + Math.Abs(from.Z - to.Z);
    }

    /// <summary>
    /// Вычисляет общую длину пути
    /// </summary>
    private double CalculatePathLength(List<XYZ> path)
    {
        double length = 0;
        for (int i = 1; i < path.Count; i++)
        {
            length += path[i - 1].DistanceTo(path[i]);
        }
        return length;
    }

    /// <summary>
    /// Находит ближайшую точку сетки
    /// </summary>
    private XYZ GetNearestGridPoint(XYZ point)
    {
        var x = Math.Round(point.X / _gridSize) * _gridSize;
        var y = Math.Round(point.Y / _gridSize) * _gridSize;
        var z = Math.Round(point.Z / _gridSize) * _gridSize;
        
        return new XYZ(x, y, z);
    }

    /// <summary>
    /// Создает ключ для узла сетки
    /// </summary>
    private string GetNodeKey(XYZ point)
    {
        var gridPoint = GetNearestGridPoint(point);
        return $"{gridPoint.X:F2}_{gridPoint.Y:F2}_{gridPoint.Z:F2}";
    }

    /// <summary>
    /// Проверяет, существует ли уже такой путь
    /// </summary>
    private bool PathAlreadyExists(List<XYZ> newPath, List<List<XYZ>> existingPaths)
    {
        const double tolerance = 1.0;
        
        foreach (var existingPath in existingPaths)
        {
            if (Math.Abs(CalculatePathLength(newPath) - CalculatePathLength(existingPath)) < tolerance)
            {
                return true;
            }
        }
        
        return false;
    }
}

/// <summary>
/// Узел для алгоритма поиска пути
/// </summary>
public class PathNode
{
    public XYZ Position { get; set; }
    public double GCost { get; set; } // Стоимость от начала
    public double HCost { get; set; } // Эвристическая стоимость до цели
    public double FCost => GCost + HCost; // Общая стоимость
    public PathNode Parent { get; set; }
} 