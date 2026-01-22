using System;

namespace Game.Rpc.Contracts
{
    /// <summary>
    /// Marks an interface as an RPC service. ServiceId must be stable across versions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class RpcServiceAttribute : Attribute
    {
        public RpcServiceAttribute(int serviceId) => ServiceId = serviceId;
        public int ServiceId { get; }
    }

    /// <summary>
    /// Marks an interface method as an RPC method. MethodId must be stable within a service.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class RpcMethodAttribute : Attribute
    {
        public RpcMethodAttribute(int methodId) => MethodId = methodId;
        public int MethodId { get; }
    }
}
