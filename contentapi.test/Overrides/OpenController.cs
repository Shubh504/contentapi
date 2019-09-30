using contentapi.Controllers;
using contentapi.Models;
using contentapi.Services;

namespace contentapi.test.Overrides
{
    public class OpenController : AccessController<Content, ContentView>
    {
        public OpenController(GenericControllerServices services, AccessService accessService) : base(services, accessService) { }

        protected override void SetLogField(ActionLog log, long id)
        {
            //Do NOTHING
        }

        public long GetUid()
        {
            return sessionService.GetCurrentUid();
        }
    }
}