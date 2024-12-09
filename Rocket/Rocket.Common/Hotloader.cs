using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AsmResolver.IO;
using AsmResolver.PE;
using AsmResolver.PE.DotNet;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Metadata.Strings;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using HarmonyLib;

namespace Rocket.Common;

/*
 *  This code is a modified version of a OpenMod hotloader.
 *  # Credits #
 *  Original authors: Enes Sadik Özbek & OpenMod contributors
 *  Source: https://github.com/openmod/openmod/blob/c77761250883d255a457d50fcc71c6c9a371e7d4/framework/OpenMod.Common/Hotloading/Hotloader.cs
 */
/// <summary>
/// Adds support for hotloading assemblies.
/// Use <see cref="LoadAssembly(byte[])"/> instead of <see cref="Assembly.Load(byte[])"/>.
/// </summary>
public static class Hotloader
{
    private static readonly ConcurrentDictionary<AssemblyName, Assembly> assemblies = new();

    /// <summary>
    /// Defines if hotloading is enabled.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    static Hotloader()
    {
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        return FindAssembly(new(args.Name));
    }

    /// <summary>
    /// Hotloads an assembly. Redirects to <see cref="Assembly.Load(byte[], byte[])"/> if <see cref="Enabled"/> is set to false.
    /// </summary>
    /// <param name="dllFilePath">Path to the assembly file to hotload.</param>
    /// <param name="pdbFilePath">Path to the assembly symbols file.</param>
    /// <returns>The loaded assembly.</returns>
    public static Assembly LoadAssembly(string dllFilePath, string? pdbFilePath = null)
    {
        var dllFile = new FileInfo(dllFilePath);
        var assemblyData = !dllFile.Exists ? [] : File.ReadAllBytes(dllFile.FullName);
            
        var pdbFile = new FileInfo(pdbFilePath ?? Path.ChangeExtension(dllFilePath, ".pdb"));
        var assemblySymbols = !pdbFile.Exists ? [] : File.ReadAllBytes(pdbFile.FullName);
            
        return LoadAssembly(assemblyData, assemblySymbols);
    }
    
    /// <summary>
    /// Hotloads an assembly. Redirects to <see cref="Assembly.Load(byte[])"/> if <see cref="Enabled"/> is set to false.
    /// </summary>
    /// <param name="assemblyData">The assembly to hotload.</param>
    /// <returns>The loaded assembly.</returns>
    public static Assembly LoadAssembly(byte[] assemblyData)
    {
        return LoadAssembly(assemblyData, assemblySymbols: null);
    }

    /// <summary>
    /// Hotloads an assembly. Redirects to <see cref="Assembly.Load(byte[], byte[])"/> if <see cref="Enabled"/> is set to false.
    /// </summary>
    /// <param name="assemblyData">The assembly to hotload.</param>
    /// <param name="assemblySymbols">A byte array that contains the raw bytes representing the symbols for the assembly.</param>
    /// <returns>The loaded assembly.</returns>
    public static Assembly LoadAssembly(byte[] assemblyData, byte[]? assemblySymbols)
    {
        if (!Enabled) return Assembly.Load(assemblyData, assemblySymbols);

        var image = PEImage.FromBytes(assemblyData);

        var metadata = image.DotNetDirectory!.Metadata!;
        var tablesStream = metadata.GetStream<TablesStream>();
        var oldStringsStream = metadata.GetStream<StringsStream>();

        // get reference to assembly def row
        ref var assemblyRow = ref tablesStream
            .GetTable<AssemblyDefinitionRow>(TableIndex.Assembly)
            .GetRowRef(1);

        // get original name
        string name = oldStringsStream.GetStringByIndex(assemblyRow.Name)!;

        // structure full name
        var version = new Version(assemblyRow.MajorVersion, assemblyRow.MinorVersion, assemblyRow.BuildNumber,
            assemblyRow.RevisionNumber);
        var realFullName = $"{name}, Version={version}, Culture=neutral, PublicKeyToken=null";
        var realAssemblyName = new AssemblyName(realFullName);

        // generate new name
        var guid = Guid.NewGuid().ToString("N").Substring(0, 6);
        var newName = $"{name}-{guid}";

        // update assembly def name
        assemblyRow.Name = oldStringsStream.GetPhysicalSize();
        using var output = new MemoryStream();

        var writer = new BinaryStreamWriter(output);

        writer.WriteBytes(oldStringsStream.CreateReader().ReadToEnd());
        writer.WriteBytes(Encoding.UTF8.GetBytes(newName));
        writer.WriteByte(0); // Add Null Terminator
        writer.Align(4);

        var newStringsStream = new SerializedStringsStream(output.ToArray());
        // strings index size may have changed, updating in tables stream
        tablesStream.StringIndexSize = newStringsStream.IndexSize;

        // replace old strings with new one
        metadata.Streams[metadata.Streams.IndexOf(oldStringsStream)] = newStringsStream;

        var builder = new ManagedPEFileBuilder();
        // reuse old output stream
        output.SetLength(0);

        builder.CreateFile(image).Write(output);

        var newAssemblyData = output.ToArray();

        var assembly = Assembly.Load(newAssemblyData, assemblySymbols);
        assemblies[realAssemblyName] = assembly;
        return assembly;
    }

    /// <summary>
    /// Removes an assembly from the hotloader cache.
    /// </summary>
    /// <param name="assembly">The assembly to remove.</param>
    public static void Remove(Assembly assembly)
    {
        foreach (var key in assemblies
                     .Where(kv => kv.Value == assembly)
                     .Select(x => x.Key)
                     .ToArray())
        {
            assemblies.Remove(key, out _);
        }
    }

    /// <summary>
    /// Resolves a hotloaded assembly. Hotloaded assemblies have an auto generated assembly name.
    /// </summary>
    /// <param name="name">The assembly name to resolve.</param>
    /// <returns><b>The hotloaded assembly</b> if found; otherwise, <b>null</b>.</returns>
    public static Assembly? FindAssembly(AssemblyName name) => assemblies.GetValueOrDefault(name);

    /// <summary>
    /// Gets all hotloaded assemblies.
    /// </summary>
    /// <returns>The hotloaded assemblies.</returns>
    public static ICollection<Assembly> GetHotloadedAssemblies() => assemblies.Values;

    /// <summary>
    /// Gets the real assembly name of any assembly. <br/>
    /// Real assembly name will be retrieved for the hotloaded assemblies.
    /// </summary>
    /// <param name="assembly">The assembly to get the real name of.</param>
    /// <returns>
    /// <b>The real assembly name</b> of an assembly. <br/>
    /// If the given assembly was hotloaded, it will return <b>the real assembly name</b>.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static AssemblyName GetRealAssemblyName(Assembly assembly)
    {
        var assemblyName = assemblies.FirstOrDefault(kv => kv.Value == assembly).Key;
        return assemblyName ?? assembly.GetName();
    }

    /// <summary>
    /// Gets the assembly name of any assembly. <br/>
    /// Hotloaded assemblies have an auto-generated assembly name. <br/>
    /// </summary>
    /// <remarks>
    /// <b>This method ignores the <see cref="Assembly.GetName()" /> patch and provides auto-generated assembly name for the hotloaded assembly.</b>
    /// </remarks>
    /// <param name="assembly">The assembly to get the real name of.</param>
    /// <returns><b>The assembly name</b> of the assembly. If the given assembly was hotloaded, it will return an <b>auto-generated assembly name</b>.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static AssemblyName GetAssemblyName(Assembly assembly)
    {
        var assemblyName = assembly.GetName();
        return assemblyName;
    }
}