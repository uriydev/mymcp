using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using mymcp.Analysis;
using mymcp.Core;

namespace mymcp.Commands;

/// <summary>
/// Умная команда создания воздуховода с обходом препятствий
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class SmartCreateDuctCommand : ExternalCommand
{
    public override void Execute()
    {
        try
        {
            Logger.Info("Starting smart duct creation");

            var uiApp = ExternalCommandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            // Получаем точки от пользователя
            var startPoint = GetPointFromUser(uiDoc, "Выберите начальную точку воздуховода");
            if (startPoint == null) return;

            var endPoint = GetPointFromUser(uiDoc, "Выберите конечную точку воздуховода");
            if (endPoint == null) return;

            // Запрашиваем параметры воздуховода
            var ductParams = GetDuctParameters();
            if (ductParams == null) return;

            // Инициализируем анализаторы
            var spaceAnalyzer = new SpaceAnalyzer(uiApp);
            var routeCalculator = new RouteCalculator(spaceAnalyzer);
            var mepPathfinder = new MEPPathfinder(spaceAnalyzer, routeCalculator, doc);

            // Находим оптимальный маршрут
            var routeResult = mepPathfinder.FindDuctRoute(
                startPoint, 
                endPoint, 
                ductParams.Width, 
                ductParams.Height,
                ductParams.Level
            );

            if (!routeResult.Success)
            {
                TaskDialog.Show("Ошибка", $"Не удалось найти маршрут: {routeResult.Message}");
                return;
            }

            // Создаем воздуховоды по найденному маршруту
            using (var transaction = new Transaction(doc, "Создание умного воздуховода"))
            {
                transaction.Start();

                var createdElements = CreateDuctAlongPath(doc, routeResult, ductParams);

                if (createdElements.Any())
                {
                    transaction.Commit();
                    
                    // Показываем результат
                    var message = $"Создано {createdElements.Count} элементов воздуховода\n" +
                                  $"Общая длина: {routeResult.TotalLength:F2} фт\n" +
                                  $"Фитингов: {routeResult.Fittings.Count}";
                    
                    TaskDialog.Show("Успех", message);
                    
                    Logger.Info($"Smart duct creation completed. Created {createdElements.Count} elements");
                }
                else
                {
                    transaction.RollBack();
                    TaskDialog.Show("Ошибка", "Не удалось создать воздуховод");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Error in smart duct creation", ex);
            TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
        }
    }

    /// <summary>
    /// Получает точку от пользователя
    /// </summary>
    private XYZ GetPointFromUser(UIDocument uiDoc, string prompt)
    {
        try
        {
            TaskDialog.Show("Выбор точки", prompt);
            
            var selection = uiDoc.Selection.PickPoint();
            return selection;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("Error getting point from user", ex);
            return null;
        }
    }

    /// <summary>
    /// Получает параметры воздуховода от пользователя
    /// </summary>
    private DuctParameters GetDuctParameters()
    {
        // Пока используем стандартные параметры
        // В будущем можно добавить диалог для ввода параметров
        return new DuctParameters
        {
            Width = 1.0, // 1 фут
            Height = 0.5, // 0.5 фута
            Level = null // Автоматическое определение уровня
        };
    }

    /// <summary>
    /// Создает воздуховод по найденному пути
    /// </summary>
    private List<Element> CreateDuctAlongPath(Document doc, MEPRouteResult routeResult, DuctParameters ductParams)
    {
        var createdElements = new List<Element>();

        try
        {
            // Получаем тип воздуховода
            var ductType = GetDuctType(doc, ductParams.Width, ductParams.Height);
            if (ductType == null)
            {
                Logger.Error("Could not find or create duct type");
                return createdElements;
            }

            // Получаем MEP систему
            var mepSystem = GetMEPSystem(doc);
            if (mepSystem == null)
            {
                Logger.Error("Could not find MEP system");
                return createdElements;
            }

            // Определяем уровень
            var level = ductParams.Level ?? FindNearestLevel(doc, routeResult.Path.First());

            // Создаем сегменты воздуховода
            for (int i = 0; i < routeResult.Path.Count - 1; i++)
            {
                var startPoint = routeResult.Path[i];
                var endPoint = routeResult.Path[i + 1];

                try
                {
                    var duct = Duct.Create(
                        doc,
                        mepSystem.Id,
                        ductType.Id,
                        level.Id,
                        startPoint,
                        endPoint
                    );

                    if (duct != null)
                    {
                        createdElements.Add(duct);
                        Logger.Debug($"Created duct segment from {startPoint} to {endPoint}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating duct segment {i}", ex);
                }
            }

            Logger.Info($"Created {createdElements.Count} duct segments");
        }
        catch (Exception ex)
        {
            Logger.Error("Error creating duct along path", ex);
        }

        return createdElements;
    }

    /// <summary>
    /// Получает или создает тип воздуховода
    /// </summary>
    private DuctType GetDuctType(Document doc, double width, double height)
    {
        try
        {
            // Сначала ищем существующий тип
            var existingType = new FilteredElementCollector(doc)
                .OfClass(typeof(DuctType))
                .Cast<DuctType>()
                .FirstOrDefault(dt => 
                {
                    if (dt.Shape != ConnectorProfileType.Rectangular) return false;
                    
                    var widthParam = dt.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    var heightParam = dt.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    
                    if (widthParam == null || heightParam == null) return false;
                    
                    return Math.Abs(widthParam.AsDouble() - width) < 0.01 &&
                           Math.Abs(heightParam.AsDouble() - height) < 0.01;
                });

            if (existingType != null)
            {
                return existingType;
            }

            // Если не найден, создаем новый на основе первого доступного
            var baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(DuctType))
                .Cast<DuctType>()
                .FirstOrDefault(dt => dt.Shape == ConnectorProfileType.Rectangular);

            if (baseType != null)
            {
                var newType = baseType.Duplicate($"Воздуховод {width * 304.8:F0}x{height * 304.8:F0}мм") as DuctType;
                
                var widthParam = newType.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                var heightParam = newType.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

                widthParam?.Set(width);
                heightParam?.Set(height);

                return newType;
            }

            Logger.Warning("No suitable duct type found");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error("Error getting duct type", ex);
            return null;
        }
    }

    /// <summary>
    /// Получает MEP систему воздуховода
    /// </summary>
    private MEPSystemType GetMEPSystem(Document doc)
    {
        try
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(MEPSystemType))
                .Cast<MEPSystemType>()
                .FirstOrDefault(); // Просто берем первый доступный тип системы
        }
        catch (Exception ex)
        {
            Logger.Error("Error getting MEP system", ex);
            return null;
        }
    }

    /// <summary>
    /// Находит ближайший уровень
    /// </summary>
    private Level FindNearestLevel(Document doc, XYZ point)
    {
        try
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => Math.Abs(level.Elevation - point.Z))
                .ToList();

            return levels.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.Error("Error finding nearest level", ex);
            return null;
        }
    }
}

/// <summary>
/// Параметры воздуховода
/// </summary>
public class DuctParameters
{
    public double Width { get; set; }
    public double Height { get; set; }
    public Level Level { get; set; }
} 