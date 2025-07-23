using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using mymcp.Core;

namespace mymcp.Analysis;

/// <summary>
/// Анализатор пространства модели
/// </summary>
public class SpaceAnalyzer
{
    private readonly Document _document;
    private readonly UIDocument _uiDocument;

    public SpaceAnalyzer(UIApplication uiApp)
    {
        _uiDocument = uiApp.ActiveUIDocument;
        _document = _uiDocument.Document;
    }

    /// <summary>
    /// Получает все элементы в указанном ограничивающем прямоугольнике
    /// </summary>
    public List<Element> GetElementsInBoundingBox(BoundingBoxXYZ boundingBox)
    {
        try
        {
            var outline = new Outline(boundingBox.Min, boundingBox.Max);
            var filter = new BoundingBoxIntersectsFilter(outline);
            
            return new FilteredElementCollector(_document)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Error("Error getting elements in bounding box", ex);
            return new List<Element>();
        }
    }

    /// <summary>
    /// Находит препятствия на пути между двумя точками
    /// </summary>
    public List<Element> FindObstaclesOnPath(XYZ start, XYZ end, double clearance = 0.5)
    {
        try
        {
            var obstacles = new List<Element>();
            var pathDirection = (end - start).Normalize();
            var pathLength = start.DistanceTo(end);

            // Создаем расширенный ограничивающий прямоугольник вокруг пути
            var expandedBounds = CreatePathBoundingBox(start, end, clearance);
            var potentialObstacles = GetElementsInBoundingBox(expandedBounds);

            foreach (var element in potentialObstacles)
            {
                if (IsElementObstacle(element, start, end, clearance))
                {
                    obstacles.Add(element);
                }
            }

            Logger.Info($"Found {obstacles.Count} obstacles on path from {start} to {end}");
            return obstacles;
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding obstacles on path", ex);
            return new List<Element>();
        }
    }

    /// <summary>
    /// Проверяет доступность пространства для размещения элемента
    /// </summary>
    public bool IsSpaceAvailable(XYZ location, double radius)
    {
        try
        {
            var boundingBox = new BoundingBoxXYZ
            {
                Min = new XYZ(location.X - radius, location.Y - radius, location.Z - radius),
                Max = new XYZ(location.X + radius, location.Y + radius, location.Z + radius)
            };

            var intersectingElements = GetElementsInBoundingBox(boundingBox);
            
            // Исключаем элементы, которые не являются препятствиями
            var obstacles = intersectingElements.Where(IsPhysicalObstacle).ToList();

            Logger.Debug($"Space availability check at {location}: {obstacles.Count} obstacles found");
            return obstacles.Count == 0;
        }
        catch (Exception ex)
        {
            Logger.Error("Error checking space availability", ex);
            return false;
        }
    }

    /// <summary>
    /// Получает все уровни в проекте
    /// </summary>
    public List<Level> GetAllLevels()
    {
        try
        {
            return new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.Error("Error getting levels", ex);
            return new List<Level>();
        }
    }

    /// <summary>
    /// Находит ближайший уровень к указанной высоте
    /// </summary>
    public Level FindNearestLevel(double elevation)
    {
        try
        {
            var levels = GetAllLevels();
            if (!levels.Any()) return null;

            return levels.OrderBy(level => Math.Abs(level.Elevation - elevation)).First();
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding nearest level", ex);
            return null;
        }
    }

    /// <summary>
    /// Создает ограничивающий прямоугольник для пути
    /// </summary>
    private BoundingBoxXYZ CreatePathBoundingBox(XYZ start, XYZ end, double clearance)
    {
        var minX = Math.Min(start.X, end.X) - clearance;
        var maxX = Math.Max(start.X, end.X) + clearance;
        var minY = Math.Min(start.Y, end.Y) - clearance;
        var maxY = Math.Max(start.Y, end.Y) + clearance;
        var minZ = Math.Min(start.Z, end.Z) - clearance;
        var maxZ = Math.Max(start.Z, end.Z) + clearance;

        return new BoundingBoxXYZ
        {
            Min = new XYZ(minX, minY, minZ),
            Max = new XYZ(maxX, maxY, maxZ)
        };
    }

    /// <summary>
    /// Проверяет, является ли элемент препятствием на пути
    /// </summary>
    private bool IsElementObstacle(Element element, XYZ start, XYZ end, double clearance)
    {
        try
        {
            if (!IsPhysicalObstacle(element)) return false;

            var geometry = element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Medium });
            if (geometry == null) return false;

            // Проверяем пересечение геометрии элемента с путем
            var pathLine = Line.CreateBound(start, end);
            var boundingBox = element.get_BoundingBox(null);

            if (boundingBox == null) return false;

            // Расширяем ограничивающий прямоугольник на величину зазора
            boundingBox.Min = boundingBox.Min - new XYZ(clearance, clearance, clearance);
            boundingBox.Max = boundingBox.Max + new XYZ(clearance, clearance, clearance);

            return GeometryUtils.LineIntersectsBoundingBox(pathLine, boundingBox);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Проверяет, является ли элемент физическим препятствием
    /// </summary>
    private bool IsPhysicalObstacle(Element element)
    {
        var category = element.Category;
        if (category == null) return false;

        var obstacleCategories = new[]
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Furniture,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_PlumbingFixtures
        };

        return obstacleCategories.Contains((BuiltInCategory)category.Id.IntegerValue);
    }
} 