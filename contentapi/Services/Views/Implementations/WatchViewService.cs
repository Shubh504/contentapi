using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

//TODO: Some notes: it was FAR TOO MUCH WORK to create this service! It is doing almost nothing,
//and certainly nothing NEW, yet it required all these classes and methods all over the place.
//PLEASE FIX THIS!!!

namespace contentapi.Services.Views.Implementations
{


    public class WatchViewService : BaseViewServices, IViewService<WatchView, BaseSearch> //WatchSearch>
    {
        protected WatchViewConverter converter;

        public WatchViewService(ViewServicePack services, ILogger<BaseViewServices> logger, WatchViewConverter converter) 
            : base(services, logger) 
        { 
            this.converter = converter;
        }

        public async Task<EntityRelation> GetWatchRaw(long id, Requester requester)
        {
            var item = await provider.FindRelationByIdAsync(id);

            //ONLY (literally, ONLY) the owner of the watch can remove it. Or system...
            if(!requester.system && item.entityId1 != requester.userId)
                throw new AuthorizationException("Can't modify this watch!");
            
            return item;
        }

        public async Task<WatchView> DeleteAsync(long id, Requester requester)
        {
            var item = await GetWatchRaw(id, requester);
            await provider.DeleteAsync(item);
            return converter.ToView(item);
        }

        //TODO: OK so findbyidasync would return null while the others would return "authorizationexception".
        //This is kind of a mess, think about how to fix this across all view services
        public async Task<WatchView> FindByIdAsync(long id, Requester requester)
        {
            return converter.ToView(await GetWatchRaw(id, requester));
            //var search = new BaseSearch();
            //search.Ids.Add(id);
            //return (await SearchAsync(search, requester)).OnlySingle();
        }

        //TODO: There are no revisions for this kind of view service. Again, fix view services; make derived 
        //interfaces for stuff like this
        public Task<System.Collections.Generic.List<WatchView>> GetRevisions(long id, Requester requester)
        {
            throw new System.NotImplementedException();
        }

        //protected EntityRelationSearch ModifySearch(EntityRelationSearch search)
        //{
        //    search = LimitSearch(search);
        //    search.TypeLike = $"{Keys.WatchRelation}%";
        //    return search;
        //}

        public async Task<System.Collections.Generic.List<WatchView>> SearchAsync(BaseSearch search, Requester requester) //WatchSearch search, Requester requester)
        {
            logger.LogTrace($"Watch SearchAsync called by {requester}");

            var relationSearch = services.mapper.Map<EntityRelationSearch>(search);
            relationSearch = LimitSearch(relationSearch);
            relationSearch.TypeLike = $"{Keys.WatchRelation}%";
            relationSearch.EntityIds1.Add(requester.userId);

            var relations = await services.provider.GetEntityRelationsAsync(relationSearch);
            return relations.Select(x => converter.ToView(x)).ToList();
        }

        public Task<WatchView> WriteAsync(WatchView view, Requester requester)
        {
            //Just don't even allow views with ids
            if(view.id != 0)
                throw new BadRequestException("Can't edit watches! Only delete or insert!");

            //Watches are a SIMPLE LIST. I don't care if you try to watch something that might be private...
            //you won't be able to read anything from it.... aaahhh but what about the number of watches?

            //TODO: Watches are turning out to be far more complicated than I originally thought.
            //Things need to be fixed; view services need to be separated from permissions or perhaps 
            //only the "differences" between views should be written... I don't know. Need different service structures.
            throw new NotImplementedException();
        }
    }
}