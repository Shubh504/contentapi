using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Services.Extensions
{
    public static class ContentEntityProviderExtensions
    {
        /// <summary>
        /// Find a value by key/value/id (added constraints)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static async Task<EntityValue> FindValueAsync(this IEntityProvider entityProvider, string key, string value = null, long id = -1)
        {
            var valueSearch = new EntityValueSearch() { KeyLike = key };
            if(value != null)
                valueSearch.ValueLike = value;
            if(id > 0)
                valueSearch.EntityIds.Add(id);
            return (await entityProvider.GetEntityValuesAsync(valueSearch)).OnlySingle();
        }

        public static async Task<T> FindByIdAsyncGeneric<T>(this IEntityProvider provider, long id, Func<EntitySearch, Task<List<T>>> searcher)
        {
            var search = new EntitySearch();
            search.Ids.Add(id);
            return (await searcher(search)).OnlySingle();
        }

        /// <summary>
        /// Find some entity by id 
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static Task<EntityPackage> FindByIdAsync(this IEntityProvider provider, long id)
        {
            return provider.FindByIdAsyncGeneric(id, provider.GetEntityPackagesAsync);
        }

        public static async Task<EntityRelation> FindRelationByIdAsync(this IEntityProvider provider, long id)
        {
            var search = new EntityRelationSearch();
            search.Ids.Add(id);
            return (await provider.GetEntityRelationsAsync(search)).OnlySingle();
        }

        /// <summary>
        /// Find some entity by id 
        /// </summary>
        /// <param name="id"></param>
        /// <typeparam name="E"></typeparam>
        /// <returns></returns>
        public static Task<Entity> FindByIdBaseAsync(this IEntityProvider provider, long id)
        {
            return provider.FindByIdAsyncGeneric(id, provider.GetEntitiesAsync);
        }

        public static Task DeleteAsync(this IEntityProvider provider, EntityPackage package)
        {
            var deletes = new List<EntityBase>();
            deletes.Add(package.Entity);
            deletes.AddRange(package.Values);
            deletes.AddRange(package.Relations);
            return provider.DeleteAsync(deletes.ToArray());
        }
    }
}