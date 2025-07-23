using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace mymcp.Analysis;

/// <summary>
/// Утилиты для работы с геометрией
/// </summary>
public static class GeometryUtils
{
    /// <summary>
    /// Вычисляет расстояние между двумя точками
    /// </summary>
    public static double Distance(XYZ point1, XYZ point2)
    {
        return point1.DistanceTo(point2);
    }

    /// <summary>
    /// Проверяет пересечение двух ограничивающих прямоугольников
    /// </summary>
    public static bool BoundingBoxesIntersect(BoundingBoxXYZ box1, BoundingBoxXYZ box2)
    {
        if (box1 == null || box2 == null) return false;

        return box1.Min.X <= box2.Max.X && box1.Max.X >= box2.Min.X &&
               box1.Min.Y <= box2.Max.Y && box1.Max.Y >= box2.Min.Y &&
               box1.Min.Z <= box2.Max.Z && box1.Max.Z >= box2.Min.Z;
    }

    /// <summary>
    /// Находит точку пересечения двух линий в плоскости XY
    /// </summary>
    public static XYZ FindLineIntersection2D(Line line1, Line line2)
    {
        var p1 = line1.GetEndPoint(0);
        var p2 = line1.GetEndPoint(1);
        var p3 = line2.GetEndPoint(0);
        var p4 = line2.GetEndPoint(1);

        var denominator = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
        
        if (Math.Abs(denominator) < 1e-10) return null; // Параллельные линии

        var t = ((p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X)) / denominator;
        
        return new XYZ(
            p1.X + t * (p2.X - p1.X),
            p1.Y + t * (p2.Y - p1.Y),
            p1.Z + t * (p2.Z - p1.Z)
        );
    }

    /// <summary>
    /// Создает ограничивающий прямоугольник для набора точек
    /// </summary>
    public static BoundingBoxXYZ CreateBoundingBox(IEnumerable<XYZ> points)
    {
        var pointList = points.ToList();
        if (!pointList.Any()) return null;

        var minX = pointList.Min(p => p.X);
        var maxX = pointList.Max(p => p.X);
        var minY = pointList.Min(p => p.Y);
        var maxY = pointList.Max(p => p.Y);
        var minZ = pointList.Min(p => p.Z);
        var maxZ = pointList.Max(p => p.Z);

        return new BoundingBoxXYZ
        {
            Min = new XYZ(minX, minY, minZ),
            Max = new XYZ(maxX, maxY, maxZ)
        };
    }

    /// <summary>
    /// Проверяет, находится ли точка внутри ограничивающего прямоугольника
    /// </summary>
    public static bool IsPointInBoundingBox(XYZ point, BoundingBoxXYZ boundingBox)
    {
        if (boundingBox == null) return false;

        return point.X >= boundingBox.Min.X && point.X <= boundingBox.Max.X &&
               point.Y >= boundingBox.Min.Y && point.Y <= boundingBox.Max.Y &&
               point.Z >= boundingBox.Min.Z && point.Z <= boundingBox.Max.Z;
    }

    /// <summary>
    /// Вычисляет центр ограничивающего прямоугольника
    /// </summary>
    public static XYZ GetBoundingBoxCenter(BoundingBoxXYZ boundingBox)
    {
        if (boundingBox == null) return XYZ.Zero;

        return (boundingBox.Min + boundingBox.Max) * 0.5;
    }

    /// <summary>
    /// Создает линию между двумя точками
    /// </summary>
    public static Line CreateLine(XYZ start, XYZ end)
    {
        if (start.IsAlmostEqualTo(end))
            throw new ArgumentException("Start and end points cannot be the same");

        return Line.CreateBound(start, end);
    }

    /// <summary>
    /// Проверяет, пересекается ли линия с ограничивающим прямоугольником
    /// </summary>
    public static bool LineIntersectsBoundingBox(Line line, BoundingBoxXYZ boundingBox)
    {
        if (line == null || boundingBox == null) return false;

        var start = line.GetEndPoint(0);
        var end = line.GetEndPoint(1);

        // Проверяем, находятся ли концы линии по разные стороны от границ
        return LineIntersectsPlane(start, end, boundingBox.Min.X, boundingBox.Max.X, 0) ||
               LineIntersectsPlane(start, end, boundingBox.Min.Y, boundingBox.Max.Y, 1) ||
               LineIntersectsPlane(start, end, boundingBox.Min.Z, boundingBox.Max.Z, 2);
    }

    private static bool LineIntersectsPlane(XYZ start, XYZ end, double min, double max, int axis)
    {
        var startCoord = GetCoordinate(start, axis);
        var endCoord = GetCoordinate(end, axis);

        return (startCoord <= min && endCoord >= min) ||
               (startCoord >= max && endCoord <= max) ||
               (startCoord >= min && startCoord <= max) ||
               (endCoord >= min && endCoord <= max);
    }

    private static double GetCoordinate(XYZ point, int axis)
    {
        return axis switch
        {
            0 => point.X,
            1 => point.Y,
            2 => point.Z,
            _ => throw new ArgumentException("Invalid axis")
        };
    }
} 