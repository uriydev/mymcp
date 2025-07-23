using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using mymcp.Core;

namespace mymcp.Dynamic;

/// <summary>
/// Система безопасности для динамических команд
/// </summary>
public class SecuritySandbox
{
    private readonly HashSet<string> _allowedNamespaces;
    private readonly HashSet<string> _blockedMethods;
    private readonly HashSet<string> _blockedTypes;

    public SecuritySandbox()
    {
        _allowedNamespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Math",
            "Autodesk.Revit.DB",
            "Autodesk.Revit.DB.Mechanical",
            "Autodesk.Revit.DB.Plumbing",
            "Autodesk.Revit.DB.Electrical",
            "Autodesk.Revit.UI",
            "mymcp.Analysis",
            "mymcp.Dynamic",
            "mymcp.Core"
        };

        _blockedMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "File.Delete",
            "File.WriteAllText",
            "File.WriteAllBytes",
            "Directory.Delete",
            "Directory.CreateDirectory",
            "Process.Start",
            "Environment.Exit",
            "Assembly.Load",
            "Assembly.LoadFrom",
            "Activator.CreateInstance",
            "Type.GetType",
            "AppDomain.CreateDomain"
        };

        _blockedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System.IO.File",
            "System.IO.Directory", 
            "System.Diagnostics.Process",
            "System.Reflection.Assembly",
            "System.AppDomain",
            "System.Runtime.InteropServices.Marshal"
        };
    }

    /// <summary>
    /// Проверяет безопасность кода
    /// </summary>
    public SecurityValidationResult ValidateCode(string code)
    {
        var result = new SecurityValidationResult { IsSecure = true };

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();

            var walker = new SecurityWalker(_allowedNamespaces, _blockedMethods, _blockedTypes);
            walker.Visit(root);

            result.IsSecure = walker.IsSecure;
            result.SecurityViolations = walker.SecurityViolations;
            result.Warnings = walker.Warnings;

            Logger.Debug($"Security validation completed. Secure: {result.IsSecure}, Violations: {result.SecurityViolations.Count}");
        }
        catch (Exception ex)
        {
            Logger.Error("Error during security validation", ex);
            result.IsSecure = false;
            result.SecurityViolations.Add($"Security validation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Добавляет разрешенное пространство имен
    /// </summary>
    public void AddAllowedNamespace(string namespaceName)
    {
        _allowedNamespaces.Add(namespaceName);
        Logger.Info($"Added allowed namespace: {namespaceName}");
    }

    /// <summary>
    /// Блокирует метод
    /// </summary>
    public void BlockMethod(string methodName)
    {
        _blockedMethods.Add(methodName);
        Logger.Info($"Blocked method: {methodName}");
    }
}

/// <summary>
/// Обходчик синтаксического дерева для проверки безопасности
/// </summary>
public class SecurityWalker : CSharpSyntaxWalker
{
    private readonly HashSet<string> _allowedNamespaces;
    private readonly HashSet<string> _blockedMethods;
    private readonly HashSet<string> _blockedTypes;

    public bool IsSecure { get; private set; } = true;
    public List<string> SecurityViolations { get; } = new();
    public List<string> Warnings { get; } = new();

    public SecurityWalker(HashSet<string> allowedNamespaces, HashSet<string> blockedMethods, HashSet<string> blockedTypes)
    {
        _allowedNamespaces = allowedNamespaces;
        _blockedMethods = blockedMethods;
        _blockedTypes = blockedTypes;
    }

    public override void VisitUsingDirective(UsingDirectiveSyntax node)
    {
        var namespaceName = node.Name.ToString();
        
        if (!IsNamespaceAllowed(namespaceName))
        {
            AddSecurityViolation($"Restricted namespace: {namespaceName}");
        }

        base.VisitUsingDirective(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var methodCall = node.Expression.ToString();

        // Проверяем заблокированные методы
        if (_blockedMethods.Any(blocked => methodCall.Contains(blocked)))
        {
            AddSecurityViolation($"Blocked method call: {methodCall}");
        }

        // Проверяем опасные паттерны
        if (methodCall.Contains("GetType") || methodCall.Contains("Assembly"))
        {
            AddWarning($"Potentially unsafe reflection call: {methodCall}");
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var memberAccess = node.ToString();

        // Проверяем заблокированные типы
        if (_blockedTypes.Any(blocked => memberAccess.StartsWith(blocked)))
        {
            AddSecurityViolation($"Access to blocked type: {memberAccess}");
        }

        base.VisitMemberAccessExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var typeName = node.Type.ToString();

        // Проверяем создание опасных объектов
        if (_blockedTypes.Contains(typeName))
        {
            AddSecurityViolation($"Creation of blocked type: {typeName}");
        }

        base.VisitObjectCreationExpression(node);
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        // Проверяем подозрительные строки
        if (node.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            var value = node.Token.ValueText;
            
            if (value.Contains("cmd.exe") || value.Contains("powershell") || value.Contains("..\\"))
            {
                AddWarning($"Potentially suspicious string literal: {value}");
            }
        }

        base.VisitLiteralExpression(node);
    }

    private bool IsNamespaceAllowed(string namespaceName)
    {
        return _allowedNamespaces.Any(allowed => 
            namespaceName.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
            namespaceName.StartsWith(allowed + ".", StringComparison.OrdinalIgnoreCase));
    }

    private void AddSecurityViolation(string violation)
    {
        IsSecure = false;
        SecurityViolations.Add(violation);
    }

    private void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}

/// <summary>
/// Результат проверки безопасности
/// </summary>
public class SecurityValidationResult
{
    public bool IsSecure { get; set; }
    public List<string> SecurityViolations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
} 