using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using SDG.Framework.Modules;
using SDG.Unturned;

namespace Rocket.Unturned.Bootstrap;

/*
 *  This code is a modified version of a OpenMod.Unturned bootstrapper.
 *  # Credits #
 *  Original authors: Enes Sadik Özbek & OpenMod contributors
 *  Source: https://github.com/openmod/openmod/blob/c77761250883d255a457d50fcc71c6c9a371e7d4/unturned/OpenMod.Unturned.Module.Bootstrapper/BootstrapperModule.cs
 */
/// <summary>
/// Bootstrapper of Rocket.Unturned module.
/// </summary>
[UsedImplicitly]
public class BootstrapperModule : IModuleNexus
{
    private IModuleNexus? rocketModule;
    private ConcurrentDictionary<string, Assembly>? loaded;
    private const string ModuleName = "Rocket.Unturned",
                         ModuleAssemblyFileName = "Rocket.Unturned.dll",
                         ModuleTypeName = "U";

    /// <summary>
    /// Instance of bootstrapper.
    /// </summary>
    /// <remarks>
    /// Note, this instance is used in hard-reload via reflection.
    /// </remarks>
    public static BootstrapperModule? Instance { get; private set; }

    public void initialize()
    {
        Instance = this;

        var rocketModModuleDirectory = string.Empty;
        var bootstrapperAssemblyFileName = string.Empty;
        var bootstrapperAssembly = typeof(BootstrapperModule).Assembly;

        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var module in ModuleHook.modules)
        {
            // ReSharper disable once InvertIf
            if (module.assemblies is { Length: > 0 } && module.assemblies[0] == bootstrapperAssembly)
            {
                rocketModModuleDirectory = Path.GetFullPath(module.config.DirectoryPath);
                bootstrapperAssemblyFileName = Path.GetFileName(module.config.Assemblies[0].Path);
                break;
            }
        }

        if (string.IsNullOrEmpty(rocketModModuleDirectory))
        {
            throw new Exception($"Failed to get {ModuleName} module directory");
        }

        loaded = new();
        ModuleHook.PreVanillaAssemblyResolve += UnturnedPreVanillaAssemblyResolve;

        Assembly? moduleAssembly = null;

        foreach (var assemblyFilePath in Directory.GetFiles(rocketModModuleDirectory, "*.dll", 
                                                            SearchOption.AllDirectories))
        {
            if (Path.GetFileName(assemblyFilePath).Equals(bootstrapperAssemblyFileName, 
                                                          StringComparison.OrdinalIgnoreCase))
                continue;

            var symbolsFilePath = Path.ChangeExtension(assemblyFilePath, "pdb");
            var symbols = File.Exists(symbolsFilePath) ? File.ReadAllBytes(symbolsFilePath) : null;

            var assembly = Assembly.Load(File.ReadAllBytes(assemblyFilePath), symbols);
            loaded.TryAdd(assembly.GetName().Name, assembly);

            var fileName = Path.GetFileName(assemblyFilePath);
            if (fileName.Equals(ModuleAssemblyFileName))
            {
                moduleAssembly = assembly;
            }
        }

        if (moduleAssembly is null)
        {
            throw new Exception($"Failed to find {ModuleName} module assembly!");
        }

        AddRocketModResolveAssemblies();

        IEnumerable<Type> types;
        try
        {
            types = moduleAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Console.WriteLine($"{ModuleName} Bootstrap failed to obtain assembly types: {ex}");
            foreach (var loaderException in ex.LoaderExceptions)
            {
                Console.WriteLine($"Loader exception: {loaderException}");
            }

            Console.WriteLine("Trying to load bootstrap anyways...");
            types = ex.Types.Where(d => d != null);
        }

        var moduleType = types.SingleOrDefault(d => d.Name.Equals(ModuleTypeName)) ??
                         throw new Exception($"Failed to find {ModuleTypeName} class in {moduleAssembly}!");
        rocketModule = (IModuleNexus)Activator.CreateInstance(moduleType);
        rocketModule.initialize();

        PatchUnturnedVanillaAssemblyResolve();
    }

    /// <summary>
    /// To prevent unnecessary errors and warning about assemblies resolving
    /// We will make Unturned Resolve Assemblies as last resource
    /// With this patch other components like hotloader wil try to resolve assemblies before unturned
    /// </summary>
    private void PatchUnturnedVanillaAssemblyResolve()
    {
        var assemblyResolveMethod =
            typeof(ModuleHook).GetMethod("handleAssemblyResolve", BindingFlags.NonPublic | BindingFlags.Instance);
        if (assemblyResolveMethod is null)
        {
            Console.WriteLine($"Couldn't find OnAssemblyResolve method for {nameof(ModuleHook)}!");
            return;
        }

        //using
        var provider = Provider.steam;
        if (!provider)
        {
            Console.WriteLine("Couldn't find Provider instance!");
            return;
        }

        var moduleHook = provider.GetComponent<ModuleHook>();
        if (!moduleHook)
        {
            Console.WriteLine("Couldn't get ModuleHook instance from Provider!");
            return;
        }

        var vanillaDelegate =
            (ResolveEventHandler)assemblyResolveMethod.CreateDelegate(typeof(ResolveEventHandler), moduleHook);

        AppDomain.CurrentDomain.AssemblyResolve -= vanillaDelegate;
        AppDomain.CurrentDomain.AssemblyResolve += vanillaDelegate;
    }

    /// <summary>
    /// Adds LDM assemblies to our assembly resolver to fix assembly resolve issue due to using different version
    /// </summary>
    private void AddRocketModResolveAssemblies()
    {
        foreach (var module in ModuleHook.modules)
        {
            if (!module.config.Name.Equals(ModuleName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var moduleAssembly in module.config.Assemblies)
            {
                var path = module.config.DirectoryPath + moduleAssembly.Path;

                // let Unturned get the assembly
                var assembly = ModuleHook.resolveAssemblyPath(path);

                if (assembly is null)
                {
                    // should not return null
                    return;
                }

                loaded?.TryAdd(assembly.GetName().Name, assembly);
            }

            return;
        }
    }

    private Assembly? UnturnedPreVanillaAssemblyResolve(object sender, ResolveEventArgs args)
    {
        if (loaded == null)
        {
            return null;
        }

        if (loaded.TryGetValue(args.Name, out var assembly))
        {
            return assembly;
        }

        var assemblyName = new AssemblyName(args.Name).Name;
        if (loaded.TryGetValue(assemblyName, out assembly))
        {
            loaded.TryAdd(args.Name, assembly);
            return assembly;
        }

        return null;
    }

    public void shutdown()
    {
        rocketModule?.shutdown();

        loaded?.Clear();
        loaded = null;
        ModuleHook.PreVanillaAssemblyResolve -= UnturnedPreVanillaAssemblyResolve;

        Instance = null;
    }
}