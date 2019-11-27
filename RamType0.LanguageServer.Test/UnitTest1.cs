using NUnit.Framework;
using RamType0.LanguageServer;
using System.Threading.Tasks;

namespace RamType0.LanguageServer.Test
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async ValueTask Test1()
        {
            var server = new LanguageServer();
            var connection = server.Connect();
            await Task.Delay(10000);
            var disposing = server.DisposeAsync();
            await connection;
            await disposing;
        }
    }
}