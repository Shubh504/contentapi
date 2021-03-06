using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Services.Constants;
using contentapi.Services.Extensions;
using contentapi.Views;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Services.Implementations
{
    public class ModuleMessageViewService : BaseViewServices<ModuleMessageView, ModuleMessageViewSearch>, IViewReadService<ModuleMessageView, ModuleMessageViewSearch>
    {
        protected ModuleMessageViewSource moduleMessageSource;

        public ModuleMessageViewService(ViewServicePack services, ILogger<ModuleMessageViewService> logger, 
            ModuleMessageViewSource moduleMessageSource)
            : base(services, logger) 
        { 
            this.moduleMessageSource = moduleMessageSource;
        }

        public override async Task<List<ModuleMessageView>> PreparedSearchAsync(ModuleMessageViewSearch search, Requester requester)
        {
            var result = await moduleMessageSource.SimpleSearchAsync(search, (q) =>
                q.Where(x => x.relation.entityId2 == 0 || x.relation.entityId2 == -requester.userId) //Can only get your own module messages
                //services.permissions.PermissionWhere(
                //    q.Where(x => x.relation.type != $"{Keys.ActivityKey}{Keys.FileType}"),  //This may change sometime
                //    requester, Keys.ReadAction, new PermissionExtras() 
                //    {  
                //        allowNegativeOwnerRelation = search.IncludeAnonymous, 
                //        allowedRelationTypes = new List<string>() { Keys.ActivityKey + Keys.ModuleType }
                //    })
            );

            return result;
        }

        //A special endpoint for MODULES (not users) to add module messages
        public async Task<ModuleMessageView> AddMessageAsync(ModuleMessageView basic) //long senderuid, long receiveruid, string message, string module)
        {
            var relation = moduleMessageSource.FromView(basic);
            await provider.WriteAsync(relation);
            return moduleMessageSource.ToView(relation);
        }

        //public class TempGroup
        //{
        //    public long userId {get;set;}
        //    public long contentId {get;set;}
        //    //public string action {get;set;}
        //}

        //public async Task<List<ActivityAggregateView>> SearchAggregateAsync(ActivitySearch search, Requester requester)
        //{
        //    //Repeat code, be careful
        //    await FixWatchLimits(watchSource, requester, search.ContentLimit);

        //    var ids = activity.SearchIds(search, q => services.permissions.PermissionWhere(q, requester, Keys.ReadAction));

        //    var groups = await activity.GroupAsync<EntityRelation,TempGroup>(ids, x => new TempGroup(){ userId = x.entityId1, contentId = -x.entityId2 });

        //    return groups.ToLookup(x => x.Key.contentId).Select(x => new ActivityAggregateView()
        //    {
        //        id = x.Key,
        //        count = x.Sum(y => y.Value.count),
        //        lastDate = x.Max(y => y.Value.lastDate),
        //        firstDate = x.Min(y => y.Value.firstDate),
        //        lastId = x.Max(y => y.Value.lastId),
        //        //userActions = x.Select(y => new { user = y.Key.userId, action = y.Key.action })
        //        userIds = x.Select(y => y.Key.userId).Distinct().ToList()
        //    }).ToList();
        //}
    }
}