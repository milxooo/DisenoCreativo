using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RobaColores.Game.EditorTools
{
    // Ejecuta las pruebas NUnit del nucleo sin abrir el Test Runner (util para verificar desde scripts).
    // Tambien se pueden lanzar desde Window > General > Test Runner > EditMode.
    public static class CoreTestRunnerMenu
    {
        [MenuItem("Roba Colores/Ejecutar pruebas del nucleo")]
        public static void Run()
        {
            Debug.Log(Execute());
        }

        public static string Execute()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "RobaColores.Game.Tests");
            if (asm == null) return "Ensamblado de pruebas no cargado.";
            int pass = 0, fail = 0;
            var sb = new StringBuilder();
            foreach (var t in asm.GetTypes())
            {
                var methods = t.GetMethods().Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TestAttribute")).ToList();
                foreach (var m in methods)
                {
                    var instance = Activator.CreateInstance(t);
                    try { m.Invoke(instance, null); pass++; }
                    catch (TargetInvocationException e)
                    {
                        fail++;
                        sb.AppendLine("FALLA " + t.Name + "." + m.Name + ": " + e.InnerException.Message.Replace("\n", " "));
                    }
                }
            }
            return "Pruebas del nucleo: " + pass + " correctas, " + fail + " fallan.\n" + sb;
        }
    }
}
