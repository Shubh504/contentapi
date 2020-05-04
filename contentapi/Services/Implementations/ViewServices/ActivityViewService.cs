using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ActivitySearch : EntitySearchBase
    {
        public List<long> UserIds {get;set;} = new List<long>();
        public List<long> ContentIds {get;set;} = new List<long>();

        public string Type {get;set;}
        public bool IncludeAnonymous {get;set;}
        public TimeSpan recentCommentTime {get;set;}
    }

    public class ActivityControllerProfile : Profile
    {
        public ActivityControllerProfile() 
        {
            CreateMap<ActivitySearch, EntityRelationSearch>()
                .ForMember(x => x.EntityIds1, o => o.MapFrom(s => s.UserIds))
                .ForMember(x => x.EntityIds2, o => o.MapFrom(s => s.ContentIds.Select(x => -x).ToList()));
            CreateMap<EntityRelation, ActivityView>()
                .ForMember(x => x.date, o => o.MapFrom(s => s.createDate))
                .ForMember(x => x.userId, o => o.MapFrom(s => s.entityId1))
                .ForMember(x => x.contentId, o => o.MapFrom(s => -s.entityId2))
                ; //Don't need to reverse this one
        }
    }

    public class ActivityViewService : BaseViewServices, IViewService<ActivityView, ActivitySearch>
    {
        protected IActivityService activityService;

        public ActivityViewService(ViewServices services, ILogger<ActivityViewService> logger, IActivityService activityService) 
            : base(services, logger) 
        { 
            this.activityService = activityService;
        }

        //When you split the interface into read-only and other, get rid of these
        public Task<ActivityView> WriteAsync(ActivityView view, ViewRequester requester) { throw new NotImplementedException(); }
        public Task<ActivityView> DeleteAsync(long id, ViewRequester requester) { throw new NotImplementedException(); }
        public Task<IList<ActivityView>> GetRevisions(long id, ViewRequester requester) { throw new NotImplementedException(); }

        protected EntityRelationSearch ModifySearch(EntityRelationSearch search)
        {
            //It is safe to just call any endpoint, because the count is limited to 1000.
            search = LimitSearch(search);
            search.TypeLike = $"{keys.ActivityKey}";
            return search;
        }

        public async Task<ActivityResultView> SearchResultAsync(ActivitySearch search, ViewRequester requester)
        {
            return new ActivityResultView()
            {
                activity = (await SearchAsync(search, requester)).ToList(),
                comments = (await SearchCommentsAsync(search, requester)).ToList()
            };
        }

        public async Task<IList<ActivityView>> SearchAsync(ActivitySearch search, ViewRequester requester)
        {
            var relationSearch = ModifySearch(services.mapper.Map<EntityRelationSearch>(search));

            if(string.IsNullOrWhiteSpace(search.Type))
                search.Type = "%";

            relationSearch.TypeLike += search.Type;

            var query = BasicReadQuery(requester.userId, relationSearch, x => -x.entityId2, new PermissionExtras() { allowNegativeOwnerRelation = search.IncludeAnonymous} )
                            .Where(x => x.relation.type != $"{keys.ActivityKey}{keys.FileType}");

            var relations = await services.provider.GetListAsync(FinalizeQuery<EntityRelation>(query, x=> x.relation.id, relationSearch));

            return relations.Select(x => 
            {
                var view = activityService.ConvertToView(x);
                //Strip the typing too. This is probably unsafe, I don't know what to do about it for now
                view.contentType = view.contentType.Substring(keys.ContentType.Length);
                return view;
            }).ToList();
        }

        public async Task<IList<CommentActivityView>> SearchCommentsAsync(ActivitySearch search, ViewRequester requester)
        {
            var result = new List<CommentActivityView>();

            //No matter the search, get comments for up to the recent thing.
            if(search.recentCommentTime.Ticks > 0)
            {
                var commentSearch = new EntityRelationSearch()
                {
                    TypeLike = $"{keys.CommentHack}%",
                    CreateStart = DateTime.Now.Subtract(search.recentCommentTime),
                    Reverse = true
                };

                var commentQuery = BasicReadQuery(requester.userId, commentSearch, x => x.entityId1); //entityid1 is the parent content, they need perms
                var finalComments = await provider.GetListAsync(
                    FinalizeQuery<EntityRelation>(commentQuery, x => x.relation.id, commentSearch) //ALWAYS GIVE ID GOSH
                    .Select(x => new { contentId = x.entityId1, userId = -x.entityId2, date = x.createDate})); //We only want SOME fields, don't pull them all! TOO MUCH

                foreach(var group in finalComments.ToLookup(x => x.contentId))
                {
                    var commentActivity = new CommentActivityView()
                    {
                        count = group.Count(),
                        parentId = group.Key,
                        userIds = group.Select(x => x.userId).Distinct().ToList(),
                        lastDate = group.Max(x => (DateTime)x.date),
                    };

                    //Apply current timezone to the datetime. This MAY be dangerous
                    commentActivity.lastDate = new DateTime(commentActivity.lastDate.Ticks, DateTime.Now.Kind);

                    result.Add(commentActivity);
                }
            }

            return result;
        }

        public async Task<ActivityView> FindByIdAsync(long id, ViewRequester requester)
        {
            var search = new ActivitySearch();
            search.Ids.Add(id);
            return (await SearchAsync(search, requester)).OnlySingle();
        }
    }
}