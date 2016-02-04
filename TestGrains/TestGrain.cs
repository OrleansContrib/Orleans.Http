using Orleans;
using System.Threading.Tasks;

namespace TestGrains
{
    public class TestGrain : Grain, ITestGrain
    {
        // this grain does a no-op, so we can test performance of the infrastructure, rather than the grain
        public Task Test()
        {
            return TaskDone.Done;
        }
    }
}
