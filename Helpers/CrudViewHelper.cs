using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ZnErp.Helpers;

/// <summary>Generic CRUD views (CrudIndex / CrudForm) ke liye reflection helpers.</summary>
public static class CrudViewHelper
{
    public static bool IsScalar(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal) || u == typeof(DateTime);
    }

    public static List<PropertyInfo> ScalarProps(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && IsScalar(p.PropertyType))
            .ToList();

    public static List<PropertyInfo> FormProps(Type type) =>
        ScalarProps(type)
            .Where(p => p.Name != "Id" && p.CanWrite && p.GetCustomAttribute<ScaffoldColumnAttribute>()?.Scaffold != false)
            .ToList();

    public static string Label(PropertyInfo p)
    {
        var display = p.GetCustomAttribute<DisplayAttribute>()?.Name;
        if (!string.IsNullOrEmpty(display)) return display;

        var name = p.Name;
        if (name.Length > 2 && name.EndsWith("Id", StringComparison.Ordinal)) name = name[..^2];
        return Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");
    }

    public static string Format(PropertyInfo p, object row, Dictionary<string, List<SelectListItem>> lookups)
    {
        var value = p.GetValue(row);
        if (value == null) return "";

        if (lookups.TryGetValue(p.Name, out var options))
        {
            var match = options.FirstOrDefault(o => o.Value == value.ToString());
            if (match != null) return match.Text;
        }

        return value switch
        {
            bool b => b ? "Yes" : "No",
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd-MMM-yyyy") : d.ToString("dd-MMM-yyyy HH:mm"),
            decimal m => m.ToString("N2"),
            _ => value.ToString() ?? ""
        };
    }
}
