using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using Randomous.EntitySystem.Extensions;

namespace contentapi.Controllers
{
    public abstract class EntityBaseController<V> : SimpleBaseController where V : ViewBase
    {
        public EntityBaseController(ControllerServices services, ILogger<EntityBaseController<V>> logger)
            :base(services, logger) { }

        protected abstract string EntityType {get;}

        /// <summary>
        /// Create a view with ONLY the unique fields for your controller filled in. You could fill in the
        /// others I guess, but they will be overwritten
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        protected abstract V CreateBaseView(EntityPackage package);

        /// <summary>
        /// Create a package with ONLY the unique fields for your controller filled in. 
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        protected abstract EntityPackage CreateBasePackage(V view);

        protected virtual V ConvertToView(EntityPackage package)
        {
            var view = CreateBaseView(package);

            //We are able to pull both the edit and create because all the info is in the package. we can't
            //go the other way (see above) because the view doesn't necessarily have the data we need.
            view.editDate = (DateTime)package.GetRelation(keys.StandInRelation).createDate;
            view.createDate = (DateTime)package.Entity.createDate;

            if(!package.HasRelation(keys.StandInRelation))
                throw new InvalidOperationException("Package has no stand-in relation, it is not part of the history system!");

            view.id = package.GetRelation(keys.StandInRelation).entityId1;

            return view;
        }

        //TRUST the view. Assume it is written correctly, that createdate is set properly, etc.
        protected virtual EntityPackage ConvertFromView(V view)
        {
            var package = CreateBasePackage(view);
            package.Entity.id = 0; //History dictates this must be 0, all entities for ANY view are new
            package.Entity.type = TypeSet(package.Entity.type, EntityType); //Steal the type directly from whatever they created
            package.Entity.createDate = view.createDate; //trust the create date from the view.

            //Assume any new view is active
            var relation = NewRelation(view.id, keys.StandInRelation, keys.ActiveValue);
            relation.createDate = DateTime.UtcNow;
            package.Add(relation);

            return package;
        }


        /// <summary>
        /// Create the "base" entity that serves as a parent to all actual content. It allows the 
        /// special history system to function.
        /// </summary>
        /// <returns></returns>
        protected async Task<Entity> CreateStandInAsync()
        {
            //Create date is now and will never be changed
            var standin = NewEntity(null, keys.ActiveValue).Entity; 
            standin.type = keys.StandInType;
            await services.provider.WriteAsync(standin);

            return standin;
        }

        protected async Task<Entity> GetStandInAsync(long id)
        {
            var standin = await services.provider.FindByIdBaseAsync(id); //go find the standin

            if(!TypeIs(standin?.type, keys.StandInType))
                throw new InvalidOperationException($"No entity with id {id}");
            
            return standin;
        }

        /// <summary>
        /// Find an entity by its STAND IN id (not the regular ID, use the service for that)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<EntityPackage> FindByIdAsync(long standinId)
        {
            var realIds = await ConvertStandInIdsAsync(standinId);

            if(realIds.Count() == 0)
                return null;
            else if(realIds.Count() > 1)
                throw new InvalidOperationException("Multiple entities for given standin, are there trailing history elements?");
            
            return await services.provider.FindByIdAsync(realIds.First());
        }

        /// <summary>
        /// Mark the currently active entities/relations etc for the given standin as inactive. Return a copy
        /// of the objects as they were before being edited (for rollback purposes)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<List<EntityBase>> MarkLatestInactive(long standinId, string subValue = null)
        {
            var restoreCopies = new List<EntityBase>();

            //Go find the "previous" active content and relation. Ensure they are null if there are none (it should be ok, just throw a warning)
            var lastActiveRelation = (await GetActiveRelation(standinId)) ?? throw new InvalidOperationException("Could not find active relation in historic content system");
            var lastActiveContent = (await services.provider.FindByIdBaseAsync(lastActiveRelation.entityId2)) ?? throw new InvalidOperationException("Could not find active content in historic content system");

            //The state to restore should everything go south.
            restoreCopies.Add(new EntityRelation(lastActiveRelation));
            restoreCopies.Add(new Entity(lastActiveContent));

            //Mark the last content as historic (also set override sub-value if given, otherwise use what existed before)
            lastActiveRelation.value = TypeSet((subValue ?? TypeSub(lastActiveRelation.value, keys.ActiveValue)), keys.InactiveValue);
            lastActiveContent.type = TypeSet(lastActiveContent.type, keys.HistoryKey); //Prepend to the old type just to keep it around

            //Update old values FIRST so there's NO active content
            await services.provider.WriteAsync<EntityBase>(lastActiveContent, lastActiveRelation);

            return restoreCopies;
        }


        protected async Task<EntityPackage> WriteViewAsync(V view)
        {
            logger.LogTrace("WriteViewAsync called");

            var package = ConvertFromView(view); //Assume this does EVERYTHING

            //We assume the package was there.
            var standinRelation = package.GetRelation(keys.StandInRelation);
            Entity standin = null; //This MAY be needed for some future stuff.
            bool newPackage = false;

            if(standinRelation.entityId1 == 0)
            {
                //Link in a new standin
                newPackage = true;
                logger.LogInformation("Creating standin for apparently new view");
                standin = await CreateStandInAsync();
                standinRelation.entityId1 = standin.id;
            }
            else
            {
                standin = await GetStandInAsync(standinRelation.entityId1);
            }

            List<EntityBase> restoreCopies = new List<EntityBase>();

            //When it's NOT a new package, we have to go update historical records. Oof
            if(!newPackage)
                restoreCopies = await MarkLatestInactive(standinRelation.entityId1);

            try
            {
                //Write the new historic aware package
                await services.provider.WriteAsync(package);
            }
            catch
            {
                //Oh shoot something happened! get rid of the changes to the historic content (assumes write does not persist changes)
                await services.provider.WriteAsync(restoreCopies.ToArray());
                throw;
            }

            return package;
        }

        /// <summary>
        /// Allow "fake" deletion of ANY historic entity (of any type)
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected Task DeleteEntity(long standinId)
        {
            return MarkLatestInactive(standinId, keys.DeleteAction);
        }

        /// <summary>
        /// Check the entity for deletion. Throw exception if can't
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async virtual Task<EntityPackage> DeleteEntityCheck(long standinId)
        {
            var last = await FindByIdAsync(standinId);

            if(last == null || !TypeIs(last.Entity.type, EntityType))
                throw new InvalidOperationException("No entity with that ID and type!");
            
            return last;
        }

        /// <summary>
        /// Find the relation that represents the current active content for the given standin 
        /// </summary>
        /// <param name="standinId"></param>
        /// <returns></returns>
        protected async Task<EntityRelation> GetActiveRelation(long standinId)
        {
            var search = new EntityRelationSearch();
            search.EntityIds1.Add(standinId);
            search.TypeLike = keys.StandInRelation;
            var result = await services.provider.GetEntityRelationsAsync(search);
            return result.Where(x => TypeIs(x.value, keys.ActiveValue)).OnlySingle();
        }

        /// <summary>
        /// Convert stand-in ids (from the users) to real ids (that I use for searching actual entities)
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        protected async Task<List<long>> ConvertStandInIdsAsync(List<long> ids)
        {
            //This bites me every time. I need to fix the entity search system.
            if(ids.Count == 0)
                return new List<long>();

            var relations = services.provider.GetQueryable<EntityRelation>();
            relations = services.provider.ApplyEntityRelationSearch(relations, 
                new EntityRelationSearch()
                {
                    EntityIds1 = ids,
                    TypeLike = keys.StandInRelation
                });

            return await services.provider.GetListAsync(relations.Where(x => EF.Functions.Like(x.value, $"{keys.ActiveValue}%")).Select(x => x.entityId2));
        }

        protected Task<List<long>> ConvertStandInIdsAsync(params long[] ids)
        {
            return ConvertStandInIdsAsync(ids.ToList());
        }

        /// <summary>
        /// Modify a search converted from users so it works with real entities
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        protected async Task<EntitySearch> ModifySearchAsync(EntitySearch search)
        {
            //The easy modifications
            search = LimitSearch(search);

            if(string.IsNullOrWhiteSpace(search.TypeLike))
                search.TypeLike = "%";

            search.TypeLike = TypeSet(search.TypeLike, EntityType); 

            //We have to find the rEAL ids that they want. This is the only big thing...?
            if(search.Ids.Count > 0)
            {
                search.Ids = await ConvertStandInIdsAsync(search.Ids);

                if(search.Ids.Count == 0)
                    search.Ids.Add(long.MaxValue); //This should never be found, and should ensure nothing is found in the search
            }

            return search;
        }

        protected virtual V PostCleanUpdateAsync(V view, Entity standin, EntityPackage existing)
        {
            view.createDate = (DateTime)standin.createDate;

            //Don't allow posting over some other entity! THIS IS SUUUUPER IMPORTANT!!!
            if(!TypeIs(existing.Entity.type, EntityType))
                throw new InvalidOperationException($"No entity of proper type with id {view.id}");
            
            return view;
        }

        protected virtual V PostCleanCreateAsync(V view)
        {
            //Create date should be NOOOWW
            view.createDate = DateTime.UtcNow;
            return view;
        }

        protected virtual async Task<V> PostCleanAsync(V view)
        {
            if(view.id > 0)
            {
                //This might be too heavy
                //This will already throw an exception if there isn't one.
                var standin = await GetStandInAsync(view.id);
                var existing = await FindByIdAsync(view.id);

                view = PostCleanUpdateAsync(view, await GetStandInAsync(view.id), await FindByIdAsync(view.id));
            }
            else
            {
                view = PostCleanCreateAsync(view);
            }

            return view;
        }

        protected async Task<List<V>> ViewResult(IQueryable<Entity> query)
        {
            var packages = await services.provider.LinkAsync(query);
            return packages.Select(x => ConvertToView(x)).ToList();
        }
    }
}