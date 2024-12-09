using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Rocket.API;

namespace Rocket.Core.Assets;

public class JSONFileAsset<T> : Asset<T> where T : class
{
    private readonly SemaphoreSlim @lock = new(1);

    private readonly string file;

    private readonly T defaultInstance;

    private readonly JsonSerializerSettings serializerSettings;

    private readonly Formatting formatting;

    public static JsonSerializerSettings GetDefaultSerializerSettings() => new()
    {
        ContractResolver = new DefaultContractResolver
        {
            DefaultMembersSearchFlags = BindingFlags.Public | BindingFlags.Instance
        },
        Converters =
        [ 
            new StringEnumConverter()
        ]
    };


    public JSONFileAsset(
        string file, 
        T defaultInstance = null, 
        JsonSerializerSettings? serializerSettings = null, 
        Formatting formatting = Formatting.Indented)
    {
        this.file = file;
        this.defaultInstance = defaultInstance;
        this.serializerSettings = serializerSettings ?? GetDefaultSerializerSettings();
        this.formatting = formatting;
        Load();
    }

    private async Task<T> SerializeFileAsync()
    {
        await @lock.WaitAsync();

        var fileContents = JsonConvert.SerializeObject(instance, Formatting.Indented, serializerSettings);
        await File.WriteAllTextAsync(file, fileContents);

        @lock.Release();

        return instance;
    }

    public Task<T> SaveAsync()
    {
        try
        {
            string directoryName = Path.GetDirectoryName(file);

            if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);

            if (instance is null)
                instance = defaultInstance;

            if (instance is null)
            {
                instance = Activator.CreateInstance<T>();

                if (instance is IDefaultable defaultable)
                    defaultable.LoadDefaults();
            }

            return SerializeFileAsync();
        }
        catch (Exception exception)
        {
            throw new Exception($"Failed to serialize JSONFileAsset: '{file}'", exception);
        }
    }

    public override T Save()
    {
        var task = SaveAsync();
        task.Wait();
        return task.Result;
    }

    private async Task<T> DeserializeFileAsync()
    {
        await @lock.WaitAsync();

        var fileContents = await File.ReadAllTextAsync(file);
        var result = JsonConvert.DeserializeObject<T>(fileContents, serializerSettings);

        @lock.Release();

        return result;
    }

    public async Task LoadAsync(AssetLoaded<T> callback = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
                instance = await DeserializeFileAsync();

            await SaveAsync();
            callback?.Invoke(this);
        }
        catch (Exception innerException)
        {
            throw new Exception($"Failed to deserialize JSONFileAsset: '{file}'", innerException);
        }
    }

    public override async void Load(AssetLoaded<T> callback = null) => await LoadAsync(callback);

    public async Task UnloadAsync(AssetUnloaded<T> callback = null)
    {
        await SaveAsync();
        callback?.Invoke(this);
    }

    public override async void Unload(AssetUnloaded<T> callback = null) => await UnloadAsync(callback);
}