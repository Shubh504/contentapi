using System;
using System.Linq;
using System.Threading;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.Data.Sqlite;
using Xunit;

namespace contentapi.test
{
    [Collection("ASYNC")]
    public class ModuleServiceTests : ServiceConfigTestBase<ModuleService, ModuleServiceConfig>
    {
        protected ModuleServiceConfig myConfig = new ModuleServiceConfig() { 
            ModuleDataConnectionString = "Data Source=moduledata;Mode=Memory;Cache=Shared"
        };

        protected SqliteConnection masterconnection;

        protected override ModuleServiceConfig config => myConfig;

        public ModuleServiceTests()
        {
            masterconnection = new SqliteConnection(myConfig.ModuleDataConnectionString);
            masterconnection.Open();
        }

        ~ModuleServiceTests()
        {
            masterconnection.Close();
        }

        [Fact]
        public void BasicCreate()
        {
            var modview = new ModuleView() { name = "test", code = "--wow"};
            var mod = service.UpdateModule(modview);
            Assert.True(mod.script != null);
        }

        [Fact]
        public void BasicParameterPass()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    return ""Id: "" .. uid .. "" Data: "" .. data
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", new Requester() {userId = 8});
            Assert.Equal("Id: 8 Data: whatever", result);
        }

        [Fact]
        public void BasicDataReadWrite()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    setdata(""myval"", ""something"")
                    return getdata(""myval"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
        }

        [Fact]
        public void SecondDataReadWrite()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    setdata(""myval"", ""something"")
                    return getdata(""myval"")
                end
                function command_wow2(uid, data)
                    return getdata(""myval"")
                end" 
            };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
            result = service.RunCommand("test", "wow2", "whatever", new Requester() {userId = 8});
            Assert.Equal("something", result);
        }

        [Fact]
        public void ReadMessagesInstant()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", requester);
            var messages = service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
            Assert.Equal(requester.userId, messages.First().receiverUid);
            Assert.Equal(requester.userId, messages.First().senderUid);
        }

        [Fact]
        public void ReadMessagesListen()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var result = service.RunCommand("test", "wow", "whatever", requester);
            var messages = service.ListenAsync(-1, requester, TimeSpan.FromSeconds(1), CancellationToken.None).Result;
            var lastId = messages.Last().id;
            var messageWait = service.ListenAsync(lastId, requester, TimeSpan.FromSeconds(1), CancellationToken.None);
            AssertNotWait(messageWait);
            result = service.RunCommand("test", "wow", "whatever", requester);
            messages = AssertWait(messageWait);
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
            Assert.True(messages.First().id > lastId);
        }

        [Fact]
        public void ReadMessagesListen0()
        {
            var modview = new ModuleView() { name = "test", code = @"
                function command_wow(uid, data)
                    sendmessage(uid, ""hey"")
                    sendmessage(uid + 1, ""hey NO"")
                end" 
            };
            var requester = new Requester() { userId = 9 };
            var mod = service.UpdateModule(modview);
            var messageWait = service.ListenAsync(0, requester, TimeSpan.FromSeconds(1), CancellationToken.None);
            AssertNotWait(messageWait);
            var result = service.RunCommand("test", "wow", "whatever", requester);
            var messages = AssertWait(messageWait);
            Assert.Single(messages);
            Assert.Equal("hey", messages.First().message);
            Assert.Equal("test", messages.First().module);
        }
    }
}