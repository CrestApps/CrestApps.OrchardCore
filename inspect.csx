using System;
using System.Reflection;

var asm = Assembly.LoadFrom(@"C:\Users\mike\.nuget\packages\orchardcore.abstractions\3.0.0-preview-18934\lib\net10.0\OrchardCore.Abstractions.dll");
foreach (var t in asm.GetExportedTypes())
{
    if (t.Name == "ShellScope")
    {
        Console.WriteLine($"Type: {t.FullName}");
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (m.Name.Contains("BeforeDispose") || m.Name.Contains("RegisterBefore") || m.Name.Contains("AddException") || m.Name.Contains("ExceptionHandler"))
            {
                var parms = string.Join(", ", Array.ConvertAll(m.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({parms})");
            }
        }
    }
}
