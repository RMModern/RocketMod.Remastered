using Rocket.API;
using System;

namespace Rocket.Core.Assets
{
    public class Asset<T> : IAsset<T> where T : class
    {
        protected T? instance = null;

        public T Instance
        {
            get
            {
                if (instance is null) Load();
                return instance!;
            }
            set
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (value is null)
                    // ReSharper disable once HeuristicUnreachableCode
                    return;

                instance = value;
                Save();
            }
        }

        public virtual T Save()
        {
            return instance!;
        }

        public virtual void Load(AssetLoaded<T> callback = null)
        {
            callback?.Invoke(this);
        }

        public virtual void Unload(AssetUnloaded<T> callback = null)
        {
            callback?.Invoke(this);
        }
    }
}