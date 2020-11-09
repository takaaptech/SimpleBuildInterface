﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[assembly: InternalsVisibleTo("SimpleBuildInterfaceTest")]

internal class SimpleBuildInterface
{
    static string s_OldSymbols = null;

    [InitializeOnLoadMethod]
    private static void OnInitializeOnLoadMethod()
    {
        if (!Application.isBatchMode) return;

        var args = ParseArguments(Environment.GetCommandLineArgs());
        if (!args.ContainsKey("build")) return;

        EditorApplication.delayCall += () =>
        {
            var success = Build(EditorUserBuildSettings.activeBuildTarget, Environment.GetCommandLineArgs(), Application.isBatchMode);
            EditorApplication.Exit(success ? 0 : 1);
        };
    }

    public static bool Build(BuildTarget target, string[] arguments, bool isBatchMode)
    {
        var success = false;
        var options = GetCurrentBuildPlayerOptions(target, arguments);

        if (ValidBuildPlayerOptions(isBatchMode, ref options))
        {
            options = UpdateBuildPlayerOptions(options, arguments);
            options.options |= BuildOptions.ShowBuiltPlayer;
            var report = BuildPipeline.BuildPlayer(options);
            success = report.summary.result == BuildResult.Succeeded;
        }

        return success;
    }

    public static BuildPlayerOptions GetCurrentBuildPlayerOptions(BuildTarget target, string[] arguments)
    {
        var targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        var options = new BuildPlayerOptions
        {
            target = target,
            targetGroup = targetGroup,
            locationPathName = EditorUserBuildSettings.GetBuildLocation(target),
        };

        return UpdateBuildPlayerOptions(options, arguments);
    }

    public static BuildPlayerOptions UpdateBuildPlayerOptions(BuildPlayerOptions options, string[] arguments)
    {
        return options;
    }

    public static bool ValidBuildPlayerOptions(bool isBatchMode, ref BuildPlayerOptions options)
    {
        options.targetGroup = BuildPipeline.GetBuildTargetGroup(options.target);
        EditorUserBuildSettings.selectedBuildTargetGroup = options.targetGroup;

        try
        {
            // Path check.
            var askForBuildLocation = true;
            var path = (options.locationPathName ?? "").TrimEnd('/', '\\');
            if (0 < path.Length)
            {
                options.locationPathName = Path.GetFullPath(path);
                askForBuildLocation = !Directory.Exists(Path.GetDirectoryName(options.locationPathName));
            }


            if (isBatchMode && askForBuildLocation) return false;

            EditorUserBuildSettings.SetBuildLocation(options.target, options.locationPathName);

            var getBuildPlayerOptionsHandler = typeof(BuildPlayerWindow)
                .GetField("getBuildPlayerOptionsHandler", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null) as Func<BuildPlayerOptions, BuildPlayerOptions>;
            if (getBuildPlayerOptionsHandler != null)
            {
                options = getBuildPlayerOptionsHandler.Invoke(options);
            }
            else
            {
                options = (BuildPlayerOptions) typeof(BuildPlayerWindow.DefaultBuildMethods)
                    .GetMethod("GetBuildPlayerOptionsInternal", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] {askForBuildLocation, options});
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return false;
        }
    }
}
