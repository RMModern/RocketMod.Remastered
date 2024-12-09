using Rocket.API;
using Rocket.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Linq;
using Rocket.Common;
using Rocket.Core.Extensions;

namespace Rocket.Core.Plugins
{
    public sealed class RocketPluginManager : MonoBehaviour
    {
        public delegate void PluginsLoaded();
        public event PluginsLoaded OnPluginsLoaded;

        private static List<Assembly> pluginAssemblies;
        private static List<GameObject> plugins = new List<GameObject>();

        internal static List<IRocketPlugin> Plugins => plugins
            .Select(g => g.GetComponent<IRocketPlugin>())
            .Where(p => p != null)
            .ToList();

        private Dictionary<AssemblyName, string> libraries = new();

        public List<IRocketPlugin> GetPlugins() => Plugins;

        public IRocketPlugin? GetPlugin(Assembly assembly) => plugins
            .Select(x => x.GetComponent<IRocketPlugin>())
            .FirstOrDefault(x => x is not null && x.GetType().Assembly == assembly);

        public IRocketPlugin? GetPlugin(string name) => plugins
            .Select(x => x.GetComponent<IRocketPlugin>())
            .FirstOrDefault(x => x is not null && x.Name == name);

        private void Awake()
        {
            new FileSystemWatcher(Environment.PluginsDirectory) { EnableRaisingEvents = true }.Changed +=
                (object sender, FileSystemEventArgs e) => { Reload(); };

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            AppDomain.CurrentDomain.TypeResolve += OnAssemblyResolve;
        }

        private record struct AssemblyLoadManifest(AssemblyName AssemblyName, Func<Assembly> Assembly);

        private Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);

            var availableLoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => new AssemblyLoadManifest(x.GetName(), () => x))
                .Where(x => x.AssemblyName.Name == assemblyName.Name);

            var availableLibraries = libraries
                .Select(x => new AssemblyLoadManifest(x.Key, () =>
                {
                    var librariesPath = Path.Combine(Directory.GetCurrentDirectory(), Environment.LibrariesDirectory);
                    if(R.Settings.Instance.LogLibraries)
                        Logger.Log($"Loading library '{x.Key.Name}' " +
                                   $"v{x.Key.Version} " +
                                   $"from '{x.Value.Replace(librariesPath, @"$Libraries")}'.", 
                                   ConsoleColor.Gray);
                    return Assembly.LoadFile(x.Value);
                }))
                .Where(x => x.AssemblyName.Name == assemblyName.Name);

            var availableAssemblies =
                availableLoadedAssemblies
                    .Concat(availableLibraries)
                    .OrderBy(x => x.AssemblyName.Version);

            foreach (var manifest in availableAssemblies)
            {
                if (manifest.AssemblyName.Version < assemblyName.Version)
                    continue;

                return manifest.Assembly(); //Hotloader.LoadAssembly(library.Value);
            }

            Logger.LogError($"Could not find dependency: {args.Name}");

            return null;
        }

        private void Start()
        {
            loadPlugins();
        }

        public Type? GetMainTypeFromAssembly(Assembly assembly) =>
            RocketHelper.GetTypesFromInterface(assembly, nameof(IRocketPlugin)).FirstOrDefault();

        private void loadPlugins()
        {
            Console.WriteLine();

            libraries = GetAssembliesFromDirectory(Environment.LibrariesDirectory);
            foreach (var kvp in GetAssembliesFromDirectory(Environment.PluginsDirectory))
                libraries[kvp.Key] = kvp.Value;

            pluginAssemblies = LoadAssembliesFromDirectory(Environment.PluginsDirectory);

            Console.WriteLine();
            var pluginImplemenations = RocketHelper.GetTypesFromInterface(pluginAssemblies, nameof(IRocketPlugin));
            foreach (var pluginType in pluginImplemenations)
            {
                var plugin = new GameObject(pluginType.Name, pluginType);
                DontDestroyOnLoad(plugin);
                plugins.Add(plugin);
            }

            OnPluginsLoaded.TryInvoke();
        }

        private void unloadPlugins() {
            for(int i = plugins.Count; i > 0; i--)
            {
                Destroy(plugins[i-1]);
            }
            plugins.Clear();
        }

        private void OnDestroy()
        {
            unloadPlugins();
        }

        internal void Reload()
        {
            unloadPlugins();
            loadPlugins();
        }

        public static Dictionary<AssemblyName, string> GetAssembliesFromDirectory(string directory,
            string extension = "*.dll")
        {
            var libraries = new Dictionary<AssemblyName, string>();

            var files = new DirectoryInfo(directory).GetFiles(extension, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var name = AssemblyName.GetAssemblyName(file.FullName);
                    libraries.Add(name, file.FullName);
                }
                catch
                {
                }
            }

            return libraries;
        }

        public static List<Assembly> LoadAssembliesFromDirectory(string directory, string extension = "*.dll")
        {
            var assemblies = new List<Assembly>();
            var files = new DirectoryInfo(directory).GetFiles(extension, SearchOption.AllDirectories);


            foreach (var file in files)
            {
                var filePath = file.FullName;
                try
                {
                    var assembly = Hotloader.LoadAssembly(filePath);

                    if (assembly is null)
                        throw new Exception($"Failed to load assembly {filePath}");

                    var assemblyManifest = assembly.GetName();

                    var types = RocketHelper.GetTypesFromInterface(assembly, nameof(IRocketPlugin))
                        .FindAll(x => !x.IsAbstract);

                    if (types.Count == 1)
                    {
                        Logger.Log(
                            $"Loading {assemblyManifest.Name} v{assemblyManifest.Version} from {filePath}");
                        assemblies.Add(assembly);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Could not load assembly: {filePath}");
                }
            }

            return assemblies;
        }
    }
}