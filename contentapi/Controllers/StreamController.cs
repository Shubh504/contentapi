using contentapi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class StreamController : BaseSimpleController
    {
        protected ITempTokenService<long> tokenService;

        public StreamController(BaseSimpleControllerServices services, ITempTokenService<long> tokenService) : base(services)
        {
            this.tokenService = tokenService;
        }

        [HttpGet("auth")]
        [Authorize]
        public ActionResult<string> GetAuth()
        {
            return tokenService.GetToken(GetRequesterUid());
        }
    }
}