using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using ConsoleApp.Grpc;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

namespace ConsoleClient
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Press any key to start");
            Console.ReadKey();
            try
            {
                BenchmarkRunner.Run<ClientRunner>();
                //await new ClientRunner().Run();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.ReadKey();
        }
    }

    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Throughput, invocationCount: 500)]
    public class ClientRunner
    {
        private static GrpcChannel _channel;

        [GlobalSetup]
        public void Setup()
        {
            GrpcClientFactory.AllowUnencryptedHttp2 = true;
            _channel = GrpcChannel.ForAddress("http://localhost:55555",
                new GrpcChannelOptions());

        }

        [Benchmark]
        public async Task Run()
        {
            var client = _channel.CreateGrpcService<ICalculator>();
            var response = await client.MultiplyAsync(new MultiplyRequest {X = 11111, Y = 2});

            //for (var i = 0; i < 10000; i++)
            //{
                //Console.SetCursorPosition(0, 0);
                //Console.Write(i);
            //}
        }
    }
}