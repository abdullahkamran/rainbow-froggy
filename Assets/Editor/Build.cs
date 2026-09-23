using UnityEditor;
using UnityEngine;

// Invoked by: unity build --target WebGL --execute-method Build.PerformWebGLBuild
public static class Build
{
    public static void BuildWebGL() => PerformWebGLBuild();

    public static void PerformWebGLBuild()
    {
        // Disable compression so the built files can be served without special
        // server-side headers.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        // Pick up -buildOutput if the CLI passed it, fall back to Builds/WebGL.
        string output = "Builds/WebGL";
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildOutput")
            {
                output = args[i + 1];
                break;
            }
        }

        var options = new BuildPlayerOptions
        {
            scenes           = new[] { "Assets/Scenes/Game.unity" },
            locationPathName = output,
            target           = BuildTarget.WebGL,
            options          = BuildOptions.None,
        };

        var report  = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("WebGL build succeeded: " + summary.totalSize + " bytes");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("WebGL build failed: " + summary.totalErrors + " errors");
            EditorApplication.Exit(1);
        }
    }
}
