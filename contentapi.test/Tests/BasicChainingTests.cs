using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Configs;
using contentapi.Services.Implementations;
using contentapi.Views;
using Xunit;

namespace contentapi.test
{
    public class BasicChainingTests : ServiceConfigTestBase<ChainService, SystemConfig>
    {
        protected SystemConfig myConfig = new SystemConfig() 
        { 
            ListenTimeout = TimeSpan.FromSeconds(60),
            ListenGracePeriod = TimeSpan.FromSeconds(10)
        };

        protected override SystemConfig config => myConfig;

        protected ChainServices services;

        public BasicChainingTests() : base()
        {
            services = CreateService<ChainServices>();
        }

        protected ChainRequest<UserSearch, UserViewFull> BasicChainRequest(Requester requester)
        {
            return new ChainRequest<UserSearch, UserViewFull>()
            {
                baseSearch = new UserSearch(),
                retriever = (s) => services.user.SearchAsync(s, requester),
                chains = new List<Chaining>(),
                mergeLock = new object(),
                mergeList = new List<TaggedChainResult>()
            };
        }

        [Fact]
        public void BasicSingleTest() //Does the LOW level (actual chaining) thing work?
        {
            //Just a SIMPLE little chain!
            var requester = new Requester() { system = true };
            var user = services.user.WriteAsync(new UserViewFull() { username = "simple" }, requester).Result;

            var chain = BasicChainRequest(requester);

            service.ChainAsync(chain, new List<List<IIdView>>()).Wait();
            Assert.Single(chain.mergeList);
            Assert.Equal(user.id, chain.mergeList.First().id);
            Assert.Equal(user.id, ((dynamic)chain.mergeList.First().result).id);
        }

        [Fact]
        public void BasicChainTest() //Does the LOW level (actual chaining) thing work?
        {
            //Need a user first
            var requester = new Requester() { system = true };
            var user = services.user.WriteAsync(new UserViewFull() { username = "simpleuser" }, requester).Result;

            //Now create some content as user
            var content = services.content.WriteAsync(new ContentView() { name = "simplecontent" }, new Requester() {userId = user.id}).Result;

            //Create another user to ensure the chaining only gets the first one
            var NOTUSER = services.user.WriteAsync(new UserViewFull() { username = "simpleuser2" }, requester).Result;

            //Same old user chaining, BUT chain to content
            var chain = BasicChainRequest(requester);
            chain.chains = new[] { new Chaining() { 
                viewableIdentifier = "0.createUserId",
                index = 0,
                getField = "createUserId",
                searchField = "Ids"
            }};

            //This time, have some previous results to chain to
            service.ChainAsync(chain, new List<List<IIdView>>() { new List<IIdView>() {content}}).Wait();

            //Now, make sure that chained user is the only one returned!
            Assert.Single(chain.mergeList);
            Assert.Equal(user.id, chain.mergeList.First().id);
            Assert.Equal(user.id, ((dynamic)chain.mergeList.First().result).id);

            //Try another chain but this time remove the chaining. You should get two
            chain.chains = new List<Chaining>();
            chain.baseSearch = new UserSearch(); //Need to reset the search because ugh
            service.ChainAsync(chain, new List<List<IIdView>>() { new List<IIdView>() {content}}).Wait();

            Assert.True(chain.mergeList.Count == 2, "There should be two users when searching all!");
        }

        [Fact]
        public void EmptyTest()
        {
            var requester = new Requester() { system = true };
            var result = service.ChainAsync(new List<string>() , new Dictionary<string, List<string>>(), requester).Result;

            Assert.Empty(result);
        }

        [Fact]
        public void EmptyWithThingsTest()
        {
            var requester = new Requester() { system = true };
            var user = services.user.WriteAsync(new UserViewFull() { username = "simple" }, requester).Result;

            //Request nothing even though there are things
            var result = service.ChainAsync(new List<string>() , new Dictionary<string, List<string>>(), requester).Result;

            Assert.Empty(result);
        }

        [Fact]
        public void StringSingleTest()
        {
            //Just a SIMPLE little chain!
            var requester = new Requester() { system = true };
            var user = services.user.WriteAsync(new UserViewFull() { username = "simple" }, requester).Result;

            var result = service.ChainAsync(new List<string>() {"user"}, new Dictionary<string, List<string>>(), requester).Result;

            Assert.Contains("user", result.Keys);
            Assert.Single(result["user"]);
            Assert.Equal(user.id, ((dynamic)result["user"].First()).id);
        }
    }
}