using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;
using ProtoBuf.Grpc;

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
        private readonly ScopedDependency _dependency;

        public MyCalculator(ScopedDependency dependency)
        {
            _dependency = dependency;
        }

        ValueTask<MultiplyResult> ICalculator.MultiplyAsync(MultiplyRequest request, CallContext context)
        {
            return new ValueTask<MultiplyResult>(new MultiplyResult {Result = request.X * request.Y});
        }
    }
}