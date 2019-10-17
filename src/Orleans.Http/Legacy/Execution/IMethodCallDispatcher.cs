using System.Threading.Tasks;

namespace Orleans.Http.Execution
{
    public interface IMethodCallDispatcher
    {
        Task<object> Dispatch(IClusterClient client, MethodCall command);
    }
}