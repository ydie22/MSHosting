using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtoBuf.Grpc;
using Serilog.Extensions.Logging;

namespace ConsoleApp.Grpc
{
    [DataContract]
    public class MultiplyRequest
    {
        [DataMember(Order = 1)] public int X { get; set; }

        [DataMember(Order = 2)] public int Y { get; set; }
    }

    [DataContract]
    public class MultiplyResult
    {
        [DataMember(Order = 1)] public int Result { get; set; }
    }

    [ServiceContract(Name = "Hyper.Calculator")]
    public interface ICalculator
    {
        ValueTask<MultiplyResult> MultiplyAsync(MultiplyRequest request, CallContext context = default);
    }

    public class MyCalculator : ICalculator
    {

        private static readonly ILogger log = Log.ForContext<MyCalculator>();
        private readonly ScopedDependency _dependency;

        public MyCalculator(ScopedDependency dependency)
        {
            _dependency = dependency;
        }

        ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request, CallContext context)
        {
            log.LogInformation("What the...");
            return new ValueTask<MultiplyResult>(new MultiplyResult {Result = request.X * request.Y});
        }
    }
}